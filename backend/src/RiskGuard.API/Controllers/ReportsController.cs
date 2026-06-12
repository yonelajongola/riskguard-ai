using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Enums;
using RiskGuard.Persistence;
using RiskGuard.API.Services;
using RiskGuard.Domain.Entities;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(RiskGuardDbContext db, IReportService reports) : ControllerBase
{
    [Authorize(Policy = "ReadSensitive")]
    [HttpGet("executive/pdf")]
    [HttpGet("compliance/pdf")]
    [HttpGet("vendors/pdf")]
    [HttpGet("incidents/pdf")]
    public async Task<IActionResult> ExecutivePdf()
    {
        var organization = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == User.OrganizationId());
        if (organization is null) return NotFound();
        var reportType = Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(2)
            ?? "executive";
        var title = reportType.ToLowerInvariant() switch
        {
            "compliance" => "Compliance Readiness Report",
            "vendors" => "Vendor Risk Report",
            "incidents" => "Incident Management Report",
            _ => "Executive Risk Report"
        };
        var summary = await BuildSummaryAsync();
        var bytes = reports.GenerateExecutivePdf(
            organization.Name,
            title,
            summary,
            User.Identity?.Name ?? "RiskGuard AI");
        var fileName = $"RiskGuard-{reportType}-report-{DateTime.UtcNow:yyyyMMdd}.pdf";
        await WriteReportAuditAsync(title, fileName, "PDF");
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet("risk/pdf/{assessmentId:guid}")]
    public async Task<IActionResult> AssessmentPdf(Guid assessmentId)
    {
        var assessment = await db.Assessments.AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Organization)
            .Include(x => x.Department)
            .Include(x => x.RiskCategory)
            .Include(x => x.Responses)
            .ThenInclude(x => x.Question)
            .Include(x => x.Risks)
            .ThenInclude(x => x.Recommendations)
            .FirstOrDefaultAsync(x => x.Id == assessmentId);
        if (assessment is null || !CanAccessAssessment(assessment))
        {
            return NotFound();
        }
        if (assessment.Status is not (AssessmentStatus.Submitted or AssessmentStatus.Reviewed or AssessmentStatus.Approved))
        {
            return Conflict(new { message = "The assessment must be submitted before a report can be generated." });
        }

        var riskIds = assessment.Risks.Select(x => x.Id).ToArray();
        var gaps = riskIds.Length == 0
            ? []
            : await db.ComplianceGaps.AsNoTracking()
                .Include(x => x.Control)
                .ThenInclude(x => x!.Framework)
                .Where(x => x.RiskItemId.HasValue && riskIds.Contains(x.RiskItemId.Value))
                .ToListAsync();
        var bytes = reports.GenerateAssessmentPdf(
            assessment.Organization?.Name ?? "RiskGuard workspace",
            assessment,
            gaps,
            User.Identity?.Name ?? "RiskGuard AI");
        var fileName = $"RiskGuard-assessment-{assessment.Id:N}.pdf";
        await WriteReportAuditAsync($"Assessment report: {assessment.Title}", fileName, "PDF");
        return File(bytes, "application/pdf", fileName);
    }

    [Authorize(Policy = "ReadSensitive")]
    [HttpGet("risks/excel")]
    public async Task<IActionResult> RiskExcel()
    {
        var risks = (await db.Risks.AsNoTracking()
            .Include(x => x.Department)
            .Where(x => x.Assessment != null && x.Assessment.OrganizationId == User.OrganizationId())
            .ToListAsync())
            .OrderByDescending(x => x.Score)
            .ToList();
        var fileName = $"RiskGuard-Risk-Register-{DateTime.UtcNow:yyyyMMdd}.xlsx";
        var bytes = reports.GenerateRiskRegisterExcel(risks);
        await WriteReportAuditAsync("Risk Register", fileName, "Excel");
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [Authorize(Roles = "Admin,Auditor")]
    [HttpGet("auditlogs/csv")]
    public async Task<IActionResult> AuditCsv()
    {
        var userIds = await db.Users.AsNoTracking()
            .Where(x => x.OrganizationId == User.OrganizationId())
            .Select(x => x.Id.ToString())
            .ToListAsync();
        var emails = await db.Users.AsNoTracking()
            .Where(x => x.OrganizationId == User.OrganizationId())
            .Select(x => x.Email!)
            .ToListAsync();
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) || emails.Contains(x.UserEmail))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
        var fileName = $"RiskGuard-Audit-Logs-{DateTime.UtcNow:yyyyMMdd}.csv";
        var bytes = reports.GenerateCsv(logs);
        await WriteReportAuditAsync("Audit Activity Log", fileName, "CSV");
        return File(bytes, "text/csv", fileName);
    }

    [Authorize(Policy = "ReadSensitive")]
    [HttpGet("{register}/csv")]
    public async Task<IActionResult> RegisterCsv(string register)
    {
        return register.ToLowerInvariant() switch
        {
            "risks" => File(reports.GenerateCsv(await db.Risks.AsNoTracking()
                .Where(x => x.Assessment != null && x.Assessment.OrganizationId == User.OrganizationId()).ToListAsync()), "text/csv", "risks.csv"),
            "assessments" => File(reports.GenerateCsv(await db.Assessments.AsNoTracking()
                .Where(x => x.OrganizationId == User.OrganizationId()).ToListAsync()), "text/csv", "assessments.csv"),
            "recommendations" => File(reports.GenerateCsv(await db.Recommendations.AsNoTracking()
                .Where(x => x.AssessmentId.HasValue && db.Assessments.Any(a =>
                    a.Id == x.AssessmentId && a.OrganizationId == User.OrganizationId())).ToListAsync()), "text/csv", "recommendations.csv"),
            "vendors" => File(reports.GenerateCsv(await db.Vendors.AsNoTracking()
                .Where(x => x.OrganizationId == User.OrganizationId()).ToListAsync()), "text/csv", "vendors.csv"),
            "incidents" => File(reports.GenerateCsv(await db.Incidents.AsNoTracking()
                .Where(x => x.Department != null && x.Department.OrganizationId == User.OrganizationId() ||
                    x.RelatedRisk != null && x.RelatedRisk.Assessment != null &&
                    x.RelatedRisk.Assessment.OrganizationId == User.OrganizationId()).ToListAsync()), "text/csv", "incidents.csv"),
            _ => NotFound()
        };
    }

    private async Task<DashboardSummary> BuildSummaryAsync()
    {
        var organizationId = User.OrganizationId();
        var risks = await db.Risks.AsNoTracking()
            .Where(x => x.Assessment != null && x.Assessment.OrganizationId == organizationId)
            .ToListAsync();
        var score = risks.Select(x => x.Score).DefaultIfEmpty(0).Average();
        var controls = await db.ComplianceControls.CountAsync();
        var gaps = await db.ComplianceGaps.CountAsync(x => x.Status != "Closed" &&
            x.RelatedRisk != null && x.RelatedRisk.Assessment != null &&
            x.RelatedRisk.Assessment.OrganizationId == organizationId);
        var compliance = controls == 0 ? 0 : (controls - gaps) * 100m / controls;
        var continuityScores = await db.BusinessContinuityPlans
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.ContinuityScore)
            .ToListAsync();
        var continuity = continuityScores.Count == 0 ? 0 : continuityScores.Average();
        var categories = Enum.GetValues<RiskCategoryType>()
            .Select(category => new CategoryScore(
                category.ToString(),
                risks.Where(x => x.Category == category).Select(x => x.Score).DefaultIfEmpty(0).Average()))
            .ToArray();
        var riskScoreHistory = await db.RiskScores.AsNoTracking()
            .Where(x => db.Assessments.Any(a => a.Id == x.AssessmentId && a.OrganizationId == organizationId))
            .OrderByDescending(x => x.CalculatedAtUtc)
            .Take(6)
            .OrderBy(x => x.CalculatedAtUtc)
            .ToListAsync();
        return new DashboardSummary(
            Math.Round(score, 2),
            score <= 25 ? RiskLevel.Low : score <= 50 ? RiskLevel.Medium : score <= 75 ? RiskLevel.High : RiskLevel.Critical,
            risks.Count(x => x.RiskLevel == RiskLevel.Critical),
            risks.Count(x => x.RiskLevel == RiskLevel.High),
            Math.Round(compliance, 2),
            Math.Round(continuity, 2),
            risks.Sum(x => x.FinancialExposure),
            await db.Vendors.Where(x => x.OrganizationId == organizationId)
                .Select(x => (decimal?)x.RiskScore).AverageAsync() ?? 0,
            riskScoreHistory.Select(x => new ChartPoint(x.CalculatedAtUtc.ToString("MMM"), x.OverallScore)).ToArray(),
            categories);
    }

    private async Task WriteReportAuditAsync(string title, string fileName, string type)
    {
        var report = new Report
        {
            Title = title,
            Type = type,
            FileName = fileName,
            PreparedBy = User.Identity?.Name ?? User.UserId()
        };
        db.Reports.Add(report);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            UserEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty,
            Action = "Report downloaded",
            EntityType = "Report",
            EntityId = report.Id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Description = title
        });
        await db.SaveChangesAsync();
    }

    private bool CanAccessAssessment(Assessment assessment)
    {
        if (assessment.OrganizationId != User.OrganizationId())
        {
            return false;
        }
        if (User.IsInRole("Employee"))
        {
            return assessment.AssignedToUserId == User.UserId();
        }
        if (User.IsInRole("Department Manager"))
        {
            return assessment.DepartmentId == User.DepartmentId() ||
                   assessment.AssignedToUserId == User.UserId();
        }
        return true;
    }
}
