using System.Globalization;
using System.Reflection;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;

namespace RiskGuard.Infrastructure.Reporting;

public sealed class ReportService : IReportService
{
    public byte[] GenerateExecutivePdf(
        string companyName,
        string reportTitle,
        DashboardSummary summary,
        string preparedBy)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor("#263449"));
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("RISKGUARD AI").FontSize(12).Bold().FontColor("#1677ff");
                        column.Item().Text(reportTitle).FontSize(22).Bold().FontColor("#0f1c2e");
                        column.Item().Text(companyName).FontSize(12).FontColor("#64748b");
                    });
                    row.ConstantItem(150).AlignRight().Column(column =>
                    {
                        column.Item().Text(DateTime.UtcNow.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture));
                        column.Item().Text($"Prepared by {preparedBy}").FontSize(9).FontColor("#64748b");
                    });
                });

                page.Content().PaddingVertical(22).Column(column =>
                {
                    column.Spacing(14);
                    column.Item().Background("#eef5ff").Padding(18).Row(row =>
                    {
                        row.RelativeItem().Column(item =>
                        {
                            item.Item().Text("Overall risk score").FontColor("#64748b");
                            item.Item().Text($"{summary.OverallRiskScore:0} / 100").FontSize(28).Bold();
                        });
                        row.RelativeItem().Column(item =>
                        {
                            item.Item().Text("Risk level").FontColor("#64748b");
                            item.Item().Text(summary.RiskLevel.ToString()).FontSize(22).Bold().FontColor(RiskColor(summary.RiskLevel));
                        });
                        row.RelativeItem().Column(item =>
                        {
                            item.Item().Text("Financial exposure").FontColor("#64748b");
                            item.Item().Text($"R {summary.FinancialExposure:N0}").FontSize(18).Bold();
                        });
                    });

                    column.Item().Text("Executive summary").FontSize(15).Bold();
                    column.Item().Text(
                        $"The current enterprise risk position is {summary.RiskLevel.ToString().ToLowerInvariant()} at {summary.OverallRiskScore:0}/100. " +
                        $"{summary.CriticalRisks} critical and {summary.HighRisks} high risks require management oversight. " +
                        $"Compliance readiness is {summary.ComplianceReadiness:0}% and continuity readiness is {summary.BusinessContinuityScore:0}%.");

                    column.Item().Text("Category scores").FontSize(15).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Category");
                            header.Cell().Element(HeaderCell).Text("Score");
                            header.Cell().Element(HeaderCell).Text("Level");
                        });
                        foreach (var category in summary.Categories)
                        {
                            var level = RiskLevelFor(category.Score);
                            table.Cell().Element(BodyCell).Text(category.Category);
                            table.Cell().Element(BodyCell).Text($"{category.Score:0}");
                            table.Cell().Element(BodyCell).Text(level).FontColor(RiskColorName(level));
                        }
                    });

                    column.Item().Text("Key findings and next steps").FontSize(15).Bold();
                    var priorities = summary.Categories
                        .Where(category => category.Score > 0)
                        .OrderByDescending(category => category.Score)
                        .Take(4)
                        .ToArray();
                    if (priorities.Length == 0)
                    {
                        column.Item().Text("No scored risk categories are currently available.");
                    }
                    else
                    {
                        for (var index = 0; index < priorities.Length; index++)
                        {
                            var priority = priorities[index];
                            column.Item().Text(
                                $"{index + 1}. Review {priority.Category} controls and treatment plans; the current score is {priority.Score:0}/100 ({RiskLevelFor(priority.Score)}).");
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Confidential | RiskGuard AI | ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateAssessmentPdf(
        string companyName,
        Assessment assessment,
        IReadOnlyCollection<ComplianceGap> complianceGaps,
        string preparedBy)
    {
        var recommendations = assessment.Risks
            .SelectMany(risk => risk.Recommendations)
            .OrderByDescending(recommendation => recommendation.Priority)
            .ThenBy(recommendation => recommendation.DueDateUtc)
            .ToArray();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor("#263449"));
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("RISKGUARD AI").FontSize(12).Bold().FontColor("#1677ff");
                        column.Item().Text("Assessment Risk Report").FontSize(22).Bold().FontColor("#0f1c2e");
                        column.Item().Text(companyName).FontSize(12).FontColor("#64748b");
                    });
                    row.ConstantItem(150).AlignRight().Column(column =>
                    {
                        column.Item().Text(DateTime.UtcNow.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture));
                        column.Item().Text($"Prepared by {preparedBy}").FontSize(9).FontColor("#64748b");
                    });
                });

                page.Content().PaddingVertical(22).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text(assessment.Title).FontSize(20).Bold().FontColor("#0f1c2e");
                    column.Item().Text(
                            $"{assessment.RiskCategory?.Name ?? assessment.RiskCategory?.Type.ToString() ?? "Risk"} | " +
                            $"{assessment.Department?.Name ?? "Enterprise"} | " +
                            $"Submitted {assessment.SubmittedAtUtc?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "Not submitted"}")
                        .FontColor("#64748b");

                    column.Item().Background("#eef5ff").Padding(18).Row(row =>
                    {
                        row.RelativeItem().Column(item =>
                        {
                            item.Item().Text("Overall risk score").FontColor("#64748b");
                            item.Item().Text($"{assessment.Score:0.##} / 100").FontSize(26).Bold();
                        });
                        row.RelativeItem().Column(item =>
                        {
                            item.Item().Text("Risk level").FontColor("#64748b");
                            item.Item().Text(assessment.RiskLevel.ToString()).FontSize(22).Bold()
                                .FontColor(RiskColor(assessment.RiskLevel));
                        });
                        row.RelativeItem().Column(item =>
                        {
                            item.Item().Text("Submitted answers").FontColor("#64748b");
                            item.Item().Text(assessment.Responses.Count.ToString(CultureInfo.InvariantCulture))
                                .FontSize(22).Bold();
                        });
                    });

                    column.Item().Text("Submitted answers").FontSize(15).Bold();
                    foreach (var response in assessment.Responses.OrderBy(item => item.Question?.Text))
                    {
                        column.Item().Border(1).BorderColor("#e2e8f0").Padding(10).Column(card =>
                        {
                            card.Spacing(4);
                            card.Item().Text(response.Question?.Text ?? "Assessment question").Bold();
                            card.Item().Text(
                                    $"Answer: {response.Answer} | Risk score: {response.AnswerScore:0.##} | Weight: {response.Question?.Weight ?? 0:0.##}")
                                .FontSize(9).FontColor("#64748b");
                            if (!string.IsNullOrWhiteSpace(response.Notes))
                            {
                                card.Item().Text($"Evidence notes: {response.Notes}").FontSize(9);
                            }
                            if (!string.IsNullOrWhiteSpace(response.Question?.ComplianceMappings))
                            {
                                card.Item().Text($"Mappings: {response.Question.ComplianceMappings}")
                                    .FontSize(9).FontColor("#64748b");
                            }
                        });
                    }

                    column.Item().Text("Recommendations").FontSize(15).Bold();
                    if (recommendations.Length == 0)
                    {
                        column.Item().Text("No high-priority recommendations were generated for this result.");
                    }
                    foreach (var recommendation in recommendations)
                    {
                        column.Item().BorderLeft(4).BorderColor(RiskColorName(recommendation.Priority.ToString()))
                            .PaddingLeft(10).Column(card =>
                            {
                                card.Item().Text(recommendation.Title).Bold();
                                card.Item().Text(recommendation.Description).FontSize(9);
                                card.Item().Text(
                                        $"{recommendation.Priority} priority | Owner: {recommendation.SuggestedOwner} | Due: {recommendation.DueDateUtc:dd MMM yyyy}")
                                    .FontSize(9).FontColor("#64748b");
                            });
                    }

                    column.Item().Text("Compliance gaps").FontSize(15).Bold();
                    if (complianceGaps.Count == 0)
                    {
                        column.Item().Text("No compliance gaps are linked to this assessment risk.");
                    }
                    foreach (var gap in complianceGaps.OrderByDescending(item => item.Severity))
                    {
                        column.Item().Border(1).BorderColor("#e2e8f0").Padding(10).Column(card =>
                        {
                            card.Item().Text(
                                $"{gap.Control?.Framework?.Name ?? "Framework"}: " +
                                $"{gap.Control?.Code ?? "Control"} - {gap.Control?.Title ?? "Compliance gap"}").Bold();
                            card.Item().Text(gap.Description).FontSize(9);
                            card.Item().Text(
                                    $"{gap.Severity} severity | Owner: {gap.Owner} | Due: {gap.DueDateUtc:dd MMM yyyy}")
                                .FontSize(9).FontColor("#64748b");
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Confidential | RiskGuard AI | ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateRiskRegisterExcel(IEnumerable<RiskItem> risks)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Risk Register");
        var headers = new[]
        {
            "Risk", "Category", "Department", "Owner", "Impact", "Likelihood",
            "Score", "Level", "Status", "Financial Exposure"
        };

        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }

        var row = 2;
        foreach (var risk in risks)
        {
            sheet.Cell(row, 1).Value = risk.Title;
            sheet.Cell(row, 2).Value = risk.Category.ToString();
            sheet.Cell(row, 3).Value = risk.Department?.Name ?? string.Empty;
            sheet.Cell(row, 4).Value = risk.Owner;
            sheet.Cell(row, 5).Value = risk.Impact;
            sheet.Cell(row, 6).Value = risk.Likelihood;
            sheet.Cell(row, 7).Value = risk.Score;
            sheet.Cell(row, 8).Value = risk.RiskLevel.ToString();
            sheet.Cell(row, 9).Value = risk.Status;
            sheet.Cell(row, 10).Value = risk.FinancialExposure;
            row++;
        }

        var range = sheet.Range(1, 1, Math.Max(row - 1, 1), headers.Length);
        range.CreateTable();
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1677FF");
        sheet.Row(1).Style.Font.FontColor = XLColor.White;
        sheet.Column(10).Style.NumberFormat.Format = "\"R\" #,##0";
        sheet.Columns().AdjustToContents(12, 42);
        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GenerateCsv<T>(IEnumerable<T> records)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => IsSimple(property.PropertyType))
            .ToArray();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", properties.Select(property => Escape(property.Name))));

        foreach (var record in records)
        {
            builder.AppendLine(string.Join(",", properties.Select(property =>
                Escape(Convert.ToString(property.GetValue(record), CultureInfo.InvariantCulture) ?? string.Empty))));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background("#1677ff").Padding(7).DefaultTextStyle(style => style.FontColor(Colors.White).Bold());

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor("#e2e8f0").Padding(7);

    private static string RiskColor(RiskGuard.Domain.Enums.RiskLevel level) => level switch
    {
        RiskGuard.Domain.Enums.RiskLevel.Low => "#16a34a",
        RiskGuard.Domain.Enums.RiskLevel.Medium => "#ca8a04",
        RiskGuard.Domain.Enums.RiskLevel.High => "#ea580c",
        _ => "#dc2626"
    };

    private static string RiskLevelFor(decimal score) =>
        score <= 25 ? "Low" : score <= 50 ? "Medium" : score <= 75 ? "High" : "Critical";

    private static string RiskColorName(string level) => level switch
    {
        "Low" => "#16a34a",
        "Medium" => "#ca8a04",
        "High" => "#ea580c",
        _ => "#dc2626"
    };

    private static bool IsSimple(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        return target.IsPrimitive || target.IsEnum || target == typeof(string) ||
               target == typeof(Guid) || target == typeof(DateTime) || target == typeof(decimal);
    }

    private static string Escape(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
