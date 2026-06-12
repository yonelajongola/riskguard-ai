using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Services;
using RiskGuard.Application.DTOs;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;
using RiskGuard.Persistence;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/recommendations")]
public sealed class RecommendationsController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] RecommendationStatus? status)
    {
        var organizationId = User.OrganizationId();
        var query = db.Recommendations.AsNoTracking()
            .Include(x => x.RiskItem)
            .Where(x =>
                x.AssessmentId.HasValue &&
                db.Assessments.Any(a => a.Id == x.AssessmentId && a.OrganizationId == organizationId) ||
                x.RiskItem != null && x.RiskItem.Assessment != null &&
                x.RiskItem.Assessment.OrganizationId == organizationId);
        if (User.IsInRole("Employee"))
        {
            query = query.Where(x => x.AssessmentId.HasValue &&
                db.Assessments.Any(a =>
                    a.Id == x.AssessmentId &&
                    a.AssignedToUserId == User.UserId()));
        }
        else if (User.IsInRole("Department Manager") && User.DepartmentId().HasValue)
        {
            query = query.Where(x => x.RiskItem != null && x.RiskItem.DepartmentId == User.DepartmentId());
        }
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return Ok(await query.OrderBy(x => x.Priority).ThenBy(x => x.DueDateUtc).ToListAsync());
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost]
    public async Task<ActionResult> Create(Recommendation recommendation)
    {
        if (!await IsOwnedAsync(recommendation.AssessmentId, recommendation.RiskItemId))
        {
            return BadRequest(new { message = "Recommendation must reference a risk or assessment in this workspace." });
        }
        recommendation.Id = Guid.NewGuid();
        recommendation.CreatedAtUtc = DateTime.UtcNow;
        db.Recommendations.Add(recommendation);
        await db.SaveChangesAsync();
        return Created($"/api/recommendations/{recommendation.Id}", recommendation);
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, RecommendationUpdate request)
    {
        var recommendation = await FindOwnedAsync(id);
        if (recommendation is null) return NotFound();
        recommendation.Status = request.Status;
        recommendation.SuggestedOwner = request.Owner.Trim();
        recommendation.DueDateUtc = request.DueDateUtc;
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Recommendation updated", "Recommendation", id, $"Status changed to {request.Status}.");
        return NoContent();
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var recommendation = await FindOwnedAsync(id);
        if (recommendation is null) return NotFound();
        recommendation.Status = RecommendationStatus.Completed;
        await db.SaveChangesAsync();
        return NoContent();
    }

    public sealed record RecommendationUpdate(RecommendationStatus Status, string Owner, DateTime DueDateUtc);

    private Task<Recommendation?> FindOwnedAsync(Guid id) =>
        db.Recommendations.FirstOrDefaultAsync(x => x.Id == id &&
            (x.AssessmentId.HasValue &&
             db.Assessments.Any(a => a.Id == x.AssessmentId && a.OrganizationId == User.OrganizationId()) ||
             x.RiskItem != null && x.RiskItem.Assessment != null &&
             x.RiskItem.Assessment.OrganizationId == User.OrganizationId()));

    private async Task<bool> IsOwnedAsync(Guid? assessmentId, Guid? riskItemId) =>
        assessmentId.HasValue &&
        await db.Assessments.AnyAsync(x => x.Id == assessmentId && x.OrganizationId == User.OrganizationId()) ||
        riskItemId.HasValue &&
        await db.Risks.AnyAsync(x => x.Id == riskItemId && x.Assessment != null &&
            x.Assessment.OrganizationId == User.OrganizationId());
}

