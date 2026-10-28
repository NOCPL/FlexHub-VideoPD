using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Data;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Services.Assessment;

public class SessionService(AppDbContext db, MeetingMapper meetings)
{
    public async Task<EngagementSession> EnsureAsync(EnsureSessionRequest request, Guid userId)
    {
        Meeting? meeting = null;
        if (request.MeetingId is Guid meetingId)
        {
            meeting = await db.Meetings.Include(m => m.CreditOfficer).FirstOrDefaultAsync(m => m.Id == meetingId);
            var existing = await db.EngagementSessions
                .Include(s => s.Answers)
                .Where(s => s.MeetingId == meetingId)
                .ToListAsync();
            var open = existing.OrderByDescending(s => s.StartedAt).FirstOrDefault(s => s.Status == SessionStatuses.InProgress);
            if (open is not null)
            {
                SyncMode(open, meeting);
                await db.SaveChangesAsync();
                return await LoadAsync(open.Id);
            }
            if (meeting is { Status: MeetingStatuses.Completed })
            {
                var latest = existing.OrderByDescending(s => s.StartedAt).FirstOrDefault();
                if (latest is not null)
                {
                    SyncMode(latest, meeting);
                    await db.SaveChangesAsync();
                    return await LoadAsync(latest.Id);
                }
            }
        }

        AssessmentApplication? app = null;
        if (request.ApplicationId is Guid appId)
        {
            app = await db.AssessmentApplications.FirstOrDefaultAsync(a => a.Id == appId)
                  ?? throw new KeyNotFoundException("Application not found.");
        }
        else
        {
            var bank = (request.Bank ?? meeting?.Bank ?? "").Trim();
            var group = (request.GroupId ?? meeting?.GroupId ?? "").Trim();
            var member = (request.MemberId ?? meeting?.MemberId ?? "").Trim();
            if (group.Length > 0 && member.Length > 0)
            {
                var matches = await db.AssessmentApplications.ToListAsync();
                app = matches
                    .Where(a => Same(a.GroupId, group) && Same(a.MemberId, member) && (bank.Length == 0 || Same(a.Bank, bank)))
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefault();
            }
        }

        if (app is null)
        {
            throw new KeyNotFoundException("No application matches this visit. Link CAM or group/member tags first.");
        }

        var appSessions = await db.EngagementSessions.Where(s => s.ApplicationId == app.Id).ToListAsync();
        var openForApp = appSessions.OrderByDescending(s => s.StartedAt).FirstOrDefault(s => s.Status == SessionStatuses.InProgress);
        if (openForApp is not null)
        {
            if (meeting is not null && openForApp.MeetingId is null)
            {
                openForApp.MeetingId = meeting.Id;
            }
            SyncMode(openForApp, meeting);
            await db.SaveChangesAsync();
            return await LoadAsync(openForApp.Id);
        }

        var guide = await ActiveGuideAsync(app.LoanCategoryId)
                    ?? throw new InvalidOperationException("No active engagement guide for this loan category.");
        var session = new EngagementSession
        {
            Id = Guid.NewGuid(),
            ApplicationId = app.Id,
            MeetingId = meeting?.Id ?? request.MeetingId,
            GuideId = guide.Id,
            GuideVersion = guide.Version,
            Mode = meeting is { Status: MeetingStatuses.Completed } ? SessionModes.Review : SessionModes.Live,
            Status = SessionStatuses.InProgress,
            CreatedByUserId = userId,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.EngagementSessions.Add(session);
        await db.SaveChangesAsync();
        return await LoadAsync(session.Id);
    }

    public async Task<EngagementSession> LoadAsync(Guid id) =>
        await db.EngagementSessions
            .AsSplitQuery()
            .Include(s => s.Application)!.ThenInclude(a => a!.LoanCategory)
            .Include(s => s.Application)!.ThenInclude(a => a!.Applicants).ThenInclude(p => p.RuleRuns).ThenInclude(r => r.Results)
            .Include(s => s.Guide)!.ThenInclude(g => g!.Groups)
            .Include(s => s.Guide)!.ThenInclude(g => g!.Tasks)
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == id)
        ?? throw new KeyNotFoundException("Session not found.");

    public async Task<IReadOnlyList<EngagementSession>> ListForApplicationAsync(Guid applicationId)
    {
        var list = await db.EngagementSessions
            .Include(s => s.Guide)!.ThenInclude(g => g!.Tasks)
            .Include(s => s.Answers)
            .Where(s => s.ApplicationId == applicationId)
            .ToListAsync();
        return list.OrderByDescending(s => s.StartedAt).ToList();
    }

