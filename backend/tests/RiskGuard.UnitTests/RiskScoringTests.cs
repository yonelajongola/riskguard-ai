using FluentAssertions;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Services;
using RiskGuard.Domain.Enums;

namespace RiskGuard.UnitTests;

public sealed class RiskScoringTests
{
    private readonly RiskScoringService _service = new();

    [Fact]
    public void CalculateOverallRisk_UsesQuestionWeights()
    {
        var answers = new[]
        {
            new WeightedAnswer(100, 2, "Critical control", "", ""),
            new WeightedAnswer(0, 1, "Supporting control", "", "")
        };

        var result = _service.CalculateOverallRisk(answers);

        result.Score.Should().Be(66.67m);
        result.Level.Should().Be(RiskLevel.High);
        result.Color.Should().Be("#f97316");
    }

    [Theory]
    [InlineData(0, RiskLevel.Low)]
    [InlineData(25, RiskLevel.Low)]
    [InlineData(26, RiskLevel.Medium)]
    [InlineData(50, RiskLevel.Medium)]
    [InlineData(51, RiskLevel.High)]
    [InlineData(75, RiskLevel.High)]
    [InlineData(76, RiskLevel.Critical)]
    [InlineData(100, RiskLevel.Critical)]
    public void GetRiskLevel_MapsPublishedBoundaries(decimal score, RiskLevel expected)
    {
        _service.GetRiskLevel(score).Should().Be(expected);
    }

    [Fact]
    public void CalculateOverallRisk_ClampsInvalidAnswerScores()
    {
        var result = _service.CalculateOverallRisk(
            [new WeightedAnswer(140, 1, "Upper", "", ""), new WeightedAnswer(-20, 1, "Lower", "", "")]);

        result.Score.Should().Be(50);
    }
}
