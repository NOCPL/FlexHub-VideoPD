namespace VisitMeet.Api.Models;

public static class Roles
{
    public const string CreditOfficer = "CreditOfficer";
    public const string Admin = "Admin";
    public const string FieldGuest = "FieldGuest";
}

public static class MeetingStatuses
{
    public const string Scheduled = "Scheduled";
    public const string WaitingForFieldOfficer = "WaitingForFieldOfficer";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class RecordingStatuses
{
    public const string Starting = "Starting";
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Unavailable = "Unavailable";
}

public static class WaitingStatuses
{
    public const string Waiting = "Waiting";
    public const string Admitted = "Admitted";
    public const string Connected = "Connected";
    public const string Left = "Left";
    public const string Denied = "Denied";
}

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
    public string? HostSlug { get; set; }
}

public class Meeting
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Bank { get; set; } = "";
    public string Branch { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string MemberId { get; set; } = "";
    public string MemberName { get; set; } = "";
    public DateTimeOffset ScheduledAt { get; set; }
    public string Status { get; set; } = MeetingStatuses.Scheduled;
    public Guid CreditOfficerId { get; set; }
    public User? CreditOfficer { get; set; }
    public string LiveKitRoomName { get; set; } = "";
    public int FieldOfficerCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public ICollection<Recording> Recordings { get; set; } = new List<Recording>();
    public ICollection<Snapshot> Snapshots { get; set; } = new List<Snapshot>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}

public class Recording
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
    public string? EgressId { get; set; }
    public string Status { get; set; } = RecordingStatuses.Starting;
    public string Bank { get; set; } = "";
    public string Branch { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string MemberId { get; set; } = "";
    public int Sequence { get; set; }
    public string? FilePath { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
}

public class Snapshot
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
    public string Bank { get; set; } = "";
    public string Branch { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string MemberId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "image/png";
    public int? CropX { get; set; }
    public int? CropY { get; set; }
    public int? CropWidth { get; set; }
    public int? CropHeight { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CapturedByUserId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public DateTimeOffset? GeoCapturedAt { get; set; }
    public string? GeoError { get; set; }
}

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
    public Guid SenderUserId { get; set; }
    public string SenderName { get; set; } = "";
    public string SenderRole { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}

public class WaitingOfficer
{
    public Guid Id { get; set; }
    public Guid CreditOfficerId { get; set; }
    public User? CreditOfficer { get; set; }
    public string HostSlug { get; set; } = "";
    public Guid? MeetingId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Bank { get; set; } = "";
    public string Branch { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string MemberId { get; set; } = "";
    public string Status { get; set; } = WaitingStatuses.Waiting;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AdmittedAt { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
    public int? CallDurationSeconds { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public DateTimeOffset? GeoCapturedAt { get; set; }
    public string? GeoError { get; set; }
    public ICollection<LobbyMessage> Messages { get; set; } = new List<LobbyMessage>();
}

public class LobbyMessage
{
    public Guid Id { get; set; }
    public string HostSlug { get; set; } = "";
    public Guid? RecipientWaitingOfficerId { get; set; }
    public WaitingOfficer? RecipientWaitingOfficer { get; set; }
    public Guid SenderUserId { get; set; }
    public string SenderName { get; set; } = "";
    public string SenderRole { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}
