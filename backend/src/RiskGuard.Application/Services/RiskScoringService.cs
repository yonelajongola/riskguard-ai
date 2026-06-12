using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Enums;

namespace RiskGuard.Application.Services;

public sealed class RiskScoringService : IRiskScoringService
{
    public RiskCalculationResult CalculateOverallRisk(IEnumerable<WeightedAnswer> answers)
    {
        var materialized = answers.ToArray();
        if (materialized.Length == 0)
        {
            return new RiskCalculationResult(0, RiskLevel.Low, GetRiskColor(0));
        }

        var weight = materialized.Sum(x => x.Weight);
        var score = weight <= 0
            ? 0
            : Math.Round(materialized.Sum(x => Clamp(x.Score) * x.Weight) / weight, 2);

        return new RiskCalculationResult(score, GetRiskLevel(score), GetRiskColor(score));
    }

    public decimal CalculateCategoryRisk(IEnumerable<WeightedAnswer> answers) =>
        CalculateOverallRisk(answers).Score;

    public RiskLevel GetRiskLevel(decimal score) => Clamp(score) switch
    {
        <= 25 => RiskLevel.Low,
        <= 50 => RiskLevel.Medium,
        <= 75 => RiskLevel.High,
        _ => RiskLevel.Critical
    };

    public string GetRiskColor(decimal score) => GetRiskLevel(score) switch
    {
        RiskLevel.Low => "#22c55e",
        RiskLevel.Medium => "#eab308",
        RiskLevel.High => "#f97316",
        _ => "#ef4444"
    };

    public decimal CalculateTrend(decimal currentScore, decimal previousScore)
    {
        if (previousScore == 0)
        {
            return currentScore == 0 ? 0 : 100;
        }

        return Math.Round((currentScore - previousScore) / previousScore * 100, 2);
    }

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0, 100);
}
