namespace VisitMeet.Api.Dtos;

public record LoginRequest(string Email, string Password);

public record UserDto(Guid Id, string Name, string Email, string Role, string? HostSlug, string? HostUrl);

public record AuthResponse(string Token, UserDto User);

public record ScheduleMeetingRequest(
    string? Bank,
    string? Branch,
    string? GroupId,
    string? MemberId,
    string? MemberName,
    DateTimeOffset ScheduledAt,
    Guid? CreditOfficerId);

public record JoinMeetingRequest(
    string Slug,
    string? Bank,
    string? Branch,
    string? GroupId,
    string? MemberId,
    string? DisplayName);

public record ChatPostRequest(string Body);
public record LobbyChatRequest(string Body, Guid? RecipientWaitingOfficerId);
public record GeotagReportRequest(
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    DateTimeOffset? CapturedAt,
    string? Error);
public record CreateCreditOfficerRequest(string Name, string Email, string TemporaryPassword);

public record MeetingListItemDto(
    Guid Id,
    string Code,
    string Bank,
    string Branch,
    string GroupId,
    string MemberId,
    string MemberName,
    DateTimeOffset ScheduledAt,
    string Status,
    string CreditOfficerName,
    Guid CreditOfficerId,
    string? HostSlug,
    string HostUrl,
    string FieldUrl,
    int? DurationSeconds,
    string? RecordingStatus,
    int RecordingSegments);

public record RecordingDto(
    Guid Id,
    string Status,
    int Sequence,
    string Bank,
    string Branch,
    string GroupId,
    string MemberId,
    string? FilePath,
    int? DurationSeconds,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? JoinedAt,
    DateTimeOffset? LeftAt);

public record SnapshotDto(
    Guid Id,
    string Bank,
    string Branch,
    string GroupId,
    string MemberId,
    string Url,
    int? CropX,
    int? CropY,
    int? CropWidth,
    int? CropHeight,
    DateTimeOffset CreatedAt,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    DateTimeOffset? GeoCapturedAt,
    string? GeoError);

public record ChatMessageDto(
    Guid Id,
    Guid SenderUserId,
    string SenderName,
    string SenderRole,
    string Body,
    DateTimeOffset SentAt);

public record LobbyMessageDto(
    Guid Id,
    Guid SenderUserId,
    string SenderName,
    string SenderRole,
    Guid? RecipientWaitingOfficerId,
    string Body,
    DateTimeOffset SentAt);

public record MeetingDetailDto(
    Guid Id,
    string Code,
    string Bank,
    string Branch,
    string GroupId,
    string MemberId,
    string MemberName,
    DateTimeOffset ScheduledAt,
    string Status,
    string LiveKitRoomName,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int? DurationSeconds,
    string HostUrl,
    string FieldUrl,
    UserDto CreditOfficer,
    IReadOnlyList<RecordingDto> Recordings,
    IReadOnlyList<SnapshotDto> Snapshots,
    IReadOnlyList<ChatMessageDto> Chat);

public record JoinTokenResponse(
    string Token,
    string GuestToken,
    string LiveKitUrl,
    string Identity,
    MeetingDetailDto Meeting);

public record WaitingOfficerDto(
    Guid Id,
    string DisplayName,
    string Bank,
    string Branch,
    string GroupId,
    string MemberId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AdmittedAt,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? LeftAt,
    int? CallDurationSeconds,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    DateTimeOffset? GeoCapturedAt,
    string? GeoError);

public record WaitingRoomDto(
    WaitingOfficerDto Waiting,
    IReadOnlyList<LobbyMessageDto> Chat);

public record WaitResponse(
    string GuestToken,
    WaitingOfficerDto Waiting,
    string CreditOfficerName,
    string HostSlug);

public record HostLobbyDto(
    UserDto Officer,
    MeetingDetailDto? Meeting,
    IReadOnlyList<WaitingOfficerDto> Waiting,
    WaitingOfficerDto? Active,
    IReadOnlyList<LobbyMessageDto> Chat);

public record AdmitResponse(
    JoinTokenResponse Field,
    JoinTokenResponse Host,
    WaitingOfficerDto Waiting);

public record NotificationPayload(
    string Type,
    Guid MeetingId,
    string Code,
    string Message,
    string? Bank,
    string? Branch,
    string? GroupId,
    string? MemberId);
