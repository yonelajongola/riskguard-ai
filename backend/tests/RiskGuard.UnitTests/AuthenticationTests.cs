using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RiskGuard.Infrastructure.Authentication;

namespace RiskGuard.UnitTests;

public sealed class AuthenticationTests
{
    [Fact]
    public void TokenService_CreatesSignedJwtWithRoleClaims()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-only-secret-with-at-least-32-characters",
                ["Jwt:Issuer"] = "RiskGuardAI.Tests",
                ["Jwt:Audience"] = "RiskGuardAI.Tests.Client"
            })
            .Build();
        var service = new TokenService(configuration);

        var token = service.CreateAccessToken(
            Guid.NewGuid(), "admin@riskguard.local", "System Administrator",
            ["Admin", "Risk Manager"], Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5));
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        parsed.Issuer.Should().Be("RiskGuardAI.Tests");
        parsed.Claims.Should().Contain(claim => claim.Value == "Admin");
        parsed.Claims.Should().Contain(claim => claim.Value == "admin@riskguard.local");
    }
}
