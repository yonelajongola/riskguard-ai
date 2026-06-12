using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Enums;

namespace RiskGuard.Infrastructure.AI;

public sealed class MockAiRiskService : IAiRiskService
{
    public bool IsConfigured => false;

    public Task<AiChatResponse> GenerateAsync(
        AiGenerationRequest request,
        AiRiskContext context,
        CancellationToken cancellationToken)
    {
        var prompt = request.Prompt.ToLowerInvariant();
        var topRisk = context.TopRisks.FirstOrDefault();
        var topCategory = context.Summary.CategoryScores
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();
        var topRecommendation = context.Recommendations.FirstOrDefault();
        var topGap = context.ComplianceGaps.FirstOrDefault();
        var topVendor = context.Vendors.OrderByDescending(item => item.RiskScore).FirstOrDefault();
        var continuity = context.ContinuityFindings.OrderBy(item => item.ContinuityScore).FirstOrDefault();

        var response = request.PromptCategory switch
        {
            "Executive" => Executive(request, context, topRisk, topCategory),
            "Mitigation" => Mitigation(request, context, topRisk, topRecommendation),
            "Compliance" => Compliance(request, context, topGap),
            "Cybersecurity" => Cybersecurity(request, context),
            "Vendor" => Vendor(request, context, topVendor),
            "Business continuity" => BusinessContinuity(request, context, continuity),
            "Department" => Department(request, context, topRisk),
            "Recommendations" => Mitigation(request, context, topRisk, topRecommendation),
            _ when prompt.Contains("assessment") && context.Assessment is not null =>
                Assessment(request, context),
            _ => RiskExplanation(request, context, topRisk, topCategory)
        };

        return Task.FromResult(response with
        {
            IsMock = true,
            GeneratedAtUtc = DateTime.UtcNow,
            Context = context.Summary
        });
    }

    private static AiChatResponse RiskExplanation(
        AiGenerationRequest request,
        AiRiskContext context,
        AiRiskItemContext? topRisk,
        CategoryScore? topCategory)
    {
        var title = topRisk is null ? "Current risk position" : $"Priority risk: {topRisk.Title}";
        var summary = topRisk is null
            ? $"The current overall risk score is {context.Summary.OverallRiskScore:0.##}/100 ({context.Summary.RiskLevel})."
            : $"{topRisk.Title} is the leading recorded exposure at {topRisk.Score:0.##}/100 ({topRisk.Level}) in {topRisk.Department}.";
        return Response(
            title,
            summary,
            [
                $"{context.Summary.CriticalRisks} critical and {context.Summary.HighRisks} high risks are currently recorded.",
                topCategory is null
                    ? "No category score is available."
                    : $"{topCategory.Category} is the highest-scoring category at {topCategory.Score:0.##}/100.",
                $"{context.Summary.OpenIncidents} open incidents and {context.Summary.OpenComplianceGaps} open compliance gaps contribute to the current position."
            ],
            Actions(context, topRisk),
            Priority(topRisk?.Level ?? context.Summary.RiskLevel),
            topRisk is null
                ? "Unmanaged exposure can affect service availability, compliance confidence, and decision quality."
                : $"If untreated, this exposure can affect {topRisk.Department} operations and enterprise assurance.",
            ["Confirm the accountable owner.", "Validate current evidence.", "Track treatment progress on the risk dashboard."],
            request.ResponseType);
    }

    private static AiChatResponse Assessment(AiGenerationRequest request, AiRiskContext context)
    {
        var assessment = context.Assessment!;
        var failed = assessment.Answers.Where(item => item.Score > 50).OrderByDescending(item => item.Score * item.Weight).ToArray();
        return Response(
            $"Assessment summary: {assessment.Title}",
            $"{assessment.Title} scored {assessment.Score:0.##}/100 ({assessment.RiskLevel}) across {assessment.Answers.Count} answered controls.",
            failed.Length == 0
                ? ["No high-exposure answers were recorded."]
                : failed.Take(4).Select(item => $"{item.Question} was answered '{item.Answer}' and scored {item.Score:0.##}.").ToArray(),
            context.Recommendations.Take(4).Select(item => item.Title).DefaultIfEmpty("Review and approve the assessment result.").ToArray(),
            Priority(assessment.RiskLevel),
            $"The assessment result affects {assessment.Department} control confidence and the {assessment.Category} risk position.",
            ["Review the highest-weight failed controls.", "Assign treatment owners.", "Retain evidence and schedule reassessment."],
            request.ResponseType);
    }

