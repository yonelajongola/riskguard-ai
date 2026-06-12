using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RiskGuard.API.Services;
using RiskGuard.Application.DTOs;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;
using RiskGuard.Persistence;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/risk-categories")]
public sealed class RiskCategoriesController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll() =>
        Ok(await db.RiskCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync());
}

[ApiController]
[Authorize]
[Route("api/questions")]
public sealed class QuestionsController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll() =>
        Ok(await db.AssessmentQuestions.AsNoTracking().Include(x => x.RiskCategory).OrderBy(x => x.RiskCategory!.Name).ThenBy(x => x.Text).ToListAsync());

    [HttpGet("category/{category}")]
    public async Task<ActionResult> GetByCategory(RiskCategoryType category) =>
        Ok(await db.AssessmentQuestions.AsNoTracking()
            .Include(x => x.RiskCategory)
            .Where(x => x.RiskCategory!.Type == category && x.IsActive)
            .OrderBy(x => x.Text).ToListAsync());

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost]
    public async Task<ActionResult> Create(QuestionRequest request)
    {
        var validation = await ValidateRequestAsync(request);
        if (validation is not null) return validation;
        var question = new AssessmentQuestion
        {
            RiskCategoryId = request.RiskCategoryId,
            Text = request.Text.Trim(),
            Weight = request.Weight,
            AnswerType = request.AnswerType,
            ScoreMappingJson = request.ScoreMappingJson,
            RecommendationText = request.RecommendationText,
            ComplianceMappings = request.ComplianceMappings,
            SeverityImpact = request.SeverityImpact
        };
        db.AssessmentQuestions.Add(question);
        await db.SaveChangesAsync();
        return Created($"/api/questions/{question.Id}", question);
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, QuestionRequest request)
    {
        var validation = await ValidateRequestAsync(request);
        if (validation is not null) return validation;
        var question = await db.AssessmentQuestions.FindAsync(id);
        if (question is null) return NotFound();
        question.Text = request.Text.Trim();
        question.Weight = request.Weight;
        question.AnswerType = request.AnswerType;
        question.ScoreMappingJson = request.ScoreMappingJson;
        question.RecommendationText = request.RecommendationText;
        question.ComplianceMappings = request.ComplianceMappings;
        question.SeverityImpact = request.SeverityImpact;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var question = await db.AssessmentQuestions.FindAsync(id);
        if (question is null) return NotFound();
        question.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    public sealed record QuestionRequest(
        Guid RiskCategoryId, string Text, decimal Weight, AnswerType AnswerType,
        string ScoreMappingJson, string RecommendationText, string ComplianceMappings, Severity SeverityImpact);

    private async Task<BadRequestObjectResult?> ValidateRequestAsync(QuestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 500 ||
            request.Weight <= 0 || request.Weight > 10 ||
            !await db.RiskCategories.AnyAsync(x => x.Id == request.RiskCategoryId))
        {
            return BadRequest(new { message = "Question category, text, or weight is invalid." });
        }
        try
        {
            var mapping = JsonSerializer.Deserialize<Dictionary<string, decimal>>(request.ScoreMappingJson);
            if (mapping is null || mapping.Count == 0 || mapping.Values.Any(value => value is < 0 or > 100))
            {
                return BadRequest(new { message = "Score mapping must contain answer scores between 0 and 100." });
            }
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Score mapping must be valid JSON." });
        }
        return null;
    }
}

