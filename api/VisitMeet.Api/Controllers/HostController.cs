using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisitMeet.Api.Auth;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Controllers;

[ApiController]
[Route("api/host")]
[Authorize(Roles = $"{Roles.CreditOfficer},{Roles.Admin}")]
public class HostController(MeetingService meetings, LobbyService lobby, LiveKitService liveKit) : ControllerBase
{
    [HttpGet("{slug}")]
    public async Task<ActionResult<HostLobbyDto>> Current(string slug)
    {
        var officer = await meetings.FindOfficerBySlugAsync(slug);
        if (officer is null) return NotFound(new { message = "Unknown host link." });
        if (!CanOpen(officer)) return Forbid();
        return await lobby.GetLobbyAsync(officer);
    }

    [HttpGet("{slug}/waiting")]
    public async Task<ActionResult<IReadOnlyList<WaitingOfficerDto>>> Waiting(string slug)
    {
        var officer = await meetings.FindOfficerBySlugAsync(slug);
        if (officer is null) return NotFound(new { message = "Unknown host link." });
        if (!CanOpen(officer)) return Forbid();
        var list = await lobby.ListWaitingAsync(slug);
        return list.Select(w => lobby.ToDto(w)).ToList();
    }

    [HttpPost("{slug}/waiting/{id:guid}/admit")]
    public async Task<ActionResult<AdmitResponse>> Admit(string slug, Guid id)
    {
        var officer = await meetings.FindOfficerBySlugAsync(slug);
        if (officer is null) return NotFound(new { message = "Unknown host link." });
        if (!CanOpen(officer)) return Forbid();
        var waiting = await lobby.LoadWaitingAsync(id);
        if (waiting is null || waiting.HostSlug != slug)
        {
            return NotFound(new { message = "That field officer is not waiting on this host." });
        }
        try
        {
            return await lobby.AdmitAsync(officer, waiting);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{slug}/token")]
    public async Task<ActionResult<JoinTokenResponse>> Token(string slug)
    {
        var officer = await meetings.FindOfficerBySlugAsync(slug);
        if (officer is null) return NotFound(new { message = "Unknown host link." });
        if (!CanOpen(officer)) return Forbid();

        var meeting = await meetings.EnsureDeskAsync(officer);
        var identity = $"co-{officer.Id:N}";
        var token = liveKit.CreateParticipantToken(meeting, identity, officer.Name, Roles.CreditOfficer);
        return new JoinTokenResponse(token, "", liveKit.WsUrl, identity, meetings.Mapper.ToDetail(meeting));
    }

    private bool CanOpen(User officer)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role == Roles.Admin) return true;
        return User.GetUserId() == officer.Id;
    }
}
