using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Data;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Services.Assessment;

public class MemoService(AppDbContext db)
{
    public const string TranscriptNote =
        "No call transcript is stored yet. This run fills fields from the guided session, bureau and bank flags, and captured photos. A later transcript run fills only empty transcript fields and does not replace values a credit officer saved.";

    public IQueryable<OutputTemplate> Templates =>
        db.OutputTemplates.Include(t => t.Sections).Include(t => t.Fields);

    public async Task<OutputTemplate> LoadTemplateAsync(Guid id) =>
        await Templates.FirstOrDefaultAsync(t => t.Id == id)
        ?? throw new KeyNotFoundException("Output template was not found.");

    public async Task<OutputTemplate> UpdateTemplateAsync(Guid id, UpsertTemplateRequest request, Guid userId)
    {
        var template = await db.OutputTemplates.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException("Output template was not found.");
        template.Name = request.Name.Trim();
        template.MemoType = string.IsNullOrWhiteSpace(request.MemoType) ? template.MemoType : request.MemoType.Trim();
        if (request.LoanCategoryIds is not null)
            template.LoanCategoryIdsJson = AssessmentMapper.ToJson(request.LoanCategoryIds);
        if (request.FileCategories is not null)
            template.FileCategoriesJson = AssessmentMapper.ToJson(request.FileCategories);
        var status = string.IsNullOrWhiteSpace(request.Status) ? template.Status : request.Status.Trim();
        if (status == TemplateStatuses.Active)
        {
            var others = await db.OutputTemplates
                .Where(t => t.Code == template.Code && t.Id != template.Id && t.Status == TemplateStatuses.Active)
                .ToListAsync();
            foreach (var other in others)
                other.Status = TemplateStatuses.Archived;
        }
        template.Status = status;
        template.UpdatedByUserId = userId;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return await LoadTemplateAsync(id);
    }

