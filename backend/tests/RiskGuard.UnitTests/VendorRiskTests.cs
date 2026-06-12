using FluentAssertions;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Services;
using RiskGuard.Domain.Enums;

namespace RiskGuard.UnitTests;

public sealed class VendorRiskTests
{
    [Fact]
    public void Calculate_WeightsSecurityAndDataAccessMoreHeavily()
    {
        var service = new VendorRiskService(new RiskScoringService());
        var input = new VendorRiskInput(20, 90, 60, 40, 30, 90);

        var result = service.Calculate(input);

        result.Score.Should().BeGreaterThan(60);
        result.Level.Should().Be(RiskLevel.High);
    }
}
