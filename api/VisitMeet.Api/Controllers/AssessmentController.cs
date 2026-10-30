using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Auth;
using VisitMeet.Api.Data;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services.Assessment;

namespace VisitMeet.Api.Controllers;

[ApiController]
[Route("api/assessment")]
[Authorize(Roles = $"{Roles.Admin},{Roles.CreditOfficer}")]
public class AssessmentController(AppDbContext db, AssessmentService assessment, SessionService sessions, MemoService memos) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<LoanCategoryDto>>> Categories() =>
        (await assessment.CategoriesAsync()).Select(AssessmentMapper.ToDto).ToList();

    [HttpPost("categories")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<LoanCategoryDto>> CreateCategory(UpsertLoanCategoryRequest request)
    {
        try
        {
            var category = await assessment.UpsertCategoryAsync(null, request);
            return Created($"/api/assessment/categories/{category.Id}", AssessmentMapper.ToDto(category));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("categories/{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<LoanCategoryDto>> UpdateCategory(Guid id, UpsertLoanCategoryRequest request)
    {
        try
        {
            return AssessmentMapper.ToDto(await assessment.UpsertCategoryAsync(id, request));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("guides")]
    public async Task<ActionResult<IReadOnlyList<GuideListItemDto>>> Guides()
    {
        var guides = await assessment.Guides.OrderBy(g => g.Code).ThenByDescending(g => g.Version).ToListAsync();
        return guides.Select(AssessmentMapper.ToListItem).ToList();
    }

    [HttpGet("guides/{id:guid}")]
    public async Task<ActionResult<GuideDetailDto>> Guide(Guid id)
    {
        try
        {
            return AssessmentMapper.ToDetail(await assessment.LoadGuideAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("guides")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<GuideDetailDto>> CreateGuide(UpsertGuideRequest request)
    {
        try
        {
            var guide = await assessment.CreateGuideAsync(request, User.GetUserId());
            return Created($"/api/assessment/guides/{guide.Id}", AssessmentMapper.ToDetail(guide));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("guides/{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<GuideDetailDto>> UpdateGuide(Guid id, UpsertGuideRequest request)
    {
        try
        {
            return AssessmentMapper.ToDetail(await assessment.UpdateGuideAsync(id, request, User.GetUserId()));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("guides/{id:guid}/content")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<GuideDetailDto>> SaveContent(Guid id, SaveGuideContentRequest request)
    {
        try
        {
            return AssessmentMapper.ToDetail(await assessment.SaveContentAsync(id, request, User.GetUserId()));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("guides/{id:guid}/clone")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<GuideDetailDto>> Clone(Guid id, CloneGuideRequest request)
    {
        try
        {
            var guide = await assessment.CloneAsync(id, request, User.GetUserId());
            return Created($"/api/assessment/guides/{guide.Id}", AssessmentMapper.ToDetail(guide));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("rules")]
    public async Task<ActionResult<IReadOnlyList<DeviationRuleDto>>> Rules()
    {
        var rules = await db.DeviationRules.OrderBy(r => r.CheckKey).ThenBy(r => r.Severity).ToListAsync();
        return rules.Select(AssessmentMapper.ToDto).ToList();
    }

    [HttpPut("rules/{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<DeviationRuleDto>> UpdateRule(Guid id, UpdateDeviationRuleRequest request)
    {
        try
        {
            return AssessmentMapper.ToDto(await assessment.UpdateRuleAsync(id, request, User.GetUserId()));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("applications")]
    public async Task<ActionResult<IReadOnlyList<ApplicationListItemDto>>> Applications() =>
        (await assessment.ListApplicationsAsync()).Select(AssessmentMapper.ToListItem).ToList();

    [HttpPost("applications")]
    public async Task<ActionResult<ApplicationDetailDto>> CreateApplication(CreateApplicationRequest request)
    {
        try
        {
            var app = await assessment.CreateApplicationAsync(request, User.GetUserId());
            return Created($"/api/assessment/applications/{app.Id}", AssessmentMapper.ToDetail(app));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("applications/{id:guid}")]
    public async Task<ActionResult<ApplicationDetailDto>> Application(Guid id)
    {
        try
        {
            var app = await assessment.LoadApplicationAsync(id);
            var list = (await sessions.ListForApplicationAsync(id)).Select(sessions.ToListItem).ToList();
            return AssessmentMapper.ToDetail(app, list);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("sessions/ensure")]
    public async Task<ActionResult<SessionDetailDto>> EnsureSession(EnsureSessionRequest request)
    {
        try
        {
            var session = await sessions.EnsureAsync(request, User.GetUserId());
            return await sessions.ToDetailAsync(session);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<SessionDetailDto>> Session(Guid id)
    {
        try
        {
            return await sessions.ToDetailAsync(await sessions.LoadAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("sessions/{id:guid}/tasks/{number:int}")]
    public async Task<ActionResult<SessionDetailDto>> SaveTask(Guid id, int number, SaveTaskAnswerRequest request)
    {
        try
        {
            return await sessions.ToDetailAsync(await sessions.SaveAnswerAsync(id, number, request));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("sessions/{id:guid}/complete")]
    public async Task<ActionResult<SessionDetailDto>> CompleteSession(Guid id)
    {
        try
        {
            return await sessions.ToDetailAsync(await sessions.CompleteAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("applications/{id:guid}/applicants")]
    public async Task<ActionResult<ApplicantDto>> AddApplicant(Guid id, AddApplicantRequest request)
    {
        try
        {
            var applicant = await assessment.AddApplicantAsync(id, request);
            var loaded = await assessment.LoadApplicationAsync(id);
            return Created($"/api/assessment/applicants/{applicant.Id}",
                AssessmentMapper.ToDto(loaded.Applicants.First(a => a.Id == applicant.Id)));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<TemplateListItemDto>>> Templates()
    {
        var templates = await memos.Templates.OrderBy(t => t.Code).ThenByDescending(t => t.Version).ToListAsync();
        return templates.Select(AssessmentMapper.ToListItem).ToList();
    }

    [HttpGet("templates/{id:guid}")]
    public async Task<ActionResult<TemplateDetailDto>> Template(Guid id)
    {
        try
        {
            return AssessmentMapper.ToDetail(await memos.LoadTemplateAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("templates/{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TemplateDetailDto>> UpdateTemplate(Guid id, UpsertTemplateRequest request)
    {
        try
        {
            return AssessmentMapper.ToDetail(await memos.UpdateTemplateAsync(id, request, User.GetUserId()));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("templates/{id:guid}/clone")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TemplateDetailDto>> CloneTemplate(Guid id, CloneTemplateRequest request)
    {
        try
        {
            var template = await memos.CloneAsync(id, request, User.GetUserId());
            return Created($"/api/assessment/templates/{template.Id}", AssessmentMapper.ToDetail(template));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("templates/{id:guid}/fields/{number:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TemplateDetailDto>> UpdateTemplateField(Guid id, int number, UpdateTemplateFieldRequest request)
    {
        try
        {
            await memos.UpdateFieldAsync(id, number, request, User.GetUserId());
            return AssessmentMapper.ToDetail(await memos.LoadTemplateAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("applications/{id:guid}/memos")]
    public async Task<ActionResult<IReadOnlyList<MemoListItemDto>>> Memos(Guid id)
    {
        if (!await db.AssessmentApplications.AnyAsync(a => a.Id == id))
            return NotFound();
        var list = await memos.ListForApplicationAsync(id);
        return list.Select(memos.ToListItem).ToList();
    }

    [HttpPost("applications/{id:guid}/memos")]
    public async Task<ActionResult<MemoDetailDto>> CreateMemo(Guid id, CreateMemoRequest request)
    {
        try
        {
            var memo = await memos.EnsureAsync(id, request.SessionId, User.GetUserId());
            return Created($"/api/assessment/memos/{memo.Id}", memos.ToDetail(await memos.LoadAsync(memo.Id)));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("memos/{id:guid}")]
    public async Task<ActionResult<MemoDetailDto>> Memo(Guid id)
    {
        try
        {
            return memos.ToDetail(await memos.LoadAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("memos/{id:guid}/process")]
    public async Task<ActionResult<MemoDetailDto>> ProcessMemo(Guid id)
    {
        try
        {
            return memos.ToDetail(await memos.ProcessAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("memos/{id:guid}/fields/{number:int}")]
    public async Task<ActionResult<MemoDetailDto>> SaveMemoField(Guid id, int number, SaveMemoFieldRequest request)
    {
        try
        {
            return memos.ToDetail(await memos.SaveManualAsync(id, number, request.Value));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("memos/{id:guid}/download")]
    public async Task<IActionResult> DownloadMemo(Guid id)
    {
        try
        {
            var (name, body) = await memos.DownloadAsync(id);
            return File(body, "application/json", name);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("applicants/{id:guid}/inputs")]
    [RequestSizeLimit(4_000_000)]
    public async Task<ActionResult<ApplicantDto>> UploadInput(Guid id, UploadApplicantInputRequest request)
    {
        try
        {
            return AssessmentMapper.ToDto(await assessment.UploadInputAsync(id, request, User.GetUserId()));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
