using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Data;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Services.Assessment;

public class AssessmentService(AppDbContext db, DeviationRuleEngine engine)
{
    public async Task<IReadOnlyList<LoanCategory>> CategoriesAsync() =>
        await db.LoanCategories.OrderBy(c => c.Name).ToListAsync();

    public async Task<LoanCategory> UpsertCategoryAsync(Guid? id, UpsertLoanCategoryRequest request)
    {
        var code = request.Code.Trim().ToLowerInvariant();
        var name = request.Name.Trim();
        if (code.Length < 2 || name.Length < 2)
        {
            throw new InvalidOperationException("Code and name are required.");
        }

        LoanCategory category;
        if (id is Guid existingId)
        {
            category = await db.LoanCategories.FirstOrDefaultAsync(c => c.Id == existingId)
                       ?? throw new KeyNotFoundException("Loan category not found.");
            if (await db.LoanCategories.AnyAsync(c => c.Code == code && c.Id != existingId))
            {
                throw new InvalidOperationException("A category with this code already exists.");
            }
        }
        else
        {
            if (await db.LoanCategories.AnyAsync(c => c.Code == code))
            {
                throw new InvalidOperationException("A category with this code already exists.");
            }
            category = new LoanCategory { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            db.LoanCategories.Add(category);
        }

        category.Code = code;
        category.Name = name;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await db.SaveChangesAsync();
        return category;
    }

    public IQueryable<EngagementGuide> Guides =>
        db.EngagementGuides.Include(g => g.Groups).Include(g => g.Tasks);

    public async Task<EngagementGuide> LoadGuideAsync(Guid id) =>
        await Guides.FirstOrDefaultAsync(g => g.Id == id)
        ?? throw new KeyNotFoundException("Guide not found.");

    public async Task<EngagementGuide> CreateGuideAsync(UpsertGuideRequest request, Guid userId)
    {
        var code = request.Code.Trim().ToLowerInvariant();
        if (code.Length < 2 || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Guide code and name are required.");
        }
        var version = 1;
        if (await db.EngagementGuides.AnyAsync(g => g.Code == code))
        {
            version = await db.EngagementGuides.Where(g => g.Code == code).MaxAsync(g => g.Version) + 1;
        }
        var guide = new EngagementGuide
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            EventType = string.IsNullOrWhiteSpace(request.EventType) ? "Interview" : request.EventType.Trim(),
            Version = version,
            Status = string.IsNullOrWhiteSpace(request.Status) ? GuideStatuses.Draft : request.Status.Trim(),
            LoanCategoryIdsJson = AssessmentMapper.ToJson(request.LoanCategoryIds),
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.EngagementGuides.Add(guide);
        await db.SaveChangesAsync();
        return await LoadGuideAsync(guide.Id);
    }

    public async Task<EngagementGuide> UpdateGuideAsync(Guid id, UpsertGuideRequest request, Guid userId)
    {
        var guide = await LoadGuideAsync(id);
        guide.Name = string.IsNullOrWhiteSpace(request.Name) ? guide.Name : request.Name.Trim();
        guide.EventType = string.IsNullOrWhiteSpace(request.EventType) ? guide.EventType : request.EventType.Trim();
        if (request.LoanCategoryIds is not null)
        {
            guide.LoanCategoryIdsJson = AssessmentMapper.ToJson(request.LoanCategoryIds);
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            guide.Status = request.Status.Trim();
        }
        guide.UpdatedByUserId = userId;
        guide.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return await LoadGuideAsync(id);
    }

    public async Task<EngagementGuide> SaveContentAsync(Guid id, SaveGuideContentRequest request, Guid userId)
    {
        var guide = await LoadGuideAsync(id);
        var groupIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var keepGroups = new HashSet<Guid>();
        var sort = 0;
        foreach (var incoming in request.Groups.OrderBy(g => g.SortOrder))
        {
            var name = incoming.Name.Trim();
            if (name.Length == 0) continue;
            GuideGroup group;
            if (incoming.Id is Guid gid && guide.Groups.FirstOrDefault(g => g.Id == gid) is { } existing)
            {
                group = existing;
            }
            else
            {
                group = new GuideGroup { Id = Guid.NewGuid(), GuideId = guide.Id };
                db.GuideGroups.Add(group);
                guide.Groups.Add(group);
            }
            group.Name = name;
            group.SortOrder = sort++;
            keepGroups.Add(group.Id);
            groupIds[name] = group.Id;
        }

        var keepTasks = new HashSet<Guid>();
        foreach (var incoming in request.Tasks.OrderBy(t => t.Number).ThenBy(t => t.SortOrder))
        {
            if (incoming.Number < 1 || string.IsNullOrWhiteSpace(incoming.Title)) continue;
            var groupName = incoming.GroupName.Trim();
            if (!groupIds.TryGetValue(groupName, out var groupId))
            {
                if (incoming.GroupId is Guid existingGroup && keepGroups.Contains(existingGroup))
                {
                    groupId = existingGroup;
                }
                else
                {
                    throw new InvalidOperationException($"Task {incoming.Number:000} needs a group.");
                }
            }

            GuideTask task;
            if (incoming.Id is Guid tid && guide.Tasks.FirstOrDefault(t => t.Id == tid) is { } existingTask)
            {
                task = existingTask;
            }
            else
            {
                task = new GuideTask { Id = Guid.NewGuid(), GuideId = guide.Id };
                db.GuideTasks.Add(task);
                guide.Tasks.Add(task);
            }
            task.GroupId = groupId;
            task.Number = incoming.Number;
            task.Title = incoming.Title.Trim();
            task.Description = (incoming.Description ?? "").Trim();
            task.ChoicesJson = AssessmentMapper.ToJson(incoming.Choices);
            task.AllowMultiple = incoming.AllowMultiple;
            task.ConditionTaskNumber = incoming.ConditionTaskNumber;
            task.ConditionChoicesJson = AssessmentMapper.ToJson(incoming.ConditionChoices);
            task.RuleLink = string.IsNullOrWhiteSpace(incoming.RuleLink) ? null : incoming.RuleLink.Trim();
            task.SortOrder = incoming.SortOrder;
            keepTasks.Add(task.Id);
        }

        foreach (var stale in guide.Tasks.Where(t => !keepTasks.Contains(t.Id)).ToList())
        {
            db.GuideTasks.Remove(stale);
        }

        foreach (var stale in guide.Groups.Where(g => !keepGroups.Contains(g.Id)).ToList())
        {
            db.GuideGroups.Remove(stale);
        }

        guide.UpdatedByUserId = userId;
        guide.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return await LoadGuideAsync(id);
    }

    public async Task<EngagementGuide> CloneAsync(Guid id, CloneGuideRequest request, Guid userId)
    {
        var source = await LoadGuideAsync(id);
        var code = source.Code;
        var version = await db.EngagementGuides.Where(g => g.Code == code).MaxAsync(g => g.Version) + 1;
        var clone = new EngagementGuide
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = string.IsNullOrWhiteSpace(request.Name) ? $"{source.Name} copy" : request.Name.Trim(),
            EventType = source.EventType,
            Version = version,
            Status = GuideStatuses.Draft,
            LoanCategoryIdsJson = request.LoanCategoryIds is null
                ? source.LoanCategoryIdsJson
                : AssessmentMapper.ToJson(request.LoanCategoryIds),
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.EngagementGuides.Add(clone);
        var groupMap = new Dictionary<Guid, Guid>();
        foreach (var group in source.Groups.OrderBy(g => g.SortOrder))
        {
            var copy = new GuideGroup
            {
                Id = Guid.NewGuid(),
                GuideId = clone.Id,
                Name = group.Name,
                SortOrder = group.SortOrder
            };
            db.GuideGroups.Add(copy);
            groupMap[group.Id] = copy.Id;
        }
        foreach (var task in source.Tasks.OrderBy(t => t.Number))
        {
            db.GuideTasks.Add(new GuideTask
            {
                Id = Guid.NewGuid(),
                GuideId = clone.Id,
                GroupId = groupMap[task.GroupId],
                Number = task.Number,
                Title = task.Title,
                Description = task.Description,
                ChoicesJson = task.ChoicesJson,
                AllowMultiple = task.AllowMultiple,
                ConditionTaskNumber = task.ConditionTaskNumber,
                ConditionChoicesJson = task.ConditionChoicesJson,
                RuleLink = task.RuleLink,
                SortOrder = task.SortOrder
            });
        }
        await db.SaveChangesAsync();
        return await LoadGuideAsync(clone.Id);
    }

    public async Task<DeviationRule> UpdateRuleAsync(Guid id, UpdateDeviationRuleRequest request, Guid userId)
    {
        var rule = await db.DeviationRules.FirstOrDefaultAsync(r => r.Id == id)
                   ?? throw new KeyNotFoundException("Rule not found.");
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            rule.Description = request.Description.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            rule.Severity = request.Severity.Trim();
        }
        if (request.Parameters is JsonElement parameters && parameters.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            rule.ParametersJson = parameters.GetRawText();
        }
        if (request.LoanCategoryIds is not null)
        {
            rule.LoanCategoryIdsJson = AssessmentMapper.ToJson(request.LoanCategoryIds);
        }
        if (request.Enabled is bool enabled)
        {
            rule.Enabled = enabled;
        }
        if (request.FeedsTaskNumber is not null)
        {
            rule.FeedsTaskNumber = request.FeedsTaskNumber;
        }
        rule.UpdatedByUserId = userId;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return rule;
    }

