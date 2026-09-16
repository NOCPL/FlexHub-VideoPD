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
            meeting.GroupId,
            meeting.MemberId));

    public Task MeetingEndedAsync(Meeting meeting) =>
        NotifyUserAsync(meeting.CreditOfficerId, new NotificationPayload(
            "MeetingEnded",
            meeting.Id,
            meeting.Code,
            $"Visit ended. Duration {meeting.DurationSeconds ?? 0}s.",
            meeting.Bank,
            meeting.GroupId,
            meeting.MemberId));

    public Task RecordingReadyAsync(Meeting meeting) =>
        NotifyUserAsync(meeting.CreditOfficerId, new NotificationPayload(
            "RecordingReady",
            meeting.Id,
            meeting.Code,
            "A recording segment is ready for this visit.",
            meeting.Bank,
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
            waiting.GroupId,
            waiting.MemberId);
        return Task.WhenAll(
            NotifyUserAsync(creditOfficerId, payload),
            hub.Clients.Group($"host:{slug}").SendAsync("waitingArrived", waiting));
    }

    public Task WaitingLeftAsync(string slug, Guid waitingId) =>
        hub.Clients.Group($"host:{slug}").SendAsync("waitingLeft", waitingId);

    public Task LobbyChatAsync(string slug, Guid waitingId, ChatMessageDto message) =>
        Task.WhenAll(
            hub.Clients.Group($"host:{slug}").SendAsync("lobbyChat", waitingId, message),
            hub.Clients.Group($"waiting:{waitingId}").SendAsync("lobbyChat", waitingId, message));

    public Task AdmittedAsync(string slug, Guid waitingId, JoinTokenResponse field, JoinTokenResponse host) =>
        Task.WhenAll(
            hub.Clients.Group($"waiting:{waitingId}").SendAsync("admitted", field),
            hub.Clients.Group($"host:{slug}").SendAsync("admitted", waitingId, host));
}
