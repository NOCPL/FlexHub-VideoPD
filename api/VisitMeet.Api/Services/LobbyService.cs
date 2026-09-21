using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Auth;
using VisitMeet.Api.Data;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Services;

public class LobbyService(
    AppDbContext db,
    MeetingService meetings,
    LiveKitService liveKit,
    TokenService tokens,
    NotificationService notifications)
{
    public WaitingOfficerDto ToDto(WaitingOfficer waiting) =>
        new(
            waiting.Id,
            waiting.DisplayName,
            waiting.Bank,
            waiting.Branch,
            waiting.GroupId,
            waiting.MemberId,
            waiting.Status,
            waiting.CreatedAt,
            waiting.AdmittedAt,
            waiting.ConnectedAt,
            waiting.LeftAt,
            waiting.CallDurationSeconds,
            waiting.Latitude,
            waiting.Longitude,
            waiting.AccuracyMeters,
            waiting.GeoCapturedAt,
            waiting.GeoError);

    public static LobbyMessageDto ToChat(LobbyMessage message) =>
        new(
            message.Id,
            message.SenderUserId,
            message.SenderName,
            message.SenderRole,
            message.RecipientWaitingOfficerId,
            message.Body,
            message.SentAt);

    public async Task<WaitingOfficer?> LoadWaitingAsync(Guid id) =>
        await db.WaitingOfficers.Include(w => w.CreditOfficer)
            .FirstOrDefaultAsync(w => w.Id == id);

    public async Task<IReadOnlyList<WaitingOfficer>> ListWaitingAsync(string slug)
    {
        var list = await db.WaitingOfficers
            .Where(w => w.HostSlug == slug && w.Status == WaitingStatuses.Waiting)
            .ToListAsync();
        return list.OrderBy(w => w.CreatedAt).ToList();
    }

    public async Task<HostLobbyDto> GetLobbyAsync(User officer)
    {
        var meeting = await meetings.FindCurrentVisitAsync(officer.Id);
        var waiting = await ListWaitingAsync(officer.HostSlug ?? "");
        var active = await db.WaitingOfficers
            .FirstOrDefaultAsync(w =>
                w.HostSlug == officer.HostSlug &&
                (w.Status == WaitingStatuses.Admitted || w.Status == WaitingStatuses.Connected));
        var chat = await db.LobbyMessages
            .Where(m => m.HostSlug == officer.HostSlug)
            .ToListAsync();
        return new HostLobbyDto(
            meetings.Mapper.ToUser(officer),
            meeting is null ? null : meetings.Mapper.ToDetail(meeting),
            waiting.Select(ToDto).ToList(),
            active is null ? null : ToDto(active),
            chat.OrderBy(m => m.SentAt).Select(ToChat).ToList());
    }

    public async Task<WaitResponse> EnqueueAsync(JoinMeetingRequest request)
    {
        var officer = await meetings.FindOfficerBySlugAsync(request.Slug)
                      ?? throw new KeyNotFoundException("Unknown meeting link.");

        var guestId = Guid.NewGuid();
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? "Field officer"
            : request.DisplayName.Trim();
        var waiting = new WaitingOfficer
        {
            Id = guestId,
            CreditOfficerId = officer.Id,
            HostSlug = officer.HostSlug ?? request.Slug,
            DisplayName = displayName,
            Bank = (request.Bank ?? "").Trim(),
            Branch = (request.Branch ?? "").Trim(),
            GroupId = (request.GroupId ?? "").Trim(),
            MemberId = (request.MemberId ?? "").Trim(),
            Status = WaitingStatuses.Waiting,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.WaitingOfficers.Add(waiting);
        await db.SaveChangesAsync();

        var dto = ToDto(waiting);
        await notifications.WaitingArrivedAsync(waiting.HostSlug, officer.Id, dto);
        var guestToken = tokens.CreateGuestToken(guestId, displayName, waiting.Id, waiting.HostSlug);
        return new WaitResponse(guestToken, dto, officer.Name, waiting.HostSlug);
    }

    public async Task<IReadOnlyList<LobbyMessageDto>> GetChatAsync(WaitingOfficer waiting, bool host)
    {
        var query = db.LobbyMessages.Where(m => m.HostSlug == waiting.HostSlug);
        if (!host)
        {
            query = query.Where(m =>
                m.RecipientWaitingOfficerId == null ||
                m.RecipientWaitingOfficerId == waiting.Id);
        }
        var messages = await query.ToListAsync();
        return messages.OrderBy(m => m.SentAt).Select(ToChat).ToList();
    }

    public async Task<LobbyMessageDto> PostChatAsync(
        WaitingOfficer waiting,
        Guid senderId,
        string senderName,
        string senderRole,
        string body,
        Guid? recipientWaitingOfficerId)
    {
        if (waiting.Status != WaitingStatuses.Waiting)
        {
            throw new InvalidOperationException("Chat is only available while the field officer is waiting.");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("Message cannot be empty.");
        }
        if (senderRole == Roles.FieldGuest &&
            recipientWaitingOfficerId is not null &&
            recipientWaitingOfficerId != waiting.Id)
        {
            throw new InvalidOperationException("Field officers can only send privately to the credit officer.");
        }
        if (recipientWaitingOfficerId is Guid recipient)
        {
            var validRecipient = await db.WaitingOfficers.AnyAsync(w =>
                w.Id == recipient &&
                w.HostSlug == waiting.HostSlug &&
                w.Status == WaitingStatuses.Waiting);
            if (!validRecipient)
            {
                throw new InvalidOperationException("That field officer is no longer waiting.");
            }
        }

        var message = new LobbyMessage
        {
            Id = Guid.NewGuid(),
            HostSlug = waiting.HostSlug,
            RecipientWaitingOfficerId = recipientWaitingOfficerId,
            SenderUserId = senderId,
            SenderName = senderName,
            SenderRole = senderRole,
            Body = body.Trim(),
            SentAt = DateTimeOffset.UtcNow
        };
        db.LobbyMessages.Add(message);
        await db.SaveChangesAsync();
        var dto = ToChat(message);
        await notifications.LobbyChatAsync(waiting.HostSlug, recipientWaitingOfficerId, dto);
        return dto;
    }

    public async Task LeaveAsync(WaitingOfficer waiting)
    {
        if (waiting.Status == WaitingStatuses.Waiting)
        {
            waiting.Status = WaitingStatuses.Left;
            waiting.LeftAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await notifications.WaitingLeftAsync(waiting.HostSlug, waiting.Id);
        }
    }

    public async Task DenyAsync(WaitingOfficer waiting)
    {
        if (waiting.Status != WaitingStatuses.Waiting)
        {
            throw new InvalidOperationException("This field officer is not waiting.");
        }
        waiting.Status = WaitingStatuses.Denied;
        waiting.LeftAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await notifications.WaitingDeniedAsync(waiting.HostSlug, waiting.Id);
    }

    public async Task<AdmitResponse> AdmitAsync(User officer, WaitingOfficer waiting)
    {
        if (waiting.Status != WaitingStatuses.Waiting)
        {
            throw new InvalidOperationException("This field officer is not waiting.");
        }
        var active = await db.WaitingOfficers.AnyAsync(w =>
            w.HostSlug == waiting.HostSlug &&
            (w.Status == WaitingStatuses.Admitted || w.Status == WaitingStatuses.Connected));
        if (active)
        {
            throw new InvalidOperationException("Finish the active Video PD before admitting another field officer.");
        }
        EnsureFieldGps(waiting);

        var meeting = await meetings.EnsureDeskAsync(officer);
        await meetings.StampJoinParamsAsync(
            meeting,
            waiting.Bank,
            waiting.Branch,
            waiting.GroupId,
            waiting.MemberId);
        meeting.MemberName = string.IsNullOrWhiteSpace(waiting.DisplayName) ? waiting.MemberId : waiting.DisplayName;
        await db.SaveChangesAsync();
        await meetings.MarkWaitingAsync(meeting);
        meeting = await meetings.LoadAsync(meeting.Id);

        waiting.Status = WaitingStatuses.Admitted;
        waiting.MeetingId = meeting.Id;
        waiting.AdmittedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var fieldIdentity = $"fo-{waiting.Id:N}";
        var fieldLivekit = liveKit.CreateParticipantToken(
            meeting,
            fieldIdentity,
            waiting.DisplayName,
            Roles.FieldGuest,
            new Dictionary<string, string>
            {
                ["guestId"] = waiting.Id.ToString(),
                ["bank"] = waiting.Bank,
                ["branch"] = waiting.Branch,
                ["groupId"] = waiting.GroupId,
                ["memberId"] = waiting.MemberId
            });
        var guestToken = tokens.CreateGuestToken(
            waiting.Id,
            waiting.DisplayName,
            waiting.Id,
            waiting.HostSlug,
            meeting.Id);
        var field = new JoinTokenResponse(fieldLivekit, guestToken, liveKit.WsUrl, fieldIdentity, meetings.Mapper.ToDetail(meeting));

        var hostIdentity = $"co-{officer.Id:N}";
        var hostLivekit = liveKit.CreateParticipantToken(meeting, hostIdentity, officer.Name, Roles.CreditOfficer);
        var host = new JoinTokenResponse(hostLivekit, "", liveKit.WsUrl, hostIdentity, meetings.Mapper.ToDetail(meeting));

        var dto = ToDto(waiting);
        await notifications.AdmittedAsync(waiting.HostSlug, waiting.Id, field, host);
        return new AdmitResponse(field, host, dto);
    }

    public async Task<JoinTokenResponse> ConnectAdmittedAsync(WaitingOfficer waiting)
    {
        if (waiting.Status != WaitingStatuses.Admitted || waiting.MeetingId is null)
        {
            throw new InvalidOperationException("The credit officer has not admitted you yet.");
        }
        EnsureFieldGps(waiting);
        var meeting = await meetings.LoadAsync(waiting.MeetingId.Value);
        var identity = $"fo-{waiting.Id:N}";
        var livekit = liveKit.CreateParticipantToken(
            meeting,
            identity,
            waiting.DisplayName,
            Roles.FieldGuest,
            new Dictionary<string, string>
            {
                ["guestId"] = waiting.Id.ToString(),
                ["bank"] = waiting.Bank,
                ["branch"] = waiting.Branch,
                ["groupId"] = waiting.GroupId,
                ["memberId"] = waiting.MemberId
            });
        var guestToken = tokens.CreateGuestToken(
            waiting.Id,
            waiting.DisplayName,
            waiting.Id,
            waiting.HostSlug,
            meeting.Id);
        return new JoinTokenResponse(livekit, guestToken, liveKit.WsUrl, identity, meetings.Mapper.ToDetail(meeting));
    }

    private static void EnsureFieldGps(WaitingOfficer waiting)
    {
        if (waiting.Latitude is null || waiting.Longitude is null)
        {
            throw new InvalidOperationException(
                "The field officer must allow GPS on their phone before the call can start.");
        }
    }

    public async Task<WaitingOfficerDto> ReportGeotagAsync(WaitingOfficer waiting, GeotagReportRequest request)
    {
        if (waiting.Status is WaitingStatuses.Left or WaitingStatuses.Denied)
        {
            throw new InvalidOperationException("This field officer is no longer in the Video PD.");
        }

        var capturedAt = request.CapturedAt ?? DateTimeOffset.UtcNow;
        if (capturedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            capturedAt = DateTimeOffset.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(request.Error))
        {
            var error = request.Error.Trim();
            waiting.GeoError = error.Length > 120 ? error[..120] : error;
            waiting.GeoCapturedAt ??= capturedAt;
        }
        else
        {
            if (request.Latitude is null || request.Longitude is null)
            {
                throw new InvalidOperationException("Latitude and longitude are required.");
            }

            var lat = request.Latitude.Value;
            var lng = request.Longitude.Value;
            if (lat is < -90 or > 90 || lng is < -180 or > 180)
            {
                throw new InvalidOperationException("Coordinates are out of range.");
            }

            waiting.Latitude = lat;
            waiting.Longitude = lng;
            waiting.AccuracyMeters = request.AccuracyMeters is < 0 ? 0 : request.AccuracyMeters;
            waiting.GeoCapturedAt = capturedAt;
            waiting.GeoError = null;
        }

        await db.SaveChangesAsync();
        var dto = ToDto(waiting);
        await notifications.GeotagUpdatedAsync(waiting.HostSlug, dto);
        return dto;
    }
}
