using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RiskGuard.Application.Interfaces;

namespace RiskGuard.Infrastructure.Authentication;

public sealed class TokenService(IConfiguration configuration) : ITokenService
{
    public string CreateAccessToken(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string> roles,
        Guid? organizationId,
        Guid? departmentId,
        DateTime expiresAtUtc)
    {
        var configuredSecret = configuration["Jwt:Secret"];
        var secret = string.IsNullOrWhiteSpace(configuredSecret) ||
                     configuredSecret.Equals("JWT_SECRET", StringComparison.Ordinal)
            ? Environment.GetEnvironmentVariable("JWT_SECRET")
            : configuredSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("JWT secret is not configured.");
        }
        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("JWT secret must be at least 32 bytes.");
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, fullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (organizationId.HasValue)
        {
            claims.Add(new Claim("organization_id", organizationId.Value.ToString()));
        }
        if (departmentId.HasValue)
        {
            claims.Add(new Claim("department_id", departmentId.Value.ToString()));
        }
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "RiskGuardAI",
            audience: configuration["Jwt:Audience"] ?? "RiskGuardAI.Web",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
