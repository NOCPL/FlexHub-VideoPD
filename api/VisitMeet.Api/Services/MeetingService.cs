using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Data;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Services;

public class MeetingMapper(IConfiguration config)
{
    public string PublicWebUrl => (config["PublicWebUrl"] ?? "http://127.0.0.1:43123").TrimEnd('/');

    public string HostUrl(string? slug) =>
        string.IsNullOrEmpty(slug) ? "" : $"{PublicWebUrl}/host/{slug}";

    public string FieldUrl(string? slug, string bank, string branch, string groupId, string memberId)
    {
        if (string.IsNullOrEmpty(slug)) return "";
        return $"{PublicWebUrl}/join/{slug}?bank={Uri.EscapeDataString(bank)}&branch={Uri.EscapeDataString(branch)}&groupId={Uri.EscapeDataString(groupId)}&memberId={Uri.EscapeDataString(memberId)}";
    }

    public UserDto ToUser(User user) =>
        new(user.Id, user.Name, user.Email, user.Role, user.HostSlug, HostUrl(user.HostSlug));

    public MeetingListItemDto ToListItem(Meeting m)
    {
        var slug = m.CreditOfficer?.HostSlug;
        return new(
            m.Id,
            m.Code,
            m.Bank,
            m.Branch,
            m.GroupId,
            m.MemberId,
            m.MemberName,
            m.ScheduledAt,
            m.Status,
            m.CreditOfficer?.Name ?? "",
            m.CreditOfficerId,
            slug,
            HostUrl(slug),
            FieldUrl(slug, m.Bank, m.Branch, m.GroupId, m.MemberId),
            m.DurationSeconds,
            m.Recordings.OrderByDescending(r => r.CreatedAt).FirstOrDefault()?.Status,
            m.Recordings.Count);
    }

    public MeetingDetailDto ToDetail(Meeting m)
    {
        var slug = m.CreditOfficer?.HostSlug;
        return new(
            m.Id,
            m.Code,
            m.Bank,
            m.Branch,
            m.GroupId,
            m.MemberId,
            m.MemberName,
            m.ScheduledAt,
            m.Status,
            m.LiveKitRoomName,
            m.StartedAt,
            m.EndedAt,
            m.DurationSeconds,
            HostUrl(slug),
            FieldUrl(slug, m.Bank, m.Branch, m.GroupId, m.MemberId),
            ToUser(m.CreditOfficer!),
            m.Recordings.OrderBy(r => r.Sequence).Select(r => new RecordingDto(
                r.Id, r.Status, r.Sequence, r.Bank, r.Branch, r.GroupId, r.MemberId,
                r.FilePath, r.DurationSeconds, r.Error, r.CreatedAt, r.JoinedAt, r.LeftAt)).ToList(),
            m.Snapshots.OrderByDescending(s => s.CreatedAt).Select(s => new SnapshotDto(
                s.Id, s.Bank, s.Branch, s.GroupId, s.MemberId,
                $"/api/meetings/{m.Id}/snapshots/{s.Id}/file",
                s.CropX, s.CropY, s.CropWidth, s.CropHeight, s.CreatedAt,
                s.Latitude, s.Longitude, s.AccuracyMeters, s.GeoCapturedAt, s.GeoError)).ToList(),
            m.ChatMessages.OrderBy(c => c.SentAt).Select(c => new ChatMessageDto(
                c.Id, c.SenderUserId, c.SenderName, c.SenderRole, c.Body, c.SentAt)).ToList());
    }

    public static IQueryable<Meeting> WithGraph(IQueryable<Meeting> query) =>
        query
            .Include(m => m.CreditOfficer)
            .Include(m => m.Recordings)
            .Include(m => m.Snapshots)
            .Include(m => m.ChatMessages);
}

