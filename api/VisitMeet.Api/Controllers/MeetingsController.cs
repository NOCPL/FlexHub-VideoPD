using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Auth;
using VisitMeet.Api.Data;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Controllers;

[ApiController]
[Route("api/meetings")]
[Authorize]
public class MeetingsController(
    AppDbContext db,
    MeetingService meetings) : ControllerBase
{
    private Task<User?> CurrentUserAsync() => db.Users.FindAsync(User.GetUserId()).AsTask();

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MeetingListItemDto>>> List()
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();

        var query = MeetingMapper.WithGraph(db.Meetings).AsQueryable();
        if (user.Role == Roles.CreditOfficer)
        {
            query = query.Where(m => m.CreditOfficerId == user.Id);
        }
        else if (user.Role != Roles.Admin)
        {
            return Forbid();
        }

        var items = await query.ToListAsync();
        return items
            .OrderByDescending(m => m.ScheduledAt)
            .Select(meetings.Mapper.ToListItem)
            .ToList();
    }

    [HttpPost]
    public async Task<ActionResult<MeetingDetailDto>> Schedule(ScheduleMeetingRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();
        if (user.Role is not (Roles.CreditOfficer or Roles.Admin))
        {
            return Forbid();
        }

        try
        {
            var meeting = await meetings.ScheduleAsync(user, request);
            return CreatedAtAction(nameof(Get), new { id = meeting.Id }, meetings.Mapper.ToDetail(meeting));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MeetingDetailDto>> Get(Guid id)
    {
        var meeting = await MeetingMapper.WithGraph(db.Meetings).FirstOrDefaultAsync(m => m.Id == id);
        if (meeting is null) return NotFound();
        if (!CanAccess(meeting)) return Forbid();
        return meetings.Mapper.ToDetail(meeting);
    }

    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<MeetingDetailDto>> End(Guid id)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();
        if (user.Role is not (Roles.CreditOfficer or Roles.Admin)) return Forbid();
        var meeting = await meetings.LoadAsync(id);
        if (!CanAccess(meeting)) return Forbid();
        await meetings.CompleteAsync(meeting);
        return meetings.Mapper.ToDetail(await meetings.LoadAsync(id));
    }

    [HttpGet("{id:guid}/chat")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> Chat(Guid id)
    {
        var meeting = await meetings.LoadAsync(id);
        if (!CanAccess(meeting)) return Forbid();
        return meetings.Mapper.ToDetail(meeting).Chat.ToList();
    }

    [HttpPost("{id:guid}/chat")]
    public async Task<ActionResult<ChatMessageDto>> PostChat(Guid id, ChatPostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { message = "Message cannot be empty." });
        }
        var meeting = await db.Meetings.FindAsync(id);
        if (meeting is null) return NotFound();
        if (!CanAccess(meeting)) return Forbid();

        var user = await CurrentUserAsync();
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            MeetingId = meeting.Id,
            SenderUserId = User.GetUserId(),
            SenderName = user?.Name ?? User.Identity?.Name ?? "Field officer",
            SenderRole = user?.Role ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? Roles.FieldGuest,
            Body = request.Body.Trim(),
            SentAt = DateTimeOffset.UtcNow
        };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync();
        return new ChatMessageDto(message.Id, message.SenderUserId, message.SenderName, message.SenderRole, message.Body, message.SentAt);
    }

    [HttpPost("{id:guid}/snapshots")]
    [RequestSizeLimit(8_000_000)]
    public async Task<ActionResult<SnapshotDto>> Snapshot(
        Guid id,
        IFormFile file,
        [FromForm] int? cropX,
        [FromForm] int? cropY,
        [FromForm] int? cropWidth,
        [FromForm] int? cropHeight)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Image file is required." });
        }
        var meeting = await meetings.LoadAsync(id);
        if (!CanAccess(meeting)) return Forbid();
        var snapshot = await meetings.SaveSnapshotAsync(meeting, User.GetUserId(), file, cropX, cropY, cropWidth, cropHeight);
        var dto = new SnapshotDto(
            snapshot.Id, snapshot.Bank, snapshot.Branch, snapshot.GroupId, snapshot.MemberId,
            $"/api/meetings/{meeting.Id}/snapshots/{snapshot.Id}/file",
            snapshot.CropX, snapshot.CropY, snapshot.CropWidth, snapshot.CropHeight, snapshot.CreatedAt);
        return Created(dto.Url, dto);
    }

    [HttpGet("{id:guid}/snapshots/{snapshotId:guid}/file")]
    public async Task<IActionResult> SnapshotFile(Guid id, Guid snapshotId)
    {
        var meeting = await meetings.LoadAsync(id);
        if (!CanAccess(meeting)) return Forbid();
        var snapshot = meeting.Snapshots.FirstOrDefault(s => s.Id == snapshotId);
        if (snapshot is null) return NotFound();
        var path = meetings.SnapshotPath(meeting, snapshot);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, snapshot.ContentType, snapshot.FileName);
    }

    private bool CanAccess(Meeting meeting)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role == Roles.Admin) return true;
        if (role == Roles.CreditOfficer) return meeting.CreditOfficerId == User.GetUserId();
        if (role == Roles.FieldGuest) return User.GetMeetingId() == meeting.Id;
        return false;
    }
}
