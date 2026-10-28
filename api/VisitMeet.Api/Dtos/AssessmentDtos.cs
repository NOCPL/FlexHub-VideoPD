using System.Text.Json;

namespace VisitMeet.Api.Dtos;

public record LoanCategoryDto(Guid Id, string Code, string Name, string? Description);

public record UpsertLoanCategoryRequest(string Code, string Name, string? Description);

public record GuideListItemDto(
    Guid Id,
    string Code,
    string Name,
    string EventType,
    int Version,
    string Status,
    IReadOnlyList<Guid> LoanCategoryIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record GuideGroupDto(Guid Id, string Name, int SortOrder, int TaskCount);

public record GuideTaskDto(
    Guid Id,
    Guid GroupId,
    int Number,
    string Title,
    string Description,
    IReadOnlyList<string> Choices,
    bool AllowMultiple,
    int? ConditionTaskNumber,
    IReadOnlyList<string> ConditionChoices,
    string? RuleLink,
    int SortOrder);

public record GuideDetailDto(
    Guid Id,
    string Code,
    string Name,
    string EventType,
    int Version,
    string Status,
    IReadOnlyList<Guid> LoanCategoryIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<GuideGroupDto> Groups,
    IReadOnlyList<GuideTaskDto> Tasks);

public record UpsertGuideRequest(
    string Code,
    string Name,
    string EventType,
    IReadOnlyList<Guid>? LoanCategoryIds,
    string? Status);

public record SaveGuideGroupRequest(Guid? Id, string Name, int SortOrder);

public record SaveGuideTaskRequest(
    Guid? Id,
    Guid? GroupId,
    string GroupName,
    int Number,
    string Title,
    string Description,
    IReadOnlyList<string>? Choices,
    bool AllowMultiple,
    int? ConditionTaskNumber,
    IReadOnlyList<string>? ConditionChoices,
    string? RuleLink,
    int SortOrder);

public record SaveGuideContentRequest(
    IReadOnlyList<SaveGuideGroupRequest> Groups,
    IReadOnlyList<SaveGuideTaskRequest> Tasks);

public record CloneGuideRequest(string Name, IReadOnlyList<Guid>? LoanCategoryIds);

public record DeviationRuleDto(
    Guid Id,
    string CheckKey,
    string Name,
    string Description,
    string Severity,
    JsonElement Parameters,
    IReadOnlyList<Guid> LoanCategoryIds,
    bool Enabled,
    int? FeedsTaskNumber,
    DateTimeOffset UpdatedAt);

public record UpdateDeviationRuleRequest(
    string? Description,
    string? Severity,
    JsonElement? Parameters,
    IReadOnlyList<Guid>? LoanCategoryIds,
    bool? Enabled,
    int? FeedsTaskNumber);

public record CreateApplicationRequest(
    Guid LoanCategoryId,
    string DisplayName,
    string? Bank,
    string? Branch,
    string? GroupId,
    string? MemberId,
    decimal? LoanAmount,
    string? Pan,
    string? Phone,
    string? DateOfBirth);

public record AddApplicantRequest(
    string Role,
    string Name,
    string? Pan,
    string? Phone,
    string? DateOfBirth);

public record UploadApplicantInputRequest(
    string Kind,
    string? Provider,
    string? FileName,
    JsonElement Document);

public record RuleFlagDto(
    Guid Id,
    Guid RuleId,
    string CheckKey,
    string Name,
    string Severity,
    bool Fired,
    JsonElement Evidence,
    int? FeedsTaskNumber);

public record RuleRunDto(
    Guid Id,
    string Kind,
    DateTimeOffset RanAt,
    IReadOnlyList<RuleFlagDto> Results);

public record ApplicantInputDto(
    Guid Id,
    string Kind,
    string Provider,
    string FileName,
    DateTimeOffset UploadedAt);

public record ApplicantDto(
    Guid Id,
    string Role,
    string Name,
    string? Pan,
    string? Phone,
    string? DateOfBirth,
    IReadOnlyList<ApplicantInputDto> Inputs,
    IReadOnlyList<RuleRunDto> RuleRuns);

public record ApplicationListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    string LoanCategoryName,
    string Bank,
    string Branch,
    string GroupId,
    string MemberId,
    decimal? LoanAmount,
    int ApplicantCount,
    int FiredFlagCount,
    DateTimeOffset CreatedAt);

public record ApplicationDetailDto(
    Guid Id,
    string Code,
    string DisplayName,
    Guid LoanCategoryId,
    string LoanCategoryName,
    string Bank,
    string Branch,
    string GroupId,
    string MemberId,
    decimal? LoanAmount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ApplicantDto> Applicants);