public class MeetingService(
    AppDbContext db,
    LiveKitService liveKit,
    NotificationService notifications,
    MeetingMapper mapper,
    IConfiguration config,
    ILogger<MeetingService> logger)
{
    public MeetingMapper Mapper => mapper;

    public static string NewCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[6];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }
        return new string(chars);
    }

    public async Task<Meeting> LoadAsync(Guid id) =>
        await MeetingMapper.WithGraph(db.Meetings).FirstAsync(m => m.Id == id);

    public async Task<User?> FindOfficerBySlugAsync(string slug) =>
        await db.Users.FirstOrDefaultAsync(u =>
            u.HostSlug == slug && u.Role == Roles.CreditOfficer);

    public async Task<Meeting?> FindCurrentVisitAsync(Guid creditOfficerId)
    {
        var open = await MeetingMapper.WithGraph(db.Meetings)
            .Where(m => m.CreditOfficerId == creditOfficerId &&
                        (m.Status == MeetingStatuses.WaitingForFieldOfficer ||
                         m.Status == MeetingStatuses.InProgress))
            .ToListAsync();
        if (open.Count > 0)
        {
            return open.OrderByDescending(m => m.StartedAt ?? m.ScheduledAt).First();
        }

        var scheduled = await MeetingMapper.WithGraph(db.Meetings)
            .Where(m => m.CreditOfficerId == creditOfficerId && m.Status == MeetingStatuses.Scheduled)
            .ToListAsync();
        return scheduled.OrderBy(m => m.ScheduledAt).FirstOrDefault();
    }

    public async Task<Meeting> EnsureDeskAsync(User officer)
    {
        var current = await FindCurrentVisitAsync(officer.Id);
        if (current != null)
        {
            return current;
        }

        string code;
        do
        {
            code = NewCode();
        } while (await db.Meetings.AnyAsync(m => m.Code == code));

        var id = Guid.NewGuid();
        var meeting = new Meeting
        {
            Id = id,
            Code = code,
            Bank = "",
            Branch = "",
            GroupId = "",
            MemberId = "",
            MemberName = officer.Name,
            ScheduledAt = DateTimeOffset.UtcNow,
            Status = MeetingStatuses.WaitingForFieldOfficer,
            CreditOfficerId = officer.Id,
            LiveKitRoomName = $"visit-{id:N}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        return await LoadAsync(meeting.Id);
    }

    public async Task<Meeting> ScheduleAsync(User actor, ScheduleMeetingRequest request)
    {
        User officer;
        if (actor.Role == Roles.Admin)
        {
            if (request.CreditOfficerId is null)
            {
                throw new InvalidOperationException("Admin must choose a credit officer.");
            }
            officer = await db.Users.FirstOrDefaultAsync(u =>
                           u.Id == request.CreditOfficerId && u.Role == Roles.CreditOfficer)
                       ?? throw new InvalidOperationException("Credit officer not found.");
        }
        else
        {
            officer = actor;
        }

        string code;
        do
        {
            code = NewCode();
        } while (await db.Meetings.AnyAsync(m => m.Code == code));

        var id = Guid.NewGuid();
        var meeting = new Meeting
        {
            Id = id,
            Code = code,
            Bank = (request.Bank ?? "").Trim(),
            Branch = (request.Branch ?? "").Trim(),
            GroupId = (request.GroupId ?? "").Trim(),
            MemberId = (request.MemberId ?? "").Trim(),
            MemberName = string.IsNullOrWhiteSpace(request.MemberName)
                ? (request.MemberId ?? "").Trim()
                : request.MemberName.Trim(),
            ScheduledAt = request.ScheduledAt,
            Status = MeetingStatuses.Scheduled,
            CreditOfficerId = officer.Id,
            LiveKitRoomName = $"visit-{id:N}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        return await LoadAsync(meeting.Id);
    }

    public async Task StampJoinParamsAsync(
        Meeting meeting,
        string? bank,
        string? branch,
        string? groupId,
        string? memberId)
    {
        meeting.Bank = (bank ?? "").Trim();
        meeting.Branch = (branch ?? "").Trim();
        meeting.GroupId = (groupId ?? "").Trim();
        meeting.MemberId = (memberId ?? "").Trim();
        await db.SaveChangesAsync();
    }

    public async Task MarkWaitingAsync(Meeting meeting)
    {
        if (meeting.Status == MeetingStatuses.Scheduled)
        {
            meeting.Status = MeetingStatuses.WaitingForFieldOfficer;
            await db.SaveChangesAsync();
        }
    }

    public async Task OnFieldOfficerJoinedAsync(Meeting meeting, string? identity = null)
    {
        if (meeting.Status is MeetingStatuses.Completed or MeetingStatuses.Cancelled)
        {
            return;
        }

        meeting.FieldOfficerCount += 1;
        var activeOfficer = await FindWaitingOfficerByIdentityAsync(meeting, identity);
        if (activeOfficer is not null && activeOfficer.Status == WaitingStatuses.Admitted)
        {
            activeOfficer.Status = WaitingStatuses.Connected;
            activeOfficer.ConnectedAt ??= DateTimeOffset.UtcNow;
        }
        if (meeting.Status != MeetingStatuses.InProgress)
        {
            meeting.Status = MeetingStatuses.InProgress;
            meeting.StartedAt ??= DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync();
        if (activeOfficer?.ConnectedAt is DateTimeOffset connectedAt)
        {
            await notifications.ActiveOfficerConnectedAsync(
                activeOfficer.HostSlug,
                activeOfficer.Id,
                connectedAt);
        }

        if (meeting.FieldOfficerCount != 1)
        {
            return;
        }

        var hasActive = meeting.Recordings.Any(r =>
            r.Status is RecordingStatuses.Starting or RecordingStatuses.Active);
        if (hasActive)
        {
            return;
        }

        var sequence = meeting.Recordings.Count == 0 ? 1 : meeting.Recordings.Max(r => r.Sequence) + 1;
        var (egressId, error) = await liveKit.StartRoomRecordingAsync(meeting, sequence);
        db.Recordings.Add(new Recording
        {
            Id = Guid.NewGuid(),
            MeetingId = meeting.Id,
            EgressId = egressId,
            Status = egressId is null ? RecordingStatuses.Unavailable : RecordingStatuses.Active,
            Bank = meeting.Bank,
            Branch = meeting.Branch,
            GroupId = meeting.GroupId,
            MemberId = meeting.MemberId,
            Sequence = sequence,
            FilePath = liveKit.RecordingRelativePath(meeting, sequence),
            Error = error,
            CreatedAt = DateTimeOffset.UtcNow,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        await notifications.FieldOfficerJoinedAsync(meeting);
    }

    public async Task OnFieldOfficerLeftAsync(Meeting meeting)
    {
        meeting.FieldOfficerCount = Math.Max(0, meeting.FieldOfficerCount - 1);
        await db.SaveChangesAsync();
        if (meeting.FieldOfficerCount > 0)
        {
            return;
        }

        foreach (var recording in meeting.Recordings.Where(r =>
                     (r.Status is RecordingStatuses.Active or RecordingStatuses.Starting or RecordingStatuses.Unavailable)
                     && r.LeftAt == null).ToList())
        {
            if (recording.EgressId != null)
            {
                await liveKit.StopRecordingAsync(recording.EgressId);
            }
            recording.LeftAt = DateTimeOffset.UtcNow;
            if (recording.Status is RecordingStatuses.Active or RecordingStatuses.Starting)
            {
                recording.Status = RecordingStatuses.Completed;
            }
            if (recording.JoinedAt != null)
            {
                recording.DurationSeconds = (int)Math.Max(0, (recording.LeftAt.Value - recording.JoinedAt.Value).TotalSeconds);
            }
        }
        await db.SaveChangesAsync();
    }

    private async Task<WaitingOfficer?> FindWaitingOfficerByIdentityAsync(Meeting meeting, string? identity)
    {
        if (identity?.StartsWith("fo-", StringComparison.Ordinal) == true &&
            Guid.TryParseExact(identity[3..], "N", out var waitingId))
        {
            return await db.WaitingOfficers.FirstOrDefaultAsync(w =>
                w.Id == waitingId && w.MeetingId == meeting.Id);
        }
        return await db.WaitingOfficers.FirstOrDefaultAsync(w =>
            w.MeetingId == meeting.Id &&
            (w.Status == WaitingStatuses.Admitted || w.Status == WaitingStatuses.Connected));
    }

    private async Task EndWaitingOfficerAsync(Meeting meeting, string? identity)
    {
        var waiting = await FindWaitingOfficerByIdentityAsync(meeting, identity);
        if (waiting is null) return;
        waiting.Status = WaitingStatuses.Left;
        waiting.LeftAt = DateTimeOffset.UtcNow;
        var started = waiting.ConnectedAt ?? waiting.AdmittedAt;
        waiting.CallDurationSeconds = started is null
            ? 0
            : (int)Math.Max(0, (waiting.LeftAt.Value - started.Value).TotalSeconds);
        await db.SaveChangesAsync();
        await notifications.ActiveOfficerEndedAsync(
            waiting.HostSlug,
            waiting.Id,
            waiting.CallDurationSeconds.Value);
    }

    public async Task CompleteAsync(Meeting meeting)
    {
        foreach (var recording in meeting.Recordings.Where(r => r.LeftAt == null).ToList())
        {
            if (recording.EgressId != null)
            {
                await liveKit.StopRecordingAsync(recording.EgressId);
            }
            recording.LeftAt ??= DateTimeOffset.UtcNow;
            if (recording.Status is RecordingStatuses.Active or RecordingStatuses.Starting)
            {
                recording.Status = RecordingStatuses.Completed;
            }
            if (recording.JoinedAt != null && recording.DurationSeconds is null)
            {
                recording.DurationSeconds = (int)Math.Max(0, (recording.LeftAt.Value - recording.JoinedAt.Value).TotalSeconds);
            }
        }

        meeting.EndedAt = DateTimeOffset.UtcNow;
        meeting.StartedAt ??= meeting.EndedAt;
        meeting.DurationSeconds = (int)Math.Max(0, (meeting.EndedAt.Value - meeting.StartedAt.Value).TotalSeconds);
        meeting.Status = MeetingStatuses.Completed;
        meeting.FieldOfficerCount = 0;
        var activeOfficers = await db.WaitingOfficers.Where(w =>
                w.MeetingId == meeting.Id &&
                (w.Status == WaitingStatuses.Admitted || w.Status == WaitingStatuses.Connected))
            .ToListAsync();
        foreach (var waiting in activeOfficers)
        {
            waiting.Status = WaitingStatuses.Left;
            waiting.LeftAt = DateTimeOffset.UtcNow;
            var started = waiting.ConnectedAt ?? waiting.AdmittedAt;
            waiting.CallDurationSeconds = started is null
                ? 0
                : (int)Math.Max(0, (waiting.LeftAt.Value - started.Value).TotalSeconds);
        }
        await db.SaveChangesAsync();
        foreach (var waiting in activeOfficers)
        {
            await notifications.ActiveOfficerEndedAsync(
                waiting.HostSlug,
                waiting.Id,
                waiting.CallDurationSeconds ?? 0);
        }
        await notifications.MeetingEndedAsync(meeting);
        await liveKit.DeleteRoomAsync(meeting.LiveKitRoomName);
    }

    public async Task HandleWebhookAsync(string eventName, string roomName, string? identity, string? egressId, int? durationSeconds, string? filePath)
    {
        var meeting = await MeetingMapper.WithGraph(db.Meetings)
            .FirstOrDefaultAsync(m => m.LiveKitRoomName == roomName);
        if (meeting is null && !string.IsNullOrEmpty(egressId))
        {
            meeting = await MeetingMapper.WithGraph(db.Meetings)
                .FirstOrDefaultAsync(m => m.Recordings.Any(r => r.EgressId == egressId));
        }
        if (meeting is null)
        {
            logger.LogInformation("Webhook {Event} for unknown room {Room}", eventName, roomName);
            return;
        }

        switch (eventName)
        {
            case "participant_joined":
                if (identity?.StartsWith("fo-", StringComparison.Ordinal) == true)
                {
                    await OnFieldOfficerJoinedAsync(meeting, identity);
                }
                break;
            case "participant_left":
                if (identity?.StartsWith("fo-", StringComparison.Ordinal) == true)
                {
                    await OnFieldOfficerLeftAsync(meeting);
                    await EndWaitingOfficerAsync(meeting, identity);
                }
                break;
            case "egress_ended":
            case "egress_complete":
            {
                var recording = meeting.Recordings.FirstOrDefault(r => r.EgressId == egressId)
                                ?? meeting.Recordings.OrderByDescending(r => r.CreatedAt).FirstOrDefault();
                if (recording != null)
                {
                    recording.Status = RecordingStatuses.Completed;
                    recording.LeftAt ??= DateTimeOffset.UtcNow;
                    if (durationSeconds is > 0)
                    {
                        recording.DurationSeconds = durationSeconds;
                    }
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        recording.FilePath = filePath;
                    }
                    await db.SaveChangesAsync();
                    await notifications.RecordingReadyAsync(meeting);
                }
                break;
            }
        }
    }

    public async Task<Snapshot> SaveSnapshotAsync(
        Meeting meeting,
        Guid capturedBy,
        IFormFile file,
        int? cropX, int? cropY, int? cropW, int? cropH)
    {
        var uploads = config["Storage:UploadsPath"] ?? "/workspace/data/uploads";
        var dir = Path.Combine(uploads, "snapshots", meeting.Id.ToString("N"));
        Directory.CreateDirectory(dir);
        var snapshotId = Guid.NewGuid();
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".png";
        var fileName = $"{snapshotId:N}{ext}";
        var path = Path.Combine(dir, fileName);
        await using (var stream = File.Create(path))
        {
            await file.CopyToAsync(stream);
        }

        var fieldOfficer = (await db.WaitingOfficers
            .Where(w =>
                w.MeetingId == meeting.Id &&
                (w.Status == WaitingStatuses.Admitted || w.Status == WaitingStatuses.Connected))
            .ToListAsync())
            .OrderByDescending(w => w.ConnectedAt ?? w.AdmittedAt)
            .FirstOrDefault();

        var snapshot = new Snapshot
        {
            Id = snapshotId,
            MeetingId = meeting.Id,
            Bank = meeting.Bank,
            Branch = meeting.Branch,
            GroupId = meeting.GroupId,
            MemberId = meeting.MemberId,
            FileName = fileName,
            ContentType = string.IsNullOrEmpty(file.ContentType) ? "image/png" : file.ContentType,
            CropX = cropX,
            CropY = cropY,
            CropWidth = cropW,
            CropHeight = cropH,
            CreatedAt = DateTimeOffset.UtcNow,
            CapturedByUserId = capturedBy,
            Latitude = fieldOfficer?.Latitude,
            Longitude = fieldOfficer?.Longitude,
            AccuracyMeters = fieldOfficer?.AccuracyMeters,
            GeoCapturedAt = fieldOfficer?.GeoCapturedAt,
            GeoError = fieldOfficer?.GeoError
        };
        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync();
        return snapshot;
    }

    public string SnapshotPath(Meeting meeting, Snapshot snapshot)
    {
        var uploads = config["Storage:UploadsPath"] ?? "/workspace/data/uploads";
        return Path.Combine(uploads, "snapshots", meeting.Id.ToString("N"), snapshot.FileName);
    }
}