    public async Task<IReadOnlyList<AssessmentApplication>> ListApplicationsAsync()
    {
        var apps = await db.AssessmentApplications
            .Include(a => a.LoanCategory)
            .Include(a => a.Applicants).ThenInclude(p => p.RuleRuns).ThenInclude(r => r.Results)
            .ToListAsync();
        return apps.OrderByDescending(a => a.CreatedAt).ToList();
    }

    public async Task<AssessmentApplication> LoadApplicationAsync(Guid id) =>
        await db.AssessmentApplications
            .Include(a => a.LoanCategory)
            .Include(a => a.Applicants).ThenInclude(p => p.Inputs)
            .Include(a => a.Applicants).ThenInclude(p => p.RuleRuns).ThenInclude(r => r.Results)
            .FirstOrDefaultAsync(a => a.Id == id)
        ?? throw new KeyNotFoundException("Application not found.");

    public async Task<AssessmentApplication> CreateApplicationAsync(CreateApplicationRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new InvalidOperationException("Applicant or household name is required.");
        }
        if (!await db.LoanCategories.AnyAsync(c => c.Id == request.LoanCategoryId))
        {
            throw new InvalidOperationException("Unknown loan category.");
        }
        string code;
        do
        {
            code = MeetingService.NewCode();
        } while (await db.AssessmentApplications.AnyAsync(a => a.Code == code));

