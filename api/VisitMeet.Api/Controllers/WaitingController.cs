using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisitMeet.Api.Auth;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Controllers;

[ApiController]
[Route("api/waiting")]
[Authorize]
public class WaitingController(LobbyService lobby) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WaitingOfficerDto>> Get(Guid id)
    {
        var waiting = await lobby.LoadWaitingAsync(id);
        if (waiting is null) return NotFound();
        if (!CanAccess(waiting)) return Forbid();
        return lobby.ToDto(waiting, true);
    }

    [HttpPost("{id:guid}/chat")]
    public async Task<ActionResult<ChatMessageDto>> Chat(Guid id, ChatPostRequest request)
    {
        var waiting = await lobby.LoadWaitingAsync(id);
        if (waiting is null) return NotFound();
        if (!CanAccess(waiting)) return Forbid();
        try
        {
            var name = User.Identity?.Name ?? "Officer";
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? Roles.FieldGuest;
            return await lobby.PostChatAsync(waiting, User.GetUserId(), name, role, request.Body);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id)
    {
        var waiting = await lobby.LoadWaitingAsync(id);
        if (waiting is null) return NotFound();
        if (!CanAccess(waiting)) return Forbid();
        await lobby.LeaveAsync(waiting);
        return NoContent();
    }

    [HttpPost("{id:guid}/connect")]
    public async Task<ActionResult<JoinTokenResponse>> Connect(Guid id)
    {
        var waiting = await lobby.LoadWaitingAsync(id);
        if (waiting is null) return NotFound();
        if (!CanAccess(waiting)) return Forbid();
        try
        {
            return await lobby.ConnectAdmittedAsync(waiting);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool CanAccess(WaitingOfficer waiting)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role == Roles.Admin) return true;
        if (role == Roles.CreditOfficer) return waiting.CreditOfficerId == User.GetUserId();
        if (role == Roles.FieldGuest) return User.GetWaitingId() == waiting.Id;
        return false;
    }
}
