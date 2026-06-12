using FluentAssertions;
using QuestPDF.Infrastructure;
using RiskGuard.Application.DTOs;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;
using RiskGuard.Infrastructure.Reporting;

namespace RiskGuard.UnitTests;

public sealed class ReportServiceTests
{
    public ReportServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void GenerateExecutivePdf_ReturnsPdfDocument()
    {
        var service = new ReportService();
        var summary = new DashboardSummary(
            67, RiskLevel.High, 1, 4, 64, 58, 770000, 55,
            [new ChartPoint("Jun", 67)],
            [new CategoryScore("Cybersecurity", 78)]);

        var result = service.GenerateExecutivePdf(
            "FoodieBar",
            "Executive Risk Report",
            summary,
            "Risk Manager");

        result.Length.Should().BeGreaterThan(1000);
        result.Take(4).Should().Equal("%PDF"u8.ToArray());
    }

    [Fact]
    public void GenerateAssessmentPdf_ReturnsSubmittedAssessmentReport()
    {
        var service = new ReportService();
        var question = new AssessmentQuestion
        {
            Text = "Is privileged access protected by MFA?",
            Weight = 2,
            ComplianceMappings = "ISO 27001 A.5.17"
        };
        var assessment = new Assessment
        {
            Title = "Cybersecurity control review",
            Score = 82,
            RiskLevel = RiskLevel.Critical,
            Status = AssessmentStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow,
            RiskCategory = new RiskCategory { Name = "Cybersecurity" },
            Department = new Department { Name = "Technology" },
            Responses =
            [
                new AssessmentResponse
                {
                    Question = question,
                    Answer = "No",
                    AnswerScore = 100,
                    Notes = "No enforcement evidence was supplied."
                }
            ]
        };

        var result = service.GenerateAssessmentPdf("FoodieBar", assessment, [], "Risk Manager");

        result.Length.Should().BeGreaterThan(1000);
        result.Take(4).Should().Equal("%PDF"u8.ToArray());
    }

    [Fact]
    public void GenerateRiskRegisterExcel_ReturnsOpenXmlWorkbook()
    {
        var service = new ReportService();
        var risks = new[]
        {
            new RiskItem
            {
                Title = "Privileged MFA is absent",
                Category = RiskCategoryType.Cybersecurity,
                Score = 82,
                RiskLevel = RiskLevel.Critical,
                Owner = "IT Manager",
                Impact = 4,
                Likelihood = 4,
                FinancialExposure = 185000
            }
        };

        var result = service.GenerateRiskRegisterExcel(risks);

        result.Length.Should().BeGreaterThan(1000);
        result.Take(2).Should().Equal("PK"u8.ToArray());
    }
}
