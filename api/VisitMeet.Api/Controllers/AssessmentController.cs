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
public class AssessmentController(AppDbContext db, AssessmentService assessment) : ControllerBase
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
            return AssessmentMapper.ToDetail(await assessment.LoadApplicationAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
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
