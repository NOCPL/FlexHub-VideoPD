namespace VisitMeet.Api.Models;

public static class GuideStatuses
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Archived = "Archived";
}

public static class ApplicantRoles
{
    public const string Primary = "Primary";
    public const string CoApplicant = "CoApplicant";
}

public static class InputKinds
{
    public const string Bureau = "Bureau";
    public const string BankStatement = "BankStatement";
}

public static class RuleSeverities
{
    public const string High = "High";
    public const string Medium = "Medium";
}

public class LoanCategory
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class EngagementGuide
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string EventType { get; set; } = "Interview";
    public int Version { get; set; } = 1;
    public string Status { get; set; } = GuideStatuses.Draft;
    public string LoanCategoryIdsJson { get; set; } = "[]";
    public Guid CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<GuideGroup> Groups { get; set; } = new List<GuideGroup>();
    public ICollection<GuideTask> Tasks { get; set; } = new List<GuideTask>();
}

public class GuideGroup
{
    public Guid Id { get; set; }
    public Guid GuideId { get; set; }
    public EngagementGuide? Guide { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public ICollection<GuideTask> Tasks { get; set; } = new List<GuideTask>();
}

public class GuideTask
{
    public Guid Id { get; set; }
    public Guid GuideId { get; set; }
    public EngagementGuide? Guide { get; set; }
    public Guid GroupId { get; set; }
    public GuideGroup? Group { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ChoicesJson { get; set; } = "[]";
    public bool AllowMultiple { get; set; }
    public int? ConditionTaskNumber { get; set; }
    public string ConditionChoicesJson { get; set; } = "[]";
    public string? RuleLink { get; set; }
    public int SortOrder { get; set; }
}

public class DeviationRule
{
    public Guid Id { get; set; }
    public string CheckKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Severity { get; set; } = RuleSeverities.Medium;
    public string ParametersJson { get; set; } = "{}";
    public string LoanCategoryIdsJson { get; set; } = "[]";
    public bool Enabled { get; set; } = true;
    public int? FeedsTaskNumber { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class AssessmentApplication
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public Guid LoanCategoryId { get; set; }
    public LoanCategory? LoanCategory { get; set; }
    public string Bank { get; set; } = "";
    public string Branch { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string MemberId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public decimal? LoanAmount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<Applicant> Applicants { get; set; } = new List<Applicant>();
}

public class Applicant
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public AssessmentApplication? Application { get; set; }
    public string Role { get; set; } = ApplicantRoles.Primary;
    public string Name { get; set; } = "";
    public string? Pan { get; set; }
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
    public ICollection<ApplicantInput> Inputs { get; set; } = new List<ApplicantInput>();
    public ICollection<RuleRun> RuleRuns { get; set; } = new List<RuleRun>();
}

public class ApplicantInput
{
    public Guid Id { get; set; }
    public Guid ApplicantId { get; set; }
    public Applicant? Applicant { get; set; }
    public string Kind { get; set; } = "";
    public string Provider { get; set; } = "";
    public string FileName { get; set; } = "";
    public string RawJson { get; set; } = "";
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public class RuleRun
{
    public Guid Id { get; set; }
    public Guid ApplicantId { get; set; }
    public Applicant? Applicant { get; set; }
    public Guid? InputId { get; set; }
    public string Kind { get; set; } = "";
    public DateTimeOffset RanAt { get; set; }
    public ICollection<RuleRunResult> Results { get; set; } = new List<RuleRunResult>();
}

public class RuleRunResult
{
    public Guid Id { get; set; }
    public Guid RuleRunId { get; set; }
    public RuleRun? RuleRun { get; set; }
    public Guid RuleId { get; set; }
    public string CheckKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Severity { get; set; } = "";
    public bool Fired { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public int? FeedsTaskNumber { get; set; }
}

public static class SessionStatuses
{
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
}

public static class SessionModes
{
    public const string Live = "Live";
    public const string Review = "Review";
}

public static class TaskCoverage
{
    public const string Pending = "Pending";
    public const string Covered = "Covered";
    public const string NotCovered = "NotCovered";
    public const string NotApplicable = "NotApplicable";
}

public class EngagementSession
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public AssessmentApplication? Application { get; set; }
    public Guid? MeetingId { get; set; }
    public Guid GuideId { get; set; }
    public EngagementGuide? Guide { get; set; }
    public int GuideVersion { get; set; }
    public string Mode { get; set; } = SessionModes.Live;
    public string Status { get; set; } = SessionStatuses.InProgress;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<SessionTaskAnswer> Answers { get; set; } = new List<SessionTaskAnswer>();
}

public class SessionTaskAnswer
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public EngagementSession? Session { get; set; }
    public int TaskNumber { get; set; }
    public string Status { get; set; } = TaskCoverage.Pending;
    public string ChoicesJson { get; set; } = "[]";
    public string Notes { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class TemplateStatuses
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Archived = "Archived";
}

public static class FieldDatatypes
{
    public const string String = "String";
    public const string Number = "Number";
    public const string Json = "Json";
    public const string Options = "Options";
}

public static class FieldSources
{
    public const string Transcript = "Transcript";
    public const string Photos = "Photos";
    public const string Documents = "Documents";
}

public static class MemoStatuses
{
    public const string Pending = "Pending";
    public const string Processed = "Processed";
}

public static class MemoStages
{
    public const string Extraction = "extraction";
    public const string Manual = "manual";
}

public static class MemoValueSources
{
    public const string SessionTask = "SessionTask";
    public const string RuleEvidence = "RuleEvidence";
    public const string Photo = "Photo";
    public const string Transcript = "Transcript";
    public const string Manual = "Manual";
}

public class OutputTemplate
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string MemoType { get; set; } = "Credit Assessment Memo";
    public int Version { get; set; } = 1;
    public string Status { get; set; } = TemplateStatuses.Draft;
    public string LoanCategoryIdsJson { get; set; } = "[]";
    public string FileCategoriesJson { get; set; } = "[]";
    public Guid CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<OutputSection> Sections { get; set; } = new List<OutputSection>();
    public ICollection<OutputField> Fields { get; set; } = new List<OutputField>();
}

public class OutputSection
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public OutputTemplate? Template { get; set; }
    public string Name { get; set; } = "";
    public string Source { get; set; } = FieldSources.Transcript;
    public int SortOrder { get; set; }
}

public class OutputField
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public OutputTemplate? Template { get; set; }
    public Guid SectionId { get; set; }
    public OutputSection? Section { get; set; }
    public int Number { get; set; }
    public string Label { get; set; } = "";
    public string Question { get; set; } = "";
    public string Datatype { get; set; } = FieldDatatypes.String;
    public string OptionsJson { get; set; } = "[]";
    public bool AllowMultiple { get; set; }
    public int? ConditionFieldNumber { get; set; }
    public string ConditionChoicesJson { get; set; } = "[]";
    public string Source { get; set; } = FieldSources.Transcript;
    public string? PhotoType { get; set; }
    public int? TaskNumber { get; set; }
    public string? RuleLink { get; set; }
    public string? JsonShape { get; set; }
    public int SortOrder { get; set; }
}

public class AssessmentMemo
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public AssessmentApplication? Application { get; set; }
    public Guid? SessionId { get; set; }
    public EngagementSession? Session { get; set; }
    public Guid TemplateId { get; set; }
    public OutputTemplate? Template { get; set; }
    public int TemplateVersion { get; set; }
    public string Status { get; set; } = MemoStatuses.Pending;
    public string TranscriptStatus { get; set; } = "Missing";
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public ICollection<MemoFieldValue> Values { get; set; } = new List<MemoFieldValue>();
}

public class MemoFieldValue
{
    public Guid Id { get; set; }
    public Guid MemoId { get; set; }
    public AssessmentMemo? Memo { get; set; }
    public int FieldNumber { get; set; }
    public string Value { get; set; } = "";
    public string Stage { get; set; } = MemoStages.Extraction;
    public string SourceKind { get; set; } = "";
    public string SourceRef { get; set; } = "";
    public string SourceLabel { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
