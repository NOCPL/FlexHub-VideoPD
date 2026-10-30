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
    IReadOnlyList<ApplicantDto> Applicants,
    IReadOnlyList<SessionListItemDto> Sessions);

public record EnsureSessionRequest(
    Guid? ApplicationId,
    Guid? MeetingId,
    string? Bank,
    string? Branch,
    string? GroupId,
    string? MemberId);

public record SaveTaskAnswerRequest(
    string? Status,
    IReadOnlyList<string>? Choices,
    string? Notes);

public record SessionListItemDto(
    Guid Id,
    string Status,
    string Mode,
    string GuideName,
    int GuideVersion,
    int CoveredCount,
    int VisibleCount,
    string? MeetingCode,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public record SessionProbeDto(
    Guid FlagId,
    string CheckKey,
    string Name,
    string Severity,
    JsonElement Evidence,
    bool NeedsExplanation);

public record SessionTaskViewDto(
    int Number,
    string Title,
    string Description,
    string GroupName,
    IReadOnlyList<string> Choices,
    bool AllowMultiple,
    int? ConditionTaskNumber,
    IReadOnlyList<string> ConditionChoices,
    string? RuleLink,
    string Coverage,
    IReadOnlyList<string> SelectedChoices,
    string Notes,
    IReadOnlyList<SessionProbeDto> Probes);

public record SessionGroupViewDto(
    string Name,
    int SortOrder,
    int CoveredCount,
    int VisibleCount,
    IReadOnlyList<SessionTaskViewDto> Tasks);

public record TemplateListItemDto(
    Guid Id,
    string Code,
    string Name,
    string MemoType,
    int Version,
    string Status,
    IReadOnlyList<Guid> LoanCategoryIds,
    IReadOnlyList<string> FileCategories,
    int SectionCount,
    int FieldCount,
    DateTimeOffset UpdatedAt);

public record TemplateSectionDto(Guid Id, string Name, string Source, int SortOrder, int FieldCount);

public record TemplateFieldDto(
    Guid Id,
    Guid SectionId,
    int Number,
    string Label,
    string Question,
    string Datatype,
    IReadOnlyList<string> Options,
    bool AllowMultiple,
    int? ConditionFieldNumber,
    IReadOnlyList<string> ConditionChoices,
    string Source,
    string? PhotoType,
    int? TaskNumber,
    string? RuleLink,
    string? JsonShape,
    int SortOrder);

public record TemplateDetailDto(
    Guid Id,
    string Code,
    string Name,
    string MemoType,
    int Version,
    string Status,
    IReadOnlyList<Guid> LoanCategoryIds,
    IReadOnlyList<string> FileCategories,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TemplateSectionDto> Sections,
    IReadOnlyList<TemplateFieldDto> Fields);

public record UpsertTemplateRequest(
    string Name,
    string? MemoType,
    string? Status,
    IReadOnlyList<Guid>? LoanCategoryIds,
    IReadOnlyList<string>? FileCategories);

public record CloneTemplateRequest(string Name, IReadOnlyList<Guid>? LoanCategoryIds);

public record UpdateTemplateFieldRequest(
    string Label,
    string Question,
    string? Datatype,
    IReadOnlyList<string>? Options,
    bool? AllowMultiple,
    int? ConditionFieldNumber,
    IReadOnlyList<string>? ConditionChoices,
    string? Source,
    string? PhotoType);

public record CreateMemoRequest(Guid? SessionId);

public record SaveMemoFieldRequest(string Value);

public record MemoListItemDto(
    Guid Id,
    Guid ApplicationId,
    Guid? SessionId,
    string TemplateName,
    int TemplateVersion,
    string Status,
    string TranscriptStatus,
    int FilledCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);

public record MemoFieldDto(
    int Number,
    string Label,
    string Question,
    string Datatype,
    IReadOnlyList<string> Options,
    bool AllowMultiple,
    int? ConditionFieldNumber,
    IReadOnlyList<string> ConditionChoices,
    string Source,
    string? PhotoType,
    string? JsonShape,
    bool Applicable,
    string? Value,
    string? Stage,
    string? SourceKind,
    string? SourceRef,
    string? SourceLabel);

public record MemoSectionDto(
    string Name,
    string Source,
    int SortOrder,
    IReadOnlyList<MemoFieldDto> Fields);

public record MemoDetailDto(
    Guid Id,
    Guid ApplicationId,
    string ApplicationCode,
    string DisplayName,
    Guid? SessionId,
    Guid TemplateId,
    string TemplateName,
    int TemplateVersion,
    string Status,
    string TranscriptStatus,
    string? TranscriptNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt,
    int FieldCount,
    int FilledCount,
    IReadOnlyList<MemoSectionDto> Sections);

public record SessionDetailDto(
    Guid Id,
    string Status,
    string Mode,
    string EventType,
    int GuideVersion,
    string GuideName,
    Guid ApplicationId,
    string ApplicationCode,
    string DisplayName,
    string LoanCategoryName,
    decimal? LoanAmount,
    Guid? MeetingId,
    string? MeetingCode,
    string Bank,
    string Branch,
    string GroupId,
    string MemberId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ScheduledAt,
    string? AssigneeName,
    int? DurationSeconds,
    IReadOnlyList<string> Blockers,
    int CoveredCount,
    int VisibleCount,
    IReadOnlyList<SessionGroupViewDto> Groups,
    IReadOnlyList<SnapshotDto> Photos,
    IReadOnlyList<RecordingDto> Recordings);
