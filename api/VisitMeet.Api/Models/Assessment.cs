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