    private static AiChatResponse Executive(
        AiGenerationRequest request,
        AiRiskContext context,
        AiRiskItemContext? topRisk,
        CategoryScore? topCategory) =>
        Response(
            "Executive risk summary",
            $"{context.Organization} is currently at {context.Summary.OverallRiskScore:0.##}/100 ({context.Summary.RiskLevel}), with {context.Summary.CriticalRisks} critical and {context.Summary.HighRisks} high risks.",
            [
                topRisk is null ? "No material risk item is available." : $"The leading exposure is {topRisk.Title} at {topRisk.Score:0.##}/100.",
                topCategory is null ? "No category trend is available." : $"{topCategory.Category} is the highest-risk domain.",
                $"Compliance readiness is {context.Summary.ComplianceReadiness:0.##}% and continuity readiness is {context.Summary.BusinessContinuityScore:0.##}%."
            ],
            Actions(context, topRisk),
            Priority(topRisk?.Level ?? context.Summary.RiskLevel),
            "The current position can affect strategic delivery, regulatory assurance, service resilience, and financial performance.",
            ["Confirm executive risk acceptance thresholds.", "Fund the highest-priority treatments.", "Review progress at the next governance meeting."],
            request.ResponseType);

    private static AiChatResponse Mitigation(
        AiGenerationRequest request,
        AiRiskContext context,
        AiRiskItemContext? topRisk,
        AiRecommendationContext? recommendation) =>
        Response(
            "Prioritized mitigation plan",
            topRisk is null
                ? "No scored risk is available; begin by completing and approving a current assessment."
                : $"Treatment should begin with {topRisk.Title}, currently scored {topRisk.Score:0.##}/100 ({topRisk.Level}).",
            context.TopRisks.Take(4)
                .Select(item => $"{item.Title}: {item.Score:0.##}/100, owned by {item.Owner}.")
                .DefaultIfEmpty("No high-risk findings are available.")
                .ToArray(),
            context.Recommendations.Take(5)
                .Select(item => $"{item.Title} ({item.Priority}, owner: {item.Owner}).")
                .DefaultIfEmpty(recommendation?.Title ?? "Create a treatment action with an accountable owner.")
                .ToArray(),
            Priority(topRisk?.Level ?? context.Summary.RiskLevel),
            "A sequenced plan reduces disruption, avoids duplicated remediation work, and improves evidence quality.",
            ["Approve owners and due dates.", "Address critical controls within 14 days.", "Recalculate risk after evidence is validated."],
            request.ResponseType);

    private static AiChatResponse Compliance(
        AiGenerationRequest request,
        AiRiskContext context,
        AiComplianceGapContext? gap) =>
        Response(
            "Compliance gap summary",
            $"{context.Summary.OpenComplianceGaps} open compliance gaps are reflected in a readiness score of {context.Summary.ComplianceReadiness:0.##}%.",
            context.ComplianceGaps.Take(5)
                .Select(item => $"{item.Framework} {item.Control}: {item.Description} ({item.Severity}).")
                .DefaultIfEmpty("No open compliance gaps are linked to the current risk context.")
                .ToArray(),
            context.ComplianceGaps.Take(5)
                .Select(item => $"{item.Owner}: {item.Description}")
                .DefaultIfEmpty("Continue evidence review and control monitoring.")
                .ToArray(),
            gap is null ? "Low" : gap.Severity.ToString(),
            "Open gaps can reduce audit confidence, delay assurance, and increase regulatory exposure.",
            ["Confirm control ownership.", "Attach current evidence.", "Close or formally accept each outstanding gap."],
            request.ResponseType);

    private static AiChatResponse Cybersecurity(AiGenerationRequest request, AiRiskContext context)
    {
        var cyber = context.TopRisks.Where(item => item.Category == RiskCategoryType.Cybersecurity.ToString()).ToArray();
        return Response(
            "Cybersecurity exposure",
            cyber.Length == 0
                ? "No scored cybersecurity risks are currently available."
                : $"{cyber.Length} cybersecurity risks are in context; the highest score is {cyber.Max(item => item.Score):0.##}/100.",
            cyber.Take(5).Select(item => $"{item.Title} ({item.Level}, {item.Score:0.##}/100).")
                .DefaultIfEmpty("Complete a cybersecurity assessment to establish current exposure.").ToArray(),
            context.Recommendations.Where(item => item.Priority is Severity.High or Severity.Critical)
                .Take(5).Select(item => item.Title)
                .DefaultIfEmpty("Validate identity, monitoring, patching, encryption, and recovery controls.").ToArray(),
            cyber.Any(item => item.Level == RiskLevel.Critical) ? "Critical" : cyber.Any() ? "High" : "Low",
            "Cybersecurity weaknesses can disrupt services, expose information, and trigger incident and compliance obligations.",
            ["Validate privileged access controls.", "Review detection coverage.", "Test ransomware-resilient recovery."],
            request.ResponseType);
    }