[ApiController]
[Authorize]
[Route("api/risks")]
public sealed class RisksController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var organizationId = User.OrganizationId();
        if (!organizationId.HasValue) return Ok(Array.Empty<RiskItem>());
        var query = db.Risks.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Assessment)
            .Where(x => x.Assessment != null && x.Assessment.OrganizationId == organizationId.Value);
        if (User.IsInRole("Employee"))
        {
            query = query.Where(x => x.Assessment!.AssignedToUserId == User.UserId());
        }
        else if (User.IsInRole("Department Manager") && User.DepartmentId().HasValue)
        {
            query = query.Where(x => x.DepartmentId == User.DepartmentId());
        }
        var risks = await query.ToListAsync();
        return Ok(risks.OrderByDescending(x => x.Score));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id)
    {
        var organizationId = User.OrganizationId();
        var risk = await db.Risks.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Recommendations)
            .Include(x => x.Assessment)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Assessment != null &&
                x.Assessment.OrganizationId == organizationId &&
                (!User.IsInRole("Employee") || x.Assessment.AssignedToUserId == User.UserId()) &&
                (!User.IsInRole("Department Manager") || x.DepartmentId == User.DepartmentId()));
        return risk is null ? NotFound() : Ok(risk);
    }

    [HttpGet("dashboard-summary")]
    public async Task<ActionResult<DashboardSummary>> DashboardSummary()
    {
        var organizationId = User.OrganizationId();
        if (!organizationId.HasValue)
        {
            return Ok(EmptyDashboard());
        }
        var riskQuery = db.Risks.AsNoTracking()
            .Include(x => x.Department)
            .Where(x => x.Assessment != null && x.Assessment.OrganizationId == organizationId.Value);
        if (User.IsInRole("Employee"))
        {
            riskQuery = riskQuery.Where(x => x.Assessment!.AssignedToUserId == User.UserId());
        }
        else if (User.IsInRole("Department Manager") && User.DepartmentId().HasValue)
        {
            riskQuery = riskQuery.Where(x => x.DepartmentId == User.DepartmentId());
        }
        var risks = await riskQuery.ToListAsync();
        var score = risks.Count == 0 ? 0 : Math.Round(risks.Average(x => x.Score), 2);
        var latestContinuity = await db.BusinessContinuityPlans.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId.Value)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();
        var vendorScore = await db.Vendors.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId.Value)
            .Select(x => (decimal?)x.RiskScore)
            .AverageAsync() ?? 0;
        var scoreHistory = await db.RiskScores.AsNoTracking()
            .Where(x => db.Assessments.Any(assessment =>
                assessment.Id == x.AssessmentId &&
                assessment.OrganizationId == organizationId.Value))
            .OrderBy(x => x.CalculatedAtUtc)
            .ToListAsync();
        var trend = scoreHistory
            .GroupBy(x => new { x.CalculatedAtUtc.Year, x.CalculatedAtUtc.Month })
            .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month)
            .TakeLast(6)
            .Select(x => new ChartPoint(
                new DateTime(x.Key.Year, x.Key.Month, 1).ToString("MMM"),
                Math.Round(x.Average(item => item.OverallScore), 2)))
            .ToArray();
        var categories = Enum.GetValues<RiskCategoryType>().Select(category => new CategoryScore(
            category.ToString(),
            risks.Where(x => x.Category == category).Select(x => x.Score).DefaultIfEmpty(0).Average())).ToArray();
        var summary = new DashboardSummary(
            score,
            score <= 25 ? RiskLevel.Low : score <= 50 ? RiskLevel.Medium : score <= 75 ? RiskLevel.High : RiskLevel.Critical,
            risks.Count(x => x.RiskLevel == RiskLevel.Critical),
            risks.Count(x => x.RiskLevel == RiskLevel.High),
            await ComplianceReadinessAsync(organizationId.Value),
            latestContinuity?.ContinuityScore ?? 0,
            risks.Sum(x => x.FinancialExposure),
            Math.Round(vendorScore, 2),
            trend.Length == 0 ? [new ChartPoint(DateTime.UtcNow.ToString("MMM"), score)] : trend,
            categories);
        return Ok(summary);
    }

    [HttpGet("heatmap")]
    public async Task<ActionResult> HeatMap() =>
        Ok(await db.Risks.AsNoTracking().Include(x => x.Department)
            .Where(x => x.Assessment != null &&
                x.Assessment.OrganizationId == User.OrganizationId() &&
                (!User.IsInRole("Employee") || x.Assessment.AssignedToUserId == User.UserId()) &&
                (!User.IsInRole("Department Manager") || x.DepartmentId == User.DepartmentId()))
            .Select(x => new HeatMapItem(x.Id, x.Title, x.Impact, x.Likelihood, x.RiskLevel, x.Department != null ? x.Department.Name : "Enterprise"))
            .ToListAsync());

    private async Task<decimal> ComplianceReadinessAsync(Guid organizationId)
    {
        var controls = await db.ComplianceControls.CountAsync();
        var gaps = await db.ComplianceGaps.CountAsync(x =>
            x.Status != "Closed" &&
            x.RelatedRisk != null &&
            x.RelatedRisk.Assessment != null &&
            x.RelatedRisk.Assessment.OrganizationId == organizationId);
        return controls == 0 ? 0 : Math.Round(Math.Max(0, (controls - gaps) * 100m / controls), 2);
    }

    private static DashboardSummary EmptyDashboard() =>
        new(0, RiskLevel.Low, 0, 0, 0, 0, 0, 0, [], []);
}
