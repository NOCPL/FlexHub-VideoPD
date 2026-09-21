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
    public async Task<ActionResult<WaitingRoomDto>> Get(Guid id)
    {
        var waiting = await lobby.LoadWaitingAsync(id);
        if (waiting is null) return NotFound();
        if (!CanAccess(waiting)) return Forbid();
        var isHost = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.CreditOfficer);
        return new WaitingRoomDto(
            lobby.ToDto(waiting),
            await lobby.GetChatAsync(waiting, isHost));
    }

    [HttpPost("{id:guid}/chat")]
    public async Task<ActionResult<LobbyMessageDto>> Chat(Guid id, LobbyChatRequest request)
    {
        var waiting = await lobby.LoadWaitingAsync(id);
        if (waiting is null) return NotFound();
        if (!CanAccess(waiting)) return Forbid();
        try
        {
            var name = User.Identity?.Name ?? "Officer";
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? Roles.FieldGuest;
            return await lobby.PostChatAsync(
                waiting,
                User.GetUserId(),
                name,
                role,
                request.Body,
                request.RecipientWaitingOfficerId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/deny")]
    public async Task<IActionResult> Deny(Guid id)
    {
        var waiting = await lobby.LoadWaitingAsync(id);
        if (waiting is null) return NotFound();
        if (!User.IsInRole(Roles.Admin) &&
            (!User.IsInRole(Roles.CreditOfficer) || waiting.CreditOfficerId != User.GetUserId()))
        {
            return Forbid();
        }
        try
        {
            await lobby.DenyAsync(waiting);
            return NoContent();
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

    [HttpPost("{id:guid}/geotag")]
    public async Task<ActionResult<WaitingOfficerDto>> Geotag(Guid id, GeotagReportRequest request)
    {
        var waiting = await lobby.LoadWaitingAsync(id);
        if (waiting is null) return NotFound();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role != Roles.FieldGuest || User.GetWaitingId() != waiting.Id)
        {
            return Forbid();
        }
        try
        {
            return await lobby.ReportGeotagAsync(waiting, request);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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
