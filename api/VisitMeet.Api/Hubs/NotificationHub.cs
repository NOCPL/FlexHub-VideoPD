using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VisitMeet.Api.Auth;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Hubs;

[Authorize]
public class NotificationHub(MeetingService meetings) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        var waitingId = Context.User?.GetWaitingId();
        if (waitingId is Guid wait)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"waiting:{wait}");
        }

        var hostSlug = Context.User?.FindFirst("hostSlug")?.Value;
        if (!string.IsNullOrEmpty(hostSlug))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"host:{hostSlug}");
        }

        await base.OnConnectedAsync();
    }

    public async Task WatchHost(string slug)
    {
        var role = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var officer = await meetings.FindOfficerBySlugAsync(slug);
        if (officer is null) throw new HubException("Unknown host link.");
        if (role != Roles.Admin && Context.User?.GetUserId() != officer.Id)
        {
            throw new HubException("Not allowed to watch this host.");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"host:{slug}");
    }
}
