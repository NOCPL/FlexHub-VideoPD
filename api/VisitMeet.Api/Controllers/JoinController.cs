using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Controllers;

[ApiController]
[Route("api/join")]
public class JoinController(LobbyService lobby) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("join")]
    public async Task<ActionResult<WaitResponse>> Join(JoinMeetingRequest request)
    {
        try
        {
            return await lobby.EnqueueAsync(request);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