        var app = new AssessmentApplication
        {
            Id = Guid.NewGuid(),
            Code = code,
            LoanCategoryId = request.LoanCategoryId,
            Bank = (request.Bank ?? "").Trim(),
            Branch = (request.Branch ?? "").Trim(),
            GroupId = (request.GroupId ?? "").Trim(),
            MemberId = (request.MemberId ?? "").Trim(),
            DisplayName = request.DisplayName.Trim(),
            LoanAmount = request.LoanAmount,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AssessmentApplications.Add(app);
        db.Applicants.Add(new Applicant
        {
            Id = Guid.NewGuid(),
            ApplicationId = app.Id,
            Role = ApplicantRoles.Primary,
            Name = request.DisplayName.Trim(),
            Pan = Trim(request.Pan),
            Phone = Trim(request.Phone),
            DateOfBirth = Trim(request.DateOfBirth)
        });
        await db.SaveChangesAsync();
        return await LoadApplicationAsync(app.Id);
    }

    public async Task<Applicant> AddApplicantAsync(Guid applicationId, AddApplicantRequest request)
    {
        var app = await LoadApplicationAsync(applicationId);
        var role = request.Role == ApplicantRoles.CoApplicant ? ApplicantRoles.CoApplicant : ApplicantRoles.Primary;
        if (role == ApplicantRoles.Primary && app.Applicants.Any(a => a.Role == ApplicantRoles.Primary))
        {
            throw new InvalidOperationException("This application already has a primary applicant. Add a co-applicant.");
        }
        var applicant = new Applicant
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            Role = role,
            Name = request.Name.Trim(),
            Pan = Trim(request.Pan),
            Phone = Trim(request.Phone),
            DateOfBirth = Trim(request.DateOfBirth)
        };
        if (applicant.Name.Length < 2)
        {
            throw new InvalidOperationException("Applicant name is required.");
        }
        db.Applicants.Add(applicant);
        await db.SaveChangesAsync();
        return applicant;
    }

    public async Task<Applicant> UploadInputAsync(Guid applicantId, UploadApplicantInputRequest request, Guid userId)
    {
        var applicant = await db.Applicants
                            .Include(a => a.Application)
                            .Include(a => a.Inputs)
                            .Include(a => a.RuleRuns).ThenInclude(r => r.Results)
                            .FirstOrDefaultAsync(a => a.Id == applicantId)
                        ?? throw new KeyNotFoundException("Applicant not found.");

        var kind = request.Kind == InputKinds.BankStatement ? InputKinds.BankStatement : InputKinds.Bureau;
        var provider = InputAdapters.DetectProvider(request.Document, kind, request.Provider);
        var input = new ApplicantInput
        {
            Id = Guid.NewGuid(),
            ApplicantId = applicant.Id,
            Kind = kind,
            Provider = provider,
            FileName = string.IsNullOrWhiteSpace(request.FileName) ? $"{kind.ToLowerInvariant()}.json" : request.FileName.Trim(),
            RawJson = request.Document.GetRawText(),
            UploadedByUserId = userId,
            UploadedAt = DateTimeOffset.UtcNow
        };
        db.ApplicantInputs.Add(input);

        var rules = await ApplicableRulesAsync(applicant.Application!.LoanCategoryId);
        var run = new RuleRun
        {
            Id = Guid.NewGuid(),
            ApplicantId = applicant.Id,
            InputId = input.Id,
            Kind = kind,
            RanAt = DateTimeOffset.UtcNow
        };
        db.RuleRuns.Add(run);

        IReadOnlyList<(DeviationRule Rule, bool Fired, List<RuleEvidenceItem> Evidence)> evaluated =
            kind == InputKinds.Bureau
                ? engine.EvaluateBureau(rules, InputAdapters.ToBureau(request.Document), applicant)
                : engine.EvaluateBank(rules, InputAdapters.ToBank(request.Document), applicant.Application.LoanAmount);

        foreach (var (rule, fired, evidence) in evaluated)
        {
            db.RuleRunResults.Add(new RuleRunResult
            {
                Id = Guid.NewGuid(),
                RuleRunId = run.Id,
                RuleId = rule.Id,
                CheckKey = rule.CheckKey,
                Name = rule.Name,
                Severity = rule.Severity,
                Fired = fired,
                EvidenceJson = JsonSerializer.Serialize(evidence, AssessmentMapper.Json),
                FeedsTaskNumber = rule.FeedsTaskNumber
            });
        }

        await db.SaveChangesAsync();
        return (await LoadApplicationAsync(applicant.ApplicationId)).Applicants.First(a => a.Id == applicant.Id);
    }

    private async Task<List<DeviationRule>> ApplicableRulesAsync(Guid loanCategoryId)
    {
        var rules = await db.DeviationRules.ToListAsync();
        return rules.Where(r =>
        {
            var ids = AssessmentMapper.Guids(r.LoanCategoryIdsJson);
            return ids.Count == 0 || ids.Contains(loanCategoryId);
        }).ToList();
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
