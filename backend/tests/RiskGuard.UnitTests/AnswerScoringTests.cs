using FluentAssertions;
using RiskGuard.Application.Services;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;

namespace RiskGuard.UnitTests;

public sealed class AnswerScoringTests
{
    private readonly AnswerScoringService _service = new();

    [Theory]
    [InlineData("Yes", 0)]
    [InlineData("Partially", 50)]
    [InlineData("No", 100)]
    [InlineData("Not applicable", 0)]
    public void TryCalculate_UsesServerSideMapping(string answer, decimal expected)
    {
        var question = new AssessmentQuestion
        {
            AnswerType = AnswerType.YesNo,
            ScoreMappingJson = "{\"Yes\":0,\"Partially\":50,\"No\":100,\"Not applicable\":0}"
        };

        var success = _service.TryCalculate(question, answer, out var score);

        success.Should().BeTrue();
        score.Should().Be(expected);
    }

    [Fact]
    public void TryCalculate_RejectsUnknownAnswers()
    {
        var question = new AssessmentQuestion { AnswerType = AnswerType.YesNo };

        var success = _service.TryCalculate(question, "Definitely", out _);

        success.Should().BeFalse();
    }
}