[ApiController]
[Authorize(Policy = "ReadSensitive")]
[Route("api/compliance")]
public sealed class ComplianceController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet("frameworks")]
    public async Task<ActionResult> Frameworks() =>
        Ok(await db.ComplianceFrameworks.AsNoTracking().Include(x => x.Controls).OrderBy(x => x.Name).ToListAsync());

    [HttpGet("gaps")]
    public async Task<ActionResult> Gaps() =>
        Ok(await db.ComplianceGaps.AsNoTracking()
            .Include(x => x.Control).ThenInclude(x => x!.Framework)
            .Include(x => x.RelatedRisk)
            .Where(x => x.RelatedRisk != null && x.RelatedRisk.Assessment != null &&
                x.RelatedRisk.Assessment.OrganizationId == User.OrganizationId())
            .OrderByDescending(x => x.Severity).ThenBy(x => x.DueDateUtc).ToListAsync());

    [HttpGet("dashboard")]
    public async Task<ActionResult<ComplianceDashboard>> Dashboard()
    {
        var frameworks = await db.ComplianceFrameworks.AsNoTracking().Include(x => x.Controls).ToListAsync();
        var gaps = await db.ComplianceGaps.AsNoTracking().Where(x =>
            x.Status != "Closed" &&
            x.RelatedRisk != null &&
            x.RelatedRisk.Assessment != null &&
            x.RelatedRisk.Assessment.OrganizationId == User.OrganizationId()).ToListAsync();
        var total = frameworks.Sum(x => x.Controls.Count);
        var failed = gaps.Count;
        var passed = Math.Max(0, total - failed);
        var readiness = total == 0 ? 0 : Math.Round(passed * 100m / total, 2);
        var scores = frameworks.Select(framework =>
        {
            var controlIds = framework.Controls.Select(x => x.Id).ToHashSet();
            var frameworkGaps = gaps.Count(x => controlIds.Contains(x.ControlId));
            var score = framework.Controls.Count == 0 ? 0 : (framework.Controls.Count - frameworkGaps) * 100m / framework.Controls.Count;
            return new CategoryScore(framework.Name, Math.Round(score, 2));
        }).ToArray();
        return Ok(new ComplianceDashboard(readiness, passed, failed, failed, scores));
    }

    [Authorize(Roles = "Admin,Compliance Officer,Risk Manager")]
    [HttpPost("gaps")]
    public async Task<ActionResult> CreateGap(ComplianceGapRequest request)
    {
        if (!await db.ComplianceControls.AnyAsync(x => x.Id == request.ControlId) ||
            !await db.Risks.AnyAsync(x => x.Id == request.RiskItemId &&
                x.Assessment != null && x.Assessment.OrganizationId == User.OrganizationId()))
        {
            return BadRequest(new { message = "Control or related risk is invalid." });
        }
        var gap = new ComplianceGap
        {
            ControlId = request.ControlId,
            RiskItemId = request.RiskItemId,
            Description = request.Description.Trim(),
            Severity = request.Severity,
            Recommendation = request.Recommendation.Trim(),
            Owner = request.Owner.Trim(),
            DueDateUtc = request.DueDateUtc
        };
        db.ComplianceGaps.Add(gap);
        await db.SaveChangesAsync();
        return Created($"/api/compliance/gaps/{gap.Id}", gap);
    }

    [Authorize(Roles = "Admin,Compliance Officer,Risk Manager")]
    [HttpPut("gaps/{id:guid}")]
    public async Task<IActionResult> UpdateGap(Guid id, ComplianceGapUpdate request)
    {
        var gap = await db.ComplianceGaps.FirstOrDefaultAsync(x => x.Id == id &&
            x.RelatedRisk != null && x.RelatedRisk.Assessment != null &&
            x.RelatedRisk.Assessment.OrganizationId == User.OrganizationId());
        if (gap is null) return NotFound();
        gap.Status = request.Status;
        gap.Owner = request.Owner;
        gap.DueDateUtc = request.DueDateUtc;
        await db.SaveChangesAsync();
        if (request.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
        {
            await db.WriteAuditAsync(User, HttpContext, "Compliance gap closed", "ComplianceGap", id, gap.Description);
        }
        return NoContent();
    }

    public sealed record ComplianceGapUpdate(string Status, string Owner, DateTime DueDateUtc);
    public sealed record ComplianceGapRequest(
        Guid ControlId,
        Guid RiskItemId,
        string Description,
        Severity Severity,
        string Recommendation,
        string Owner,
        DateTime DueDateUtc);
}
