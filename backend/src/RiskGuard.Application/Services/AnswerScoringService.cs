using System.Text.Json;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;

namespace RiskGuard.Application.Services;

public sealed class AnswerScoringService : IAnswerScoringService
{
    public bool TryCalculate(AssessmentQuestion question, string answer, out decimal score)
    {
        score = 0;
        var normalized = answer.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (question.AnswerType is AnswerType.Numeric or AnswerType.RatingScale)
        {
            if (!decimal.TryParse(normalized, out var numeric))
            {
                return false;
            }

            score = Math.Clamp(numeric, 0, 100);
            return true;
        }

        if (question.AnswerType is AnswerType.Text or AnswerType.EvidenceUpload)
        {
            score = 0;
            return true;
        }

        try
        {
            var mappings = JsonSerializer.Deserialize<Dictionary<string, decimal>>(
                question.ScoreMappingJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var match = mappings?.Where(item =>
                    item.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                .Select(item => (decimal?)item.Value)
                .FirstOrDefault();
            if (match.HasValue)
            {
                score = Math.Clamp(match.Value, 0, 100);
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        var fallback = normalized.ToLowerInvariant() switch
        {
            "yes" => 0m,
            "partially" or "partial" => 50m,
            "no" => 100m,
            "not applicable" or "n/a" => 0m,
            _ => (decimal?)null
        };
        if (!fallback.HasValue)
        {
            return false;
        }

        score = fallback.Value;
        return true;
    }
}
