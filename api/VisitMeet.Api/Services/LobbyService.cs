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
    public WaitingOfficerDto ToDto(WaitingOfficer waiting, bool includeChat = false) =>
        new(
            waiting.Id,
            waiting.DisplayName,
            waiting.Bank,
            waiting.GroupId,
            waiting.MemberId,
            waiting.Status,
            waiting.CreatedAt,
            waiting.AdmittedAt,
            includeChat
                ? waiting.Messages.OrderBy(m => m.SentAt).Select(ToChat).ToList()
                : []);

    public static ChatMessageDto ToChat(LobbyMessage message) =>
        new(message.Id, message.SenderUserId, message.SenderName, message.SenderRole, message.Body, message.SentAt);

    public async Task<WaitingOfficer?> LoadWaitingAsync(Guid id) =>
        await db.WaitingOfficers.Include(w => w.Messages).Include(w => w.CreditOfficer)
            .FirstOrDefaultAsync(w => w.Id == id);

    public async Task<IReadOnlyList<WaitingOfficer>> ListWaitingAsync(string slug)
    {
        var list = await db.WaitingOfficers
            .Include(w => w.Messages)
            .Where(w => w.HostSlug == slug && w.Status == WaitingStatuses.Waiting)
            .ToListAsync();
        return list.OrderBy(w => w.CreatedAt).ToList();
    }

    public async Task<HostLobbyDto> GetLobbyAsync(User officer)
    {
        var meeting = await meetings.FindCurrentVisitAsync(officer.Id);
        var waiting = await ListWaitingAsync(officer.HostSlug ?? "");
        return new HostLobbyDto(
            meetings.Mapper.ToUser(officer),
            meeting is null ? null : meetings.Mapper.ToDetail(meeting),
            waiting.Select(w => ToDto(w)).ToList());
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
            GroupId = (request.GroupId ?? "").Trim(),
            MemberId = (request.MemberId ?? "").Trim(),
            Status = WaitingStatuses.Waiting,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.WaitingOfficers.Add(waiting);
        await db.SaveChangesAsync();

        var dto = ToDto(waiting, true);
        await notifications.WaitingArrivedAsync(waiting.HostSlug, officer.Id, dto);
        var guestToken = tokens.CreateGuestToken(guestId, displayName, waiting.Id);
        return new WaitResponse(guestToken, dto, officer.Name, waiting.HostSlug);
    }

    public async Task<ChatMessageDto> PostChatAsync(WaitingOfficer waiting, Guid senderId, string senderName, string senderRole, string body)
    {
        if (waiting.Status != WaitingStatuses.Waiting)
        {
            throw new InvalidOperationException("Chat is only available while the field officer is waiting.");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("Message cannot be empty.");
        }

        var message = new LobbyMessage
        {
            Id = Guid.NewGuid(),
            WaitingOfficerId = waiting.Id,
            SenderUserId = senderId,
            SenderName = senderName,
            SenderRole = senderRole,
            Body = body.Trim(),
            SentAt = DateTimeOffset.UtcNow
        };
        db.LobbyMessages.Add(message);
        await db.SaveChangesAsync();
        var dto = ToChat(message);
        await notifications.LobbyChatAsync(waiting.HostSlug, waiting.Id, dto);
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

    public async Task<AdmitResponse> AdmitAsync(User officer, WaitingOfficer waiting)
    {
        if (waiting.Status != WaitingStatuses.Waiting)
        {
            throw new InvalidOperationException("This field officer is not waiting.");
        }

        var meeting = await meetings.EnsureDeskAsync(officer);
        await meetings.StampJoinParamsAsync(meeting, waiting.Bank, waiting.GroupId, waiting.MemberId);
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
                ["groupId"] = waiting.GroupId,
                ["memberId"] = waiting.MemberId
            });
        var guestToken = tokens.CreateGuestToken(waiting.Id, waiting.DisplayName, waiting.Id, meeting.Id);
        var field = new JoinTokenResponse(fieldLivekit, guestToken, liveKit.WsUrl, fieldIdentity, meetings.Mapper.ToDetail(meeting));

        var hostIdentity = $"co-{officer.Id:N}";
        var hostLivekit = liveKit.CreateParticipantToken(meeting, hostIdentity, officer.Name, Roles.CreditOfficer);
        var host = new JoinTokenResponse(hostLivekit, "", liveKit.WsUrl, hostIdentity, meetings.Mapper.ToDetail(meeting));

        var dto = ToDto(waiting, true);
        await notifications.AdmittedAsync(waiting.HostSlug, waiting.Id, field, host);
        return new AdmitResponse(field, host, dto);
    }

    public async Task<JoinTokenResponse> ConnectAdmittedAsync(WaitingOfficer waiting)
    {
        if (waiting.Status != WaitingStatuses.Admitted || waiting.MeetingId is null)
        {
            throw new InvalidOperationException("The credit officer has not admitted you yet.");
        }
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
                ["groupId"] = waiting.GroupId,
                ["memberId"] = waiting.MemberId
            });
        var guestToken = tokens.CreateGuestToken(waiting.Id, waiting.DisplayName, waiting.Id, meeting.Id);
        return new JoinTokenResponse(livekit, guestToken, liveKit.WsUrl, identity, meetings.Mapper.ToDetail(meeting));
    }
}