    private static AiChatResponse Vendor(
        AiGenerationRequest request,
        AiRiskContext context,
        AiVendorContext? vendor) =>
        Response(
            "Vendor risk explanation",
            vendor is null
                ? "No vendor risk records are available."
                : $"{vendor.Name} is the highest-risk supplier at {vendor.RiskScore:0.##}/100 ({vendor.RiskLevel}).",
            context.Vendors.Take(5)
                .Select(item => $"{item.Name}: {item.RiskScore:0.##}/100, {item.ComplianceStatus}, service: {item.Service}.")
                .DefaultIfEmpty("No vendor findings are available.")
                .ToArray(),
            ["Review high-risk supplier assurance evidence.", "Confirm contract, incident, and exit obligations.", "Track remediation with the accountable vendor owner."],
            vendor?.RiskLevel.ToString() ?? "Low",
            "Supplier weaknesses can create service concentration, data exposure, contractual, and continuity risk.",
            ["Escalate critical suppliers.", "Set review dates.", "Test exit and substitution arrangements."],
            request.ResponseType);

    private static AiChatResponse BusinessContinuity(
        AiGenerationRequest request,
        AiRiskContext context,
        AiContinuityContext? finding) =>
        Response(
            "Business continuity recommendation",
            finding is null
                ? "No business continuity plan is available."
                : $"{finding.Name} has a readiness score of {finding.ContinuityScore:0.##}% with {finding.OverdueTests} overdue tests.",
            context.ContinuityFindings.Take(5)
                .Select(item => $"{item.Name}: {item.ContinuityScore:0.##}% readiness, {item.OverdueTests} overdue tests.")
                .DefaultIfEmpty("No continuity findings are available.")
                .ToArray(),
            ["Test recovery against approved RTO and RPO values.", "Resolve overdue backup and disaster-recovery tests.", "Retain evidence and lessons learned."],
            finding is null ? "Low" : finding.ContinuityScore < 50 ? "Critical" : finding.ContinuityScore < 70 ? "High" : "Medium",
            "Weak recovery capability can extend downtime, increase financial loss, and disrupt customer service.",
            ["Schedule the next recovery exercise.", "Confirm critical-system owners.", "Report unresolved recovery gaps."],
            request.ResponseType);

    private static AiChatResponse Department(
        AiGenerationRequest request,
        AiRiskContext context,
        AiRiskItemContext? topRisk)
    {
        var department = context.TopRisks
            .GroupBy(item => item.Department)
            .Select(group => new { Department = group.Key, Score = group.Average(item => item.Score), Count = group.Count() })
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();
        return Response(
            "Department risk summary",
            department is null
                ? "No department-level risk data is available."
                : $"{department.Department} needs the most urgent attention, averaging {department.Score:0.##}/100 across {department.Count} risks.",
            context.TopRisks.Where(item => item.Department == department?.Department)
                .Take(5).Select(item => $"{item.Title} ({item.Level}).")
                .DefaultIfEmpty("No department findings are available.").ToArray(),
            Actions(context, topRisk),
            department is null ? "Low" : department.Score > 75 ? "Critical" : department.Score > 50 ? "High" : "Medium",
            "Concentrated departmental exposure can affect service delivery and enterprise objectives.",
            ["Meet with the department risk owner.", "Confirm treatment capacity.", "Review progress weekly until exposure returns to tolerance."],
            request.ResponseType);
    }

    private static string[] Actions(AiRiskContext context, AiRiskItemContext? topRisk) =>
        context.Recommendations.Take(4).Select(item => item.Title)
            .DefaultIfEmpty(topRisk is null ? "Complete a current risk assessment." : $"Create a treatment plan for {topRisk.Title}.")
            .ToArray();

    private static string Priority(RiskLevel level) => level.ToString();

    private static AiChatResponse Response(
        string title,
        string summary,
        IReadOnlyCollection<string> findings,
        IReadOnlyCollection<string> actions,
        string priority,
        string businessImpact,
        IReadOnlyCollection<string> nextSteps,
        string responseType) =>
        new(
            title,
            summary,
            findings,
            actions,
            priority,
            businessImpact,
            nextSteps,
            responseType,
            true,
            DateTime.UtcNow,
            new AiRiskContextSummary(0, RiskLevel.Low, 0, 0, 0, 0, 0, 0, 0, []));
}
