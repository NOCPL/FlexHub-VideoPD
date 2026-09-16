using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Controllers;

[ApiController]
[Route("api/webhooks/livekit")]
public class WebhooksController(LiveKitService liveKit, MeetingService meetings, ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var auth = Request.Headers.Authorization.FirstOrDefault() ?? "";
        try
        {
            var evt = liveKit.ParseWebhook(body, auth);
            var room = evt.Room?.Name ?? "";
            var identity = evt.Participant?.Identity;
            var egressId = evt.EgressInfo?.EgressId;
            int? duration = null;
            string? filePath = null;
            if (evt.EgressInfo != null)
            {
                if (evt.EgressInfo.FileResults.Count > 0)
                {
                    var file = evt.EgressInfo.FileResults[0];
                    duration = file.Duration > 0 ? (int)(file.Duration / 1_000_000_000) : null;
                    filePath = file.Filename;
                }
                else if (evt.EgressInfo.File != null)
                {
                    duration = evt.EgressInfo.File.Duration > 0
                        ? (int)(evt.EgressInfo.File.Duration / 1_000_000_000)
                        : null;
                    filePath = evt.EgressInfo.File.Filename;
                }
            }

            logger.LogInformation("LiveKit webhook {Event} room={Room} identity={Identity}", evt.Event, room, identity);
            await meetings.HandleWebhookAsync(evt.Event, room, identity, egressId, duration, filePath);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rejected LiveKit webhook");
            return Unauthorized();
        }
    }
}
