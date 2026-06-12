using FluentAssertions;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Services;
using RiskGuard.Domain.Enums;

namespace RiskGuard.UnitTests;

public sealed class RecommendationTests
{
    [Fact]
    public void GenerateRecommendations_CreatesActionsOnlyForHighExposureAnswers()
    {
        var engine = new RecommendationEngine();
        var answers = new[]
        {
            new WeightedAnswer(82, 2, "Is MFA enforced?", "Enable MFA.", "ISO 27001 A.5.17"),
            new WeightedAnswer(25, 1, "Are logs retained?", "Retain logs.", "NIST DE.CM")
        };

        var result = engine.GenerateRecommendations(
            Guid.NewGuid(), RiskCategoryType.Cybersecurity, answers, "Security Analyst");

        result.Should().ContainSingle();
        result.Single().Priority.Should().Be(Severity.Critical);
        result.Single().DueDateUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(14), TimeSpan.FromSeconds(3));
        result.Single().ComplianceMapping.Should().Contain("ISO 27001");
    }
}
