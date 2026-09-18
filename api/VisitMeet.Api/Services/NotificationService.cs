using Microsoft.AspNetCore.SignalR;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Hubs;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Services;

public class NotificationService(IHubContext<NotificationHub> hub)
{
    public Task NotifyUserAsync(Guid userId, NotificationPayload payload) =>
        hub.Clients.Group($"user:{userId}").SendAsync("notify", payload);

    public Task FieldOfficerJoinedAsync(Meeting meeting) =>
        NotifyUserAsync(meeting.CreditOfficerId, new NotificationPayload(
            "FieldOfficerJoined",
            meeting.Id,
            meeting.Code,
            "Field officer joined. Recording segment started.",
            meeting.Bank,
            meeting.Branch,
            meeting.GroupId,
            meeting.MemberId));

    public Task MeetingEndedAsync(Meeting meeting) =>
        NotifyUserAsync(meeting.CreditOfficerId, new NotificationPayload(
            "MeetingEnded",
            meeting.Id,
            meeting.Code,
            $"Video PD ended. Duration {meeting.DurationSeconds ?? 0}s.",
            meeting.Bank,
            meeting.Branch,
            meeting.GroupId,
            meeting.MemberId));

    public Task RecordingReadyAsync(Meeting meeting) =>
        NotifyUserAsync(meeting.CreditOfficerId, new NotificationPayload(
            "RecordingReady",
            meeting.Id,
            meeting.Code,
            "A recording segment is ready for this visit.",
            meeting.Bank,
            meeting.Branch,
            meeting.GroupId,
            meeting.MemberId));

    public Task WaitingArrivedAsync(string slug, Guid creditOfficerId, WaitingOfficerDto waiting)
    {
        var payload = new NotificationPayload(
            "WaitingArrived",
            Guid.Empty,
            slug,
            $"{waiting.DisplayName} is waiting to be admitted.",
            waiting.Bank,
            waiting.Branch,
            waiting.GroupId,
            waiting.MemberId);
        return Task.WhenAll(
            NotifyUserAsync(creditOfficerId, payload),
            hub.Clients.Group($"host:{slug}").SendAsync("waitingArrived", waiting));
    }

    public Task WaitingLeftAsync(string slug, Guid waitingId) =>
        Task.WhenAll(
            hub.Clients.Group($"host:{slug}").SendAsync("waitingLeft", waitingId),
            hub.Clients.Group($"lobby:{slug}").SendAsync("waitingLeft", waitingId));

    public Task WaitingDeniedAsync(string slug, Guid waitingId) =>
        Task.WhenAll(
            hub.Clients.Group($"host:{slug}").SendAsync("waitingLeft", waitingId),
            hub.Clients.Group($"waiting:{waitingId}").SendAsync("denied"));

    public Task LobbyChatAsync(string slug, Guid? recipientWaitingId, LobbyMessageDto message)
    {
        var sends = new List<Task>
        {
            hub.Clients.Group($"host:{slug}").SendAsync("lobbyChat", message)
        };
        sends.Add(recipientWaitingId is Guid recipient
            ? hub.Clients.Group($"waiting:{recipient}").SendAsync("lobbyChat", message)
            : hub.Clients.Group($"lobby:{slug}").SendAsync("lobbyChat", message));
        return Task.WhenAll(sends);
    }

    public Task AdmittedAsync(string slug, Guid waitingId, JoinTokenResponse field, JoinTokenResponse host) =>
        Task.WhenAll(
            hub.Clients.Group($"waiting:{waitingId}").SendAsync("admitted", field),
            hub.Clients.Group($"host:{slug}").SendAsync("admitted", waitingId, host));

    public Task ActiveOfficerEndedAsync(string slug, Guid waitingId, int durationSeconds) =>
        Task.WhenAll(
            hub.Clients.Group($"host:{slug}").SendAsync("activeEnded", waitingId, durationSeconds),
            hub.Clients.Group($"waiting:{waitingId}").SendAsync("meetingEnded", durationSeconds));

    public Task ActiveOfficerConnectedAsync(string slug, Guid waitingId, DateTimeOffset connectedAt) =>
        hub.Clients.Group($"host:{slug}").SendAsync("activeConnected", waitingId, connectedAt);
}