    public async Task<OutputTemplate> CloneAsync(Guid id, CloneTemplateRequest request, Guid userId)
    {
        var source = await LoadTemplateAsync(id);
        var version = await db.OutputTemplates.Where(t => t.Code == source.Code).MaxAsync(t => (int?)t.Version) ?? source.Version;
        var now = DateTimeOffset.UtcNow;
        var copy = new OutputTemplate
        {
            Id = Guid.NewGuid(),
            Code = source.Code,
            Name = request.Name.Trim(),
            MemoType = source.MemoType,
            Version = version + 1,
            Status = TemplateStatuses.Draft,
            LoanCategoryIdsJson = request.LoanCategoryIds is null
                ? source.LoanCategoryIdsJson
                : AssessmentMapper.ToJson(request.LoanCategoryIds),
            FileCategoriesJson = source.FileCategoriesJson,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.OutputTemplates.Add(copy);
        var sectionMap = new Dictionary<Guid, Guid>();
        foreach (var section in source.Sections.OrderBy(s => s.SortOrder))
        {
            var sectionId = Guid.NewGuid();
            sectionMap[section.Id] = sectionId;
            db.OutputSections.Add(new OutputSection
            {
                Id = sectionId,
                TemplateId = copy.Id,
                Name = section.Name,
                Source = section.Source,
                SortOrder = section.SortOrder
            });
        }
        foreach (var field in source.Fields.OrderBy(f => f.Number))
        {
            db.OutputFields.Add(new OutputField
            {
                Id = Guid.NewGuid(),
                TemplateId = copy.Id,
                SectionId = sectionMap[field.SectionId],
                Number = field.Number,
                Label = field.Label,
                Question = field.Question,
                Datatype = field.Datatype,
                OptionsJson = field.OptionsJson,
                AllowMultiple = field.AllowMultiple,
                ConditionFieldNumber = field.ConditionFieldNumber,
                ConditionChoicesJson = field.ConditionChoicesJson,
                Source = field.Source,
                PhotoType = field.PhotoType,
                TaskNumber = field.TaskNumber,
                RuleLink = field.RuleLink,
                JsonShape = field.JsonShape,
                SortOrder = field.SortOrder
            });
        }
        await db.SaveChangesAsync();
        return await LoadTemplateAsync(copy.Id);
    }

    public async Task<OutputField> UpdateFieldAsync(Guid templateId, int number, UpdateTemplateFieldRequest request, Guid userId)
    {
        var template = await db.OutputTemplates.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new KeyNotFoundException("Output template was not found.");
        var field = template.Fields.FirstOrDefault(f => f.Number == number)
            ?? throw new KeyNotFoundException("Template field was not found.");
        var label = request.Label.Trim();
        if (label.Length == 0)
            throw new InvalidOperationException("Label is required. It is the API key.");
        if (template.Fields.Any(f => f.Id != field.Id && string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Another field already uses this label.");
        field.Label = label;
        field.Question = request.Question.Trim();
        if (!string.IsNullOrWhiteSpace(request.Datatype))
            field.Datatype = request.Datatype.Trim();
        if (request.Options is not null)
            field.OptionsJson = AssessmentMapper.ToJson(request.Options);
        if (request.AllowMultiple is bool multi)
            field.AllowMultiple = multi;
        field.ConditionFieldNumber = request.ConditionFieldNumber;
        if (request.ConditionChoices is not null)
            field.ConditionChoicesJson = AssessmentMapper.ToJson(request.ConditionChoices);
        if (!string.IsNullOrWhiteSpace(request.Source))
            field.Source = request.Source.Trim();
        field.PhotoType = string.IsNullOrWhiteSpace(request.PhotoType) ? field.PhotoType : request.PhotoType.Trim();
        template.UpdatedByUserId = userId;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return field;
    }

    public async Task<IReadOnlyList<AssessmentMemo>> ListForApplicationAsync(Guid applicationId) =>
        (await db.AssessmentMemos
            .Include(m => m.Template)
            .Include(m => m.Values)
            .Where(m => m.ApplicationId == applicationId)
            .ToListAsync())
            .OrderByDescending(m => m.CreatedAt)
            .ToList();

    public async Task<AssessmentMemo> EnsureAsync(Guid applicationId, Guid? sessionId, Guid userId)
    {
        var app = await db.AssessmentApplications.FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new KeyNotFoundException("Application was not found.");
        if (sessionId is Guid requested && !await db.EngagementSessions.AnyAsync(s => s.Id == requested && s.ApplicationId == applicationId))
            throw new InvalidOperationException("That session is not on this application.");

        var existing = await db.AssessmentMemos
            .Include(m => m.Template)
            .Include(m => m.Values)
            .FirstOrDefaultAsync(m => m.ApplicationId == applicationId && m.SessionId == sessionId);
        if (existing is not null)
            return existing;

        var templates = await db.OutputTemplates
            .Where(t => t.Status == TemplateStatuses.Active)
            .OrderByDescending(t => t.Version)
            .ToListAsync();
        var template = templates.FirstOrDefault(t => AssessmentMapper.Guids(t.LoanCategoryIdsJson).Contains(app.LoanCategoryId))
            ?? throw new InvalidOperationException("No active CAM template for this loan category.");

        var memo = new AssessmentMemo
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            SessionId = sessionId,
            TemplateId = template.Id,
            TemplateVersion = template.Version,
            Status = MemoStatuses.Pending,
            TranscriptStatus = "Missing",
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AssessmentMemos.Add(memo);
        await db.SaveChangesAsync();
        memo.Template = template;
        return memo;
    }

    public async Task<AssessmentMemo> LoadAsync(Guid id) =>
        await db.AssessmentMemos
            .AsSplitQuery()
            .Include(m => m.Application)
            .Include(m => m.Template)!.ThenInclude(t => t!.Sections)
            .Include(m => m.Template)!.ThenInclude(t => t!.Fields)
            .Include(m => m.Values)
            .FirstOrDefaultAsync(m => m.Id == id)
        ?? throw new KeyNotFoundException("Memo was not found.");

    public async Task<AssessmentMemo> ProcessAsync(Guid id)
    {
        var memo = await LoadAsync(id);
        var template = memo.Template ?? throw new InvalidOperationException("Memo has no template.");
        var fields = template.Fields.OrderBy(f => f.Number).ToList();
        var answers = memo.SessionId is Guid sessionId
            ? await db.SessionTaskAnswers.Where(a => a.SessionId == sessionId).ToListAsync()
            : [];
        var meetingId = memo.SessionId is Guid sid
            ? await db.EngagementSessions.Where(s => s.Id == sid).Select(s => s.MeetingId).FirstOrDefaultAsync()
            : null;
        var photos = meetingId is Guid mid
            ? (await db.Snapshots.Where(s => s.MeetingId == mid).ToListAsync()).OrderBy(s => s.CreatedAt).ToList()
            : [];
        var evidence = await LatestEvidenceAsync(memo.ApplicationId);
        var byNumber = memo.Values.GroupBy(v => v.FieldNumber).ToDictionary(g => g.Key, g => g.First());
        var now = DateTimeOffset.UtcNow;

        foreach (var field in fields)
        {
            byNumber.TryGetValue(field.Number, out var current);
            if (current?.Stage == MemoStages.Manual)
                continue;

            if (!IsApplicable(field, byNumber))
            {
                if (current is not null)
                {
                    db.MemoFieldValues.Remove(current);
                    byNumber.Remove(field.Number);
                }
                continue;
            }

            var extracted = Extract(field, answers, photos, evidence);
            if (extracted is null)
                continue;

            if (current is null)
            {
                current = new MemoFieldValue { Id = Guid.NewGuid(), MemoId = memo.Id, FieldNumber = field.Number };
                db.MemoFieldValues.Add(current);
                memo.Values.Add(current);
                byNumber[field.Number] = current;
            }
            current.Value = extracted.Value.Value;
            current.Stage = MemoStages.Extraction;
            current.SourceKind = extracted.Value.Kind;
            current.SourceRef = extracted.Value.Ref;
            current.SourceLabel = extracted.Value.Label;
            current.UpdatedAt = now;
        }

        memo.Status = MemoStatuses.Processed;
        memo.TranscriptStatus = "Missing";
        memo.ProcessedAt = now;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return await LoadAsync(id);
    }

    public async Task<AssessmentMemo> SaveManualAsync(Guid id, int number, string value)
    {
        var memo = await LoadAsync(id);
        if (memo.Status != MemoStatuses.Processed)
            throw new InvalidOperationException("Run extraction before editing CAM fields.");
        var field = memo.Template?.Fields.FirstOrDefault(f => f.Number == number)
            ?? throw new KeyNotFoundException("Template field was not found.");
        var text = value.Trim();
        if (text.Length == 0)
            throw new InvalidOperationException("Enter a value, or leave the extracted value in place.");
        var row = memo.Values.FirstOrDefault(v => v.FieldNumber == number);
        if (row is null)
        {
            row = new MemoFieldValue { Id = Guid.NewGuid(), MemoId = memo.Id, FieldNumber = field.Number };
            db.MemoFieldValues.Add(row);
        }
        row.Value = text;
        row.Stage = MemoStages.Manual;
        row.SourceKind = MemoValueSources.Manual;
        row.SourceRef = "credit-officer";
        row.SourceLabel = "Entered by the credit officer";
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return await LoadAsync(id);
    }

    public async Task<(string FileName, byte[] Body)> DownloadAsync(Guid id)
    {
        var memo = await LoadAsync(id);
        if (memo.Status != MemoStatuses.Processed)
            throw new InvalidOperationException("The memo can be downloaded only after it is processed.");
        var detail = ToDetail(memo);
        var json = JsonSerializer.Serialize(detail, new JsonSerializerOptions(AssessmentMapper.Json) { WriteIndented = true });
        var code = memo.Application?.Code ?? "cam";
        return ($"{code}-cam-v{memo.TemplateVersion}.json", Encoding.UTF8.GetBytes(json));
    }

    public MemoListItemDto ToListItem(AssessmentMemo memo)
    {
        var filled = memo.Values.Count(v => v.Stage == MemoStages.Manual || !string.IsNullOrWhiteSpace(v.Value));
        return new MemoListItemDto(
            memo.Id,
            memo.ApplicationId,
            memo.SessionId,
            memo.Template?.Name ?? "",
            memo.TemplateVersion,
            memo.Status,
            memo.TranscriptStatus,
            filled,
            memo.CreatedAt,
            memo.ProcessedAt);
    }

    public MemoDetailDto ToDetail(AssessmentMemo memo)
    {
        var template = memo.Template;
        var values = memo.Values.GroupBy(v => v.FieldNumber).ToDictionary(g => g.Key, g => g.First());
        var showFields = memo.Status == MemoStatuses.Processed && template is not null;
        var sections = showFields
            ? template!.Sections.OrderBy(s => s.SortOrder).Select(section =>
            {
                var fields = template.Fields.Where(f => f.SectionId == section.Id).OrderBy(f => f.Number).Select(field =>
                {
                    values.TryGetValue(field.Number, out var row);
                    var applicable = IsApplicable(field, values);
                    return new MemoFieldDto(
                        field.Number,
                        field.Label,
                        field.Question,
                        field.Datatype,
                        AssessmentMapper.Strings(field.OptionsJson),
                        field.AllowMultiple,
                        field.ConditionFieldNumber,
                        AssessmentMapper.Strings(field.ConditionChoicesJson),
                        field.Source,
                        field.PhotoType,
                        field.JsonShape,
                        applicable,
                        applicable ? row?.Value : null,
                        applicable ? row?.Stage : null,
                        applicable ? row?.SourceKind : null,
                        applicable ? row?.SourceRef : null,
                        applicable ? row?.SourceLabel : null);
                }).ToList();
                return new MemoSectionDto(section.Name, section.Source, section.SortOrder, fields);
            }).ToList()
            : [];
        var filled = showFields
            ? sections.SelectMany(s => s.Fields).Count(f => f.Applicable && !string.IsNullOrWhiteSpace(f.Value))
            : 0;
        return new MemoDetailDto(
            memo.Id,
            memo.ApplicationId,
            memo.Application?.Code ?? "",
            memo.Application?.DisplayName ?? "",
            memo.SessionId,
            memo.TemplateId,
            template?.Name ?? "",
            memo.TemplateVersion,
            memo.Status,
            memo.TranscriptStatus,
            memo.Status == MemoStatuses.Processed ? TranscriptNote : null,
            memo.CreatedAt,
            memo.ProcessedAt,
            template?.Fields.Count ?? 0,
            filled,
            sections);
    }

    private async Task<List<RuleRunResult>> LatestEvidenceAsync(Guid applicationId)
    {
        var applicants = await db.Applicants
            .Include(a => a.RuleRuns).ThenInclude(r => r.Results)
            .Where(a => a.ApplicationId == applicationId)
            .ToListAsync();
        return applicants
            .SelectMany(a => a.RuleRuns
                .GroupBy(r => r.Kind)
                .Select(g => g.OrderByDescending(r => r.RanAt).First())
                .SelectMany(r => r.Results))
            .ToList();
    }

    private static bool IsApplicable(OutputField field, IReadOnlyDictionary<int, MemoFieldValue> values)
    {
        if (field.ConditionFieldNumber is not int gate)
            return true;
        if (!values.TryGetValue(gate, out var row) || string.IsNullOrWhiteSpace(row.Value))
            return false;
        var expected = AssessmentMapper.Strings(field.ConditionChoicesJson);
        if (expected.Count == 0)
            return true;
        var selected = Selections(row.Value);
        return selected.Any(s => expected.Contains(s, StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> Selections(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(trimmed, AssessmentMapper.Json);
                if (parsed is { Count: > 0 })
                    return parsed;
            }
            catch (JsonException)
            {
                // Fall through to a single value or a comma-separated list.
            }
        }
        if (trimmed.Contains(','))
            return trimmed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        return [trimmed];
    }

    private readonly record struct Extracted(string Value, string Kind, string Ref, string Label);

    private static Extracted? Extract(
        OutputField field,
        IReadOnlyList<SessionTaskAnswer> answers,
        IReadOnlyList<Snapshot> photos,
        IReadOnlyList<RuleRunResult> evidence)
    {
        if (field.Datatype == FieldDatatypes.Json)
            return ExtractLoans(field, answers, evidence);

        if (!string.IsNullOrWhiteSpace(field.RuleLink))
            return ExtractRules(field.RuleLink, evidence);

        if (!string.IsNullOrWhiteSpace(field.PhotoType))
        {
            var matched = photos.Where(p => PhotoMatches(p.Category, field.PhotoType)).ToList();
            var yesNo = AssessmentMapper.Strings(field.OptionsJson).Any(o => o.Equals("Yes", StringComparison.OrdinalIgnoreCase));
            if (yesNo)
            {
                if (matched.Count == 0)
                    return null;
                return new Extracted("Yes", MemoValueSources.Photo, string.Join(",", matched.Select(p => p.Id)), PhotoLabel(matched));
            }
            if (field.ConditionFieldNumber is null && matched.Count > 0)
                return new Extracted(PhotoLabel(matched), MemoValueSources.Photo, string.Join(",", matched.Select(p => p.Id)), PhotoLabel(matched));
            return null;
        }

        if (field.TaskNumber is int task)
            return ExtractTask(field, task, answers);

        return null;
    }

    private static Extracted? ExtractLoans(
        OutputField field,
        IReadOnlyList<SessionTaskAnswer> answers,
        IReadOnlyList<RuleRunResult> evidence)
    {
        var rows = new List<Dictionary<string, string?>>();
        if (field.TaskNumber is int task)
        {
            var answer = answers.FirstOrDefault(a => a.TaskNumber == task);
            var stated = AnswerText(answer);
            if (!string.IsNullOrWhiteSpace(stated))
            {
                rows.Add(new Dictionary<string, string?>
                {
                    ["lender"] = null,
                    ["amount"] = null,
                    ["emi"] = null,
                    ["remainingTenure"] = null,
                    ["status"] = "Stated on the call",
                    ["source"] = stated
                });
            }
        }
        foreach (var result in evidence.Where(r => r.Fired && r.CheckKey == "credit-report.write-off-settled"))
        {
            foreach (var item in ReadEvidence(result.EvidenceJson).Where(i => i.Kind == "account"))
            {
                rows.Add(new Dictionary<string, string?>
                {
                    ["lender"] = Field(item, "institution"),
                    ["amount"] = Field(item, "amount"),
                    ["emi"] = null,
                    ["remainingTenure"] = null,
                    ["status"] = Field(item, "status"),
                    ["source"] = "bureau"
                });
            }
        }
        if (rows.Count == 0)
            return null;
        var json = JsonSerializer.Serialize(rows, AssessmentMapper.Json);
        var fromBureau = rows.Any(r => r.TryGetValue("source", out var source) && source == "bureau");
        var refs = evidence.Where(r => r.CheckKey == "credit-report.write-off-settled").Select(r => r.Id.ToString());
        var taskRef = field.TaskNumber is int taskNumber ? $"task:{taskNumber:000}" : "";
        return new Extracted(
            json,
            fromBureau ? MemoValueSources.RuleEvidence : MemoValueSources.SessionTask,
            fromBureau ? string.Join(",", refs) : taskRef,
            fromBureau ? "Stated loans and bureau write-offs" : "Stated on the call");
    }

    private static Extracted? ExtractRules(string ruleLink, IReadOnlyList<RuleRunResult> evidence)
    {
        var keys = ruleLink.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matched = evidence.Where(r => keys.Contains(r.CheckKey, StringComparer.Ordinal)).ToList();
        if (matched.Count == 0)
            return null;
        var fired = matched.Where(r => r.Fired).ToList();
        if (fired.Count == 0)
        {
            return new Extracted(
                "No flag on the latest run.",
                MemoValueSources.RuleEvidence,
                string.Join(",", matched.Select(r => r.Id)),
                string.Join(", ", keys));
        }
        var lines = fired.SelectMany(r => ReadEvidence(r.EvidenceJson).Select(i => i.Summary).Where(s => !string.IsNullOrWhiteSpace(s))).ToList();
        var text = lines.Count == 0 ? "Flag fired." : string.Join("\n", lines);
        return new Extracted(text, MemoValueSources.RuleEvidence, string.Join(",", fired.Select(r => r.Id)), string.Join(", ", fired.Select(r => r.CheckKey).Distinct()));
    }

    private static Extracted? ExtractTask(OutputField field, int task, IReadOnlyList<SessionTaskAnswer> answers)
    {
        var answer = answers.FirstOrDefault(a => a.TaskNumber == task);
        var text = AnswerText(answer);
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (field.AllowMultiple)
            text = AssessmentMapper.ToJson(AssessmentMapper.Strings(answer!.ChoicesJson));
        return new Extracted(text, MemoValueSources.SessionTask, $"task:{task:000}", $"Session task {task:000}");
    }

    private static string? AnswerText(SessionTaskAnswer? answer)
    {
        if (answer is null || answer.Status is TaskCoverage.Pending or TaskCoverage.NotApplicable)
            return null;
        var choices = AssessmentMapper.Strings(answer.ChoicesJson);
        var notes = answer.Notes?.Trim() ?? "";
        if (choices.Count == 0)
            return notes.Length == 0 ? null : notes;
        if (notes.Length == 0)
            return string.Join(", ", choices);
        return $"{string.Join(", ", choices)} — {notes}";
    }

    private static bool PhotoMatches(string category, string photoType)
    {
        var left = category.Trim();
        var right = photoType.Trim();
        if (right.Equals("house", StringComparison.OrdinalIgnoreCase))
            return left.StartsWith("House", StringComparison.OrdinalIgnoreCase);
        return left.Contains(right, StringComparison.OrdinalIgnoreCase);
    }

    private static string PhotoLabel(IReadOnlyList<Snapshot> photos) =>
        string.Join("; ", photos.Select(p => $"{p.Category} ({p.CreatedAt:yyyy-MM-dd HH:mm})"));

    private static List<RuleEvidenceItem> ReadEvidence(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<RuleEvidenceItem>>(json, AssessmentMapper.Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Field(RuleEvidenceItem item, string key) =>
        item.Fields.TryGetValue(key, out var value) ? value : null;
}
