using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Services;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Application.Services;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;
using RiskGuard.Persistence;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public sealed class AiController(IAiRiskService aiService, RiskGuardDbContext db) : ControllerBase
{
    private static readonly HashSet<string> ResponseTypes =
    [
        "Executive summary",
        "Technical analysis",
        "Risk explanation",
        "Mitigation plan",
        "Compliance summary",
        "Board report summary",
        "Department risk summary",
        "Vendor risk explanation",
        "Business continuity recommendation"
    ];

    [HttpGet("status")]
    public ActionResult<AiProviderStatus> Status() =>
        Ok(new AiProviderStatus(
            aiService.IsConfigured,
            aiService.IsConfigured ? "Azure OpenAI" : "Safe mock"));

    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyCollection<AiRecentInsightDto>>> Recent(
        [FromQuery] int take = 6,
        CancellationToken cancellationToken = default)
    {
        var interactions = await db.AiInteractions.AsNoTracking()
            .Where(item => item.UserId == User.UserId())
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 20))
            .ToListAsync(cancellationToken);
        var result = interactions.Select(ToRecentInsight).Where(item => item is not null).Cast<AiRecentInsightDto>();
        return Ok(result);
    }

    [HttpPost("risk-summary")]
    public Task<ActionResult<AiChatResponse>> RiskSummary(
        AiRiskSummaryRequest request,
        CancellationToken cancellationToken) =>
        Generate(
            request.Focus ?? "What is our biggest risk?",
            "Risk explanation",
            "General risk",
            request.AssessmentId,
            "AI summary generated",
            cancellationToken);

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost("recommendations")]
    public Task<ActionResult<AiChatResponse>> Recommendations(
        AiRecommendationRequest request,
        CancellationToken cancellationToken) =>
        Generate(
            request.Focus ?? "What should we fix first?",
            "Mitigation plan",
            "Recommendations",
            request.AssessmentId,
            "AI recommendation generated",
            cancellationToken);

    [HttpPost("copilot-chat")]
    public Task<ActionResult<AiChatResponse>> Chat(
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        var category = ClassifyPrompt(request.Prompt, request.ResponseType);
        if (!CanUseCategory(category))
        {
            return Task.FromResult<ActionResult<AiChatResponse>>(Forbid());
        }
        return Generate(
            request.Prompt,
            request.ResponseType,
            category,
            request.AssessmentId,
            "AI chat used",
            cancellationToken);
    }

    [Authorize(Policy = "AiExecutive")]
    [HttpPost("executive-summary")]
    public Task<ActionResult<AiChatResponse>> ExecutiveSummary(
        AiRiskSummaryRequest request,
        CancellationToken cancellationToken) =>
        Generate(
            request.Focus ?? "Generate an executive board summary.",
            "Executive summary",
            "Executive",
            request.AssessmentId,
            "AI summary generated",
            cancellationToken);

    [Authorize(Policy = "AiMitigation")]
    [HttpPost("mitigation-plan")]
    public Task<ActionResult<AiChatResponse>> MitigationPlan(
        AiMitigationPlanRequest request,
        CancellationToken cancellationToken) =>
        Generate(
            request.Focus ?? "Generate a mitigation plan.",
            "Mitigation plan",
            "Mitigation",
            request.AssessmentId,
            "AI recommendation generated",
            cancellationToken);

    [Authorize(Policy = "AiCompliance")]
    [HttpPost("compliance-summary")]
    public Task<ActionResult<AiChatResponse>> ComplianceSummary(
        AiComplianceSummaryRequest request,
        CancellationToken cancellationToken) =>
        Generate(
            request.Framework is null
                ? "Explain our compliance gaps."
                : $"Explain our compliance gaps for {request.Framework}.",
            "Compliance summary",
            "Compliance",
            request.AssessmentId,
            "AI summary generated",
            cancellationToken);

    private async Task<ActionResult<AiChatResponse>> Generate(
        string prompt,
        string responseType,
        string promptCategory,
        Guid? assessmentId,
        string auditAction,
        CancellationToken cancellationToken)
    {
        var sanitizedPrompt = AiPromptSecurity.Sanitize(prompt ?? string.Empty);
        if (string.IsNullOrWhiteSpace(sanitizedPrompt))
        {
            return BadRequest(new { message = "A prompt or focus is required." });
        }
        if (!ResponseTypes.Contains(responseType))
        {
            return BadRequest(new { message = "The requested AI response type is not supported." });
        }

        var context = await BuildContextAsync(assessmentId, cancellationToken);
        if (context is null)
        {
            return NotFound(new { message = "The related assessment was not found or is not accessible." });
        }

        var generationRequest = new AiGenerationRequest(
            sanitizedPrompt,
            responseType,
            promptCategory,
            assessmentId);
        var response = await aiService.GenerateAsync(generationRequest, context, cancellationToken);
        var interaction = new AiInteraction
        {
            UserId = User.UserId(),
            Prompt = sanitizedPrompt,
            Response = JsonSerializer.Serialize(response),
            ResponseType = response.ResponseType,
            UsedConfiguredProvider = !response.IsMock
        };
        db.AiInteractions.Add(interaction);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = User.UserId(),
            UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "unknown",
            Action = auditAction,
            EntityType = assessmentId.HasValue ? "Assessment" : "AiInteraction",
            EntityId = assessmentId?.ToString() ?? interaction.Id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Description = $"Prompt category: {promptCategory}; response type: {responseType}; mode: {(response.IsMock ? "Safe mock" : "Azure OpenAI")}."
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(response);
    }

    private async Task<AiRiskContext?> BuildContextAsync(Guid? assessmentId, CancellationToken cancellationToken)
    {
        var organizationId = User.OrganizationId();
        if (!organizationId.HasValue)
        {
            return EmptyContext();
        }

        var organizationName = await db.Organizations.AsNoTracking()
            .Where(item => item.Id == organizationId.Value)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Current organization";

        var risksQuery = db.Risks.AsNoTracking()
            .Include(item => item.Assessment)
            .Include(item => item.Department)
            .Where(item => item.Assessment != null && item.Assessment.OrganizationId == organizationId.Value);
        if (User.IsInRole("Employee"))
        {
            risksQuery = risksQuery.Where(item => item.Assessment!.AssignedToUserId == User.UserId());
        }
        else if (User.IsInRole("Department Manager") && User.DepartmentId().HasValue)
        {
            risksQuery = risksQuery.Where(item => item.DepartmentId == User.DepartmentId());
        }
        var risks = (await risksQuery.ToListAsync(cancellationToken))
            .OrderByDescending(item => item.Score)
            .ToList();
        var riskIds = risks.Select(item => item.Id).ToArray();
        var visibleAssessmentIds = risks.Select(item => item.AssessmentId).Distinct().ToArray();

        Assessment? assessment = null;
        if (assessmentId.HasValue)
        {
            assessment = await db.Assessments.AsNoTracking()
                .Include(item => item.RiskCategory)
                .Include(item => item.Department)
                .Include(item => item.Responses).ThenInclude(item => item.Question)
                .FirstOrDefaultAsync(item =>
                    item.Id == assessmentId.Value &&
                    item.OrganizationId == organizationId.Value &&
                    (!User.IsInRole("Employee") || item.AssignedToUserId == User.UserId()) &&
                    (!User.IsInRole("Department Manager") || item.DepartmentId == User.DepartmentId()),
                    cancellationToken);
            if (assessment is null)
            {
                return null;
            }
            visibleAssessmentIds = visibleAssessmentIds.Append(assessment.Id).Distinct().ToArray();
        }

        var recommendations = await db.Recommendations.AsNoTracking()
            .Where(item =>
                item.AssessmentId.HasValue && visibleAssessmentIds.Contains(item.AssessmentId.Value) ||
                item.RiskItemId.HasValue && riskIds.Contains(item.RiskItemId.Value))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.DueDateUtc)
            .Take(12)
            .ToListAsync(cancellationToken);

        var gaps = await db.ComplianceGaps.AsNoTracking()
            .Include(item => item.Control).ThenInclude(item => item!.Framework)
            .Where(item => item.RiskItemId.HasValue && riskIds.Contains(item.RiskItemId.Value) && item.Status != "Closed")
            .OrderByDescending(item => item.Severity)
            .ToListAsync(cancellationToken);

        var incidentsQuery = db.Incidents.AsNoTracking()
            .Include(item => item.Department)
            .Include(item => item.RelatedRisk).ThenInclude(item => item!.Assessment)
            .Where(item =>
                item.Department != null && item.Department.OrganizationId == organizationId.Value ||
                item.RelatedRisk != null && item.RelatedRisk.Assessment != null &&
                item.RelatedRisk.Assessment.OrganizationId == organizationId.Value);
        if (User.IsInRole("Employee") || User.IsInRole("Department Manager"))
        {
            incidentsQuery = incidentsQuery.Where(item => item.DepartmentId == User.DepartmentId());
        }
        var incidents = await incidentsQuery
            .Where(item => item.Status != IncidentStatus.Resolved && item.Status != IncidentStatus.Closed)
            .OrderByDescending(item => item.Severity)
            .ToListAsync(cancellationToken);

        var canReadSensitive = User.IsInRole("Admin") || User.IsInRole("Executive") ||
            User.IsInRole("Risk Manager") || User.IsInRole("Auditor") ||
            User.IsInRole("Compliance Officer") || User.IsInRole("Security Analyst") ||
            User.IsInRole("Department Manager");
        var vendors = canReadSensitive
            ? (await db.Vendors.AsNoTracking()
                .Where(item => item.OrganizationId == organizationId.Value)
                .ToListAsync(cancellationToken))
                .OrderByDescending(item => item.RiskScore)
                .ToList()
            : [];
        var continuityPlans = canReadSensitive
            ? (await db.BusinessContinuityPlans.AsNoTracking()
                .Include(item => item.CriticalSystems)
                .Where(item => item.OrganizationId == organizationId.Value)
                .ToListAsync(cancellationToken))
                .OrderBy(item => item.ContinuityScore)
                .ToList()
            : [];

        var controls = await db.ComplianceControls.CountAsync(cancellationToken);
        var overallScore = risks.Count == 0 ? assessment?.Score ?? 0 : Math.Round(risks.Average(item => item.Score), 2);
        var riskLevel = ToRiskLevel(overallScore);
        var categoryScores = Enum.GetValues<RiskCategoryType>()
            .Select(category => new CategoryScore(
                category.ToString(),
                Math.Round(risks.Where(item => item.Category == category).Select(item => item.Score).DefaultIfEmpty(0).Average(), 2)))
            .ToArray();
        var complianceReadiness = controls == 0
            ? 0
            : Math.Round(Math.Max(0, (controls - gaps.Count) * 100m / controls), 2);
        var continuityScore = continuityPlans.Count == 0
            ? 0
            : Math.Round(continuityPlans.Average(item => item.ContinuityScore), 2);
        var summary = new AiRiskContextSummary(
            overallScore,
            riskLevel,
            risks.Count(item => item.RiskLevel == RiskLevel.Critical),
            risks.Count(item => item.RiskLevel == RiskLevel.High),
            complianceReadiness,
            gaps.Count,
            incidents.Count,
            vendors.Count(item => item.RiskLevel is RiskLevel.High or RiskLevel.Critical),
            continuityScore,
            categoryScores);

        return new AiRiskContext(
            organizationName,
            summary,
            risks.Take(10).Select(item => new AiRiskItemContext(
                item.Title,
                item.Category.ToString(),
                item.Score,
                item.RiskLevel,
                item.Department?.Name ?? "Enterprise",
                item.Owner)).ToArray(),
            recommendations.Select(item => new AiRecommendationContext(
                item.Title,
                item.Priority,
                item.SuggestedOwner,
                item.DueDateUtc,
                item.Status)).ToArray(),
            gaps.Take(12).Select(item => new AiComplianceGapContext(
                item.Control?.Framework?.Name ?? "Unmapped",
                item.Control?.Code ?? "Unmapped",
                item.Description,
                item.Severity,
                item.Owner,
                item.Status)).ToArray(),
            incidents.Take(10).Select(item => new AiIncidentContext(
                item.Title,
                item.Severity,
                item.Status,
                item.Owner)).ToArray(),
            vendors.Take(10).Select(item => new AiVendorContext(
                item.Name,
                item.ServiceProvided,
                item.RiskScore,
                item.RiskLevel,
                item.ComplianceStatus)).ToArray(),
            continuityPlans.Take(10).Select(item => new AiContinuityContext(
                item.Name,
                item.ContinuityScore,
                item.Status.ToString(),
                item.CriticalSystems.Count(system =>
                    !system.LastDisasterRecoveryTestDateUtc.HasValue ||
                    system.LastDisasterRecoveryTestDateUtc < DateTime.UtcNow.AddYears(-1)))).ToArray(),
            assessment is null
                ? null
                : new AiAssessmentContext(
                    assessment.Id,
                    assessment.Title,
                    assessment.RiskCategory?.Name ?? "Uncategorized",
                    assessment.Department?.Name ?? "Enterprise",
                    assessment.Status,
                    assessment.Score,
                    assessment.RiskLevel,
                    assessment.Responses.Select(item => new AiAssessmentAnswerContext(
                        item.Question?.Text ?? "Assessment question",
                        item.Answer,
                        item.AnswerScore,
                        item.Question?.Weight ?? 1)).ToArray()));
    }

    private bool CanUseCategory(string category) => category switch
    {
        "Executive" => User.IsInRole("Admin") || User.IsInRole("Executive") ||
            User.IsInRole("Risk Manager") || User.IsInRole("Auditor"),
        "Compliance" => User.IsInRole("Admin") || User.IsInRole("Risk Manager") ||
            User.IsInRole("Compliance Officer") || User.IsInRole("Auditor"),
        "Cybersecurity" => User.IsInRole("Admin") || User.IsInRole("Risk Manager") ||
            User.IsInRole("Security Analyst"),
        "Mitigation" or "Recommendations" => User.IsInRole("Admin") ||
            User.IsInRole("Risk Manager") || User.IsInRole("Security Analyst") ||
            User.IsInRole("Compliance Officer") || User.IsInRole("Department Manager"),
        "Vendor" or "Business continuity" => !User.IsInRole("Employee"),
        _ => true
    };

    private static string ClassifyPrompt(string prompt, string responseType)
    {
        var text = $"{prompt} {responseType}".ToLowerInvariant();
        if (text.Contains("executive") || text.Contains("board")) return "Executive";
        if (text.Contains("compliance") || text.Contains("regulatory") || text.Contains("gap")) return "Compliance";
        if (text.Contains("cyber") || text.Contains("security exposure")) return "Cybersecurity";
        if (text.Contains("vendor") || text.Contains("supplier")) return "Vendor";
        if (text.Contains("continuity") || text.Contains("recovery") || text.Contains("resilience")) return "Business continuity";
        if (text.Contains("mitigation") || text.Contains("fix first") || text.Contains("recommend")) return "Mitigation";
        if (text.Contains("department") || text.Contains("urgent attention")) return "Department";
        return "General risk";
    }

    private static AiRecentInsightDto? ToRecentInsight(AiInteraction interaction)
    {
        try
        {
            var response = JsonSerializer.Deserialize<AiChatResponse>(
                interaction.Response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return response is null
                ? null
                : new AiRecentInsightDto(
                    interaction.Id,
                    response.Title,
                    response.Summary,
                    response.ResponseType,
                    response.IsMock,
                    response.GeneratedAtUtc);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RiskLevel ToRiskLevel(decimal score) =>
        score <= 25 ? RiskLevel.Low :
        score <= 50 ? RiskLevel.Medium :
        score <= 75 ? RiskLevel.High :
        RiskLevel.Critical;

    private static AiRiskContext EmptyContext() =>
        new(
            "Current organization",
            new AiRiskContextSummary(0, RiskLevel.Low, 0, 0, 0, 0, 0, 0, 0, []),
            [],
            [],
            [],
            [],
            [],
            [],
            null);
}
