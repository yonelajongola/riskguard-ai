using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;

namespace RiskGuard.Application.Services;

public sealed class RecommendationEngine : IRecommendationEngine
{
    public IReadOnlyCollection<Recommendation> GenerateRecommendations(
        Guid assessmentId,
        RiskCategoryType category,
        IEnumerable<WeightedAnswer> answers,
        string suggestedOwner)
    {
        return answers
            .Where(x => x.Score > 50)
            .OrderByDescending(x => x.Score * x.Weight)
            .Select(x =>
            {
                var critical = x.Score > 75;
                return new Recommendation
                {
                    AssessmentId = assessmentId,
                    Title = $"Address: {Shorten(x.Question, 72)}",
                    Description = string.IsNullOrWhiteSpace(x.Recommendation)
                        ? "Define and implement a documented control, assign accountable ownership, and retain evidence of operation."
                        : x.Recommendation,
                    Category = category,
                    Severity = critical ? Severity.Critical : Severity.High,
                    Priority = critical ? Severity.Critical : Severity.High,
                    SuggestedOwner = suggestedOwner,
                    DueDateUtc = DateTime.UtcNow.AddDays(critical ? 14 : 30),
                    BusinessImpact = "Reduces operational exposure, improves control maturity, and provides auditable evidence.",
                    ComplianceMapping = x.ComplianceMapping,
                    Status = RecommendationStatus.Open
                };
            })
            .ToArray();
    }

    private static string Shorten(string value, int length) =>
        value.Length <= length ? value : $"{value[..(length - 3)]}...";
}