    public async Task<EngagementSession> SaveAnswerAsync(Guid sessionId, int taskNumber, SaveTaskAnswerRequest request)
    {
        var session = await LoadAsync(sessionId);
        if (session.Status == SessionStatuses.Completed)
        {
            throw new InvalidOperationException("This session is completed. Answers are locked.");
        }
        var task = session.Guide!.Tasks.FirstOrDefault(t => t.Number == taskNumber)
                   ?? throw new KeyNotFoundException("Task not found on this guide.");
        var answers = AnswerMap(session);
        if (!SessionPlanner.IsVisible(task, answers) && request.Choices is { Count: > 0 })
        {
            throw new InvalidOperationException("This task is not applicable until its condition is met.");
        }

        var answer = session.Answers.FirstOrDefault(a => a.TaskNumber == taskNumber);
        if (answer is null)
        {
            answer = new SessionTaskAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                TaskNumber = taskNumber
            };
            db.SessionTaskAnswers.Add(answer);
            session.Answers.Add(answer);
        }
        if (request.Choices is not null)
        {
            answer.ChoicesJson = AssessmentMapper.ToJson(request.Choices);
            if (request.Status is null && request.Choices.Count > 0 && answer.Status == TaskCoverage.Pending)
            {
                answer.Status = TaskCoverage.Covered;
            }
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            answer.Status = request.Status.Trim() is TaskCoverage.Covered or TaskCoverage.NotCovered
                ? request.Status.Trim()
                : TaskCoverage.Pending;
        }
        if (request.Notes is not null)
        {
            answer.Notes = request.Notes.Trim();
        }
        answer.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return await LoadAsync(session.Id);
    }

    public async Task<EngagementSession> CompleteAsync(Guid sessionId)
    {
        var session = await LoadAsync(sessionId);
        if (session.Status == SessionStatuses.Completed)
        {
            return session;
        }
        var blockers = Blockers(session);
        if (blockers.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", blockers));
        }
        session.Status = SessionStatuses.Completed;
        session.Mode = SessionModes.Review;
        session.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return await LoadAsync(session.Id);
    }

    public async Task<SessionDetailDto> ToDetailAsync(EngagementSession session)
    {
        Meeting? meeting = null;
        if (session.MeetingId is Guid mid)
        {
            meeting = await db.Meetings
                .Include(m => m.CreditOfficer)
                .Include(m => m.Snapshots)
                .Include(m => m.Recordings)
                .FirstOrDefaultAsync(m => m.Id == mid);
        }
        return ToDetail(session, meeting);
    }

    public SessionDetailDto ToDetail(EngagementSession session, Meeting? meeting)
    {
        var guide = session.Guide ?? throw new InvalidOperationException("Guide missing.");
        var app = session.Application ?? throw new InvalidOperationException("Application missing.");
        var answers = AnswerMap(session);
        var probes = LatestProbes(app);
        var groups = new List<SessionGroupViewDto>();
        foreach (var group in guide.Groups.OrderBy(g => g.SortOrder))
        {
            var tasks = guide.Tasks.Where(t => t.GroupId == group.Id).OrderBy(t => t.Number).ToList();
            var views = new List<SessionTaskViewDto>();
            foreach (var task in tasks)
            {
                if (!SessionPlanner.IsVisible(task, answers))
                {
                    continue;
                }
                answers.TryGetValue(task.Number, out var answer);
                var taskProbes = probes.Where(p =>
                    p.FeedsTaskNumber == task.Number ||
                    (!string.IsNullOrWhiteSpace(task.RuleLink) &&
                     string.Equals(p.CheckKey, task.RuleLink, StringComparison.OrdinalIgnoreCase))).ToList();
                views.Add(new SessionTaskViewDto(
                    task.Number,
                    task.Title,
                    task.Description,
                    group.Name,
                    AssessmentMapper.Strings(task.ChoicesJson),
                    task.AllowMultiple,
                    task.ConditionTaskNumber,
                    AssessmentMapper.Strings(task.ConditionChoicesJson),
                    task.RuleLink,
                    SessionPlanner.Coverage(task, answers),
                    AssessmentMapper.Strings(answer?.ChoicesJson),
                    answer?.Notes ?? "",
                    taskProbes.Select(p => new SessionProbeDto(
                        p.Id, p.CheckKey, p.Name, p.Severity, AssessmentMapper.ParseArray(p.EvidenceJson),
                        p.Severity == RuleSeverities.High && string.IsNullOrWhiteSpace(answer?.Notes))).ToList()));
            }
            if (views.Count == 0)
            {
                continue;
            }
            groups.Add(new SessionGroupViewDto(
                group.Name,
                group.SortOrder,
                views.Count(v => v.Coverage == TaskCoverage.Covered),
                views.Count,
                views));
        }

        var visible = groups.Sum(g => g.VisibleCount);
        var covered = groups.Sum(g => g.CoveredCount);
        var photos = meeting?.Snapshots.OrderByDescending(s => s.CreatedAt).Select(s => meetings.ToSnapshot(meeting, s)).ToList()
                     ?? [];
        var recordings = meeting?.Recordings.OrderBy(r => r.Sequence).Select(r => new RecordingDto(
            r.Id, r.Status, r.Sequence, r.Bank, r.Branch, r.GroupId, r.MemberId, r.FilePath, r.DurationSeconds, r.Error,
            r.CreatedAt, r.JoinedAt, r.LeftAt)).ToList() ?? [];

        return new SessionDetailDto(
            session.Id,
            session.Status,
            session.Mode,
            guide.EventType,
            session.GuideVersion,
            guide.Name,
            app.Id,
            app.Code,
            app.DisplayName,
            app.LoanCategory?.Name ?? "",
            app.LoanAmount,
            session.MeetingId,
            meeting?.Code,
            app.Bank,
            app.Branch,
            app.GroupId,
            app.MemberId,
            session.StartedAt,
            session.CompletedAt,
            meeting?.ScheduledAt,
            meeting?.CreditOfficer?.Name,
            meeting?.DurationSeconds,
            Blockers(session),
            covered,
            visible,
            groups,
            photos,
            recordings);
    }

    public SessionListItemDto ToListItem(EngagementSession session)
    {
        var guide = session.Guide;
        var answers = AnswerMap(session);
        var tasks = guide?.Tasks ?? [];
        var visible = tasks.Count(t => SessionPlanner.IsVisible(t, answers));
        var covered = tasks.Count(t => SessionPlanner.Coverage(t, answers) == TaskCoverage.Covered);
        return new SessionListItemDto(
            session.Id, session.Status, session.Mode, guide?.Name ?? "", session.GuideVersion,
            covered, visible, null, session.StartedAt, session.CompletedAt);
    }

    private List<string> Blockers(EngagementSession session)
    {
        var guide = session.Guide!;
        var answers = AnswerMap(session);
        var blockers = new List<string>();
        if (!SessionPlanner.ConsentGranted(guide.Tasks.ToList(), answers))
        {
            blockers.Add("Introduction (consent to proceed) must be covered with Yes before the session can close.");
        }
        var app = session.Application!;
        foreach (var flag in LatestProbes(app).Where(p => p.Severity == RuleSeverities.High))
        {
            var task = guide.Tasks.FirstOrDefault(t =>
                t.Number == flag.FeedsTaskNumber ||
                (!string.IsNullOrWhiteSpace(t.RuleLink) &&
                 string.Equals(t.RuleLink, flag.CheckKey, StringComparison.OrdinalIgnoreCase)));
            if (task is null || !SessionPlanner.IsVisible(task, answers))
            {
                continue;
            }
            answers.TryGetValue(task.Number, out var answer);
            if (string.IsNullOrWhiteSpace(answer?.Notes))
            {
                blockers.Add($"High flag {flag.CheckKey} needs a recorded explanation on task {task.Number:000}.");
            }
        }
        return blockers.Distinct().ToList();
    }

    private static List<RuleRunResult> LatestProbes(AssessmentApplication app)
    {
        var flags = new List<RuleRunResult>();
        foreach (var person in app.Applicants)
        {
            foreach (var kind in person.RuleRuns.Select(r => r.Kind).Distinct())
            {
                var latest = person.RuleRuns.Where(r => r.Kind == kind).OrderByDescending(r => r.RanAt).First();
                flags.AddRange(latest.Results.Where(r => r.Fired));
            }
        }
        return flags;
    }

    private async Task<EngagementGuide?> ActiveGuideAsync(Guid loanCategoryId)
    {
        var guides = await db.EngagementGuides.Include(g => g.Groups).Include(g => g.Tasks)
            .Where(g => g.Status == GuideStatuses.Active)
            .ToListAsync();
        return guides
            .Where(g => AssessmentMapper.Guids(g.LoanCategoryIdsJson).Contains(loanCategoryId))
            .OrderByDescending(g => g.Version)
            .FirstOrDefault();
    }

    private static Dictionary<int, SessionTaskAnswer> AnswerMap(EngagementSession session) =>
        session.Answers
            .GroupBy(a => a.TaskNumber)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.UpdatedAt).First());

    private static void SyncMode(EngagementSession session, Meeting? meeting)
    {
        if (session.Status == SessionStatuses.Completed)
        {
            session.Mode = SessionModes.Review;
            return;
        }
        session.Mode = meeting is { Status: MeetingStatuses.Completed } ? SessionModes.Review : SessionModes.Live;
    }

    private static bool Same(string? left, string right) =>
        string.Equals((left ?? "").Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
