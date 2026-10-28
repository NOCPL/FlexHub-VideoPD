using System.Text.Json;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Services.Assessment;

public static class AssessmentMapper
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static List<string> Strings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static List<Guid> Guids(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string ToJson(IEnumerable<string>? values) =>
        JsonSerializer.Serialize(values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct() ?? [], Json);

    public static string ToJson(IEnumerable<Guid>? values) =>
        JsonSerializer.Serialize(values?.Distinct() ?? [], Json);

    public static JsonElement ParseObject(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }

    public static JsonElement ParseArray(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>("[]");
        }
    }

    public static LoanCategoryDto ToDto(LoanCategory c) => new(c.Id, c.Code, c.Name, c.Description);

    public static GuideListItemDto ToListItem(EngagementGuide g) =>
        new(g.Id, g.Code, g.Name, g.EventType, g.Version, g.Status, Guids(g.LoanCategoryIdsJson), g.CreatedAt, g.UpdatedAt);

    public static GuideDetailDto ToDetail(EngagementGuide g)
    {
        var groups = g.Groups.OrderBy(x => x.SortOrder).Select(x =>
            new GuideGroupDto(x.Id, x.Name, x.SortOrder, g.Tasks.Count(t => t.GroupId == x.Id))).ToList();
        var tasks = g.Tasks.OrderBy(x => x.Number).ThenBy(x => x.SortOrder).Select(ToDto).ToList();
        return new GuideDetailDto(
            g.Id, g.Code, g.Name, g.EventType, g.Version, g.Status, Guids(g.LoanCategoryIdsJson),
            g.CreatedAt, g.UpdatedAt, groups, tasks);
    }

    public static GuideTaskDto ToDto(GuideTask t) =>
        new(t.Id, t.GroupId, t.Number, t.Title, t.Description, Strings(t.ChoicesJson), t.AllowMultiple,
            t.ConditionTaskNumber, Strings(t.ConditionChoicesJson), t.RuleLink, t.SortOrder);

    public static DeviationRuleDto ToDto(DeviationRule r) =>
        new(r.Id, r.CheckKey, r.Name, r.Description, r.Severity, ParseObject(r.ParametersJson),
            Guids(r.LoanCategoryIdsJson), r.Enabled, r.FeedsTaskNumber, r.UpdatedAt);

    public static ApplicantDto ToDto(Applicant a) =>
        new(
            a.Id, a.Role, a.Name, a.Pan, a.Phone, a.DateOfBirth,
            a.Inputs.OrderByDescending(i => i.UploadedAt).Select(i =>
                new ApplicantInputDto(i.Id, i.Kind, i.Provider, i.FileName, i.UploadedAt)).ToList(),
            a.RuleRuns.OrderByDescending(r => r.RanAt).Select(ToDto).ToList());

    public static RuleRunDto ToDto(RuleRun run) =>
        new(run.Id, run.Kind, run.RanAt, run.Results.Select(r =>
            new RuleFlagDto(r.Id, r.RuleId, r.CheckKey, r.Name, r.Severity, r.Fired, ParseArray(r.EvidenceJson), r.FeedsTaskNumber)).ToList());

    public static int LatestFiredFlagCount(AssessmentApplication app) =>
        app.Applicants.Sum(a =>
            a.RuleRuns
                .GroupBy(r => r.Kind)
                .Select(g => g.OrderByDescending(r => r.RanAt).First())
                .SelectMany(r => r.Results)
                .Count(x => x.Fired));

    public static ApplicationListItemDto ToListItem(AssessmentApplication app) =>
        new(
            app.Id, app.Code, app.DisplayName, app.LoanCategory?.Name ?? "",
            app.Bank, app.Branch, app.GroupId, app.MemberId, app.LoanAmount,
            app.Applicants.Count,
            LatestFiredFlagCount(app),
            app.CreatedAt);

    public static ApplicationDetailDto ToDetail(AssessmentApplication app, IReadOnlyList<SessionListItemDto>? sessions = null) =>
        new(
            app.Id, app.Code, app.DisplayName, app.LoanCategoryId, app.LoanCategory?.Name ?? "",
            app.Bank, app.Branch, app.GroupId, app.MemberId, app.LoanAmount, app.CreatedAt,
            app.Applicants.OrderBy(a => a.Role == ApplicantRoles.Primary ? 0 : 1).ThenBy(a => a.Name).Select(ToDto).ToList(),
            sessions ?? []);
}
