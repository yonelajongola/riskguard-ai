using System.Text.RegularExpressions;

namespace RiskGuard.Application.Services;

public static partial class AiPromptSecurity
{
    public static string Sanitize(string value)
    {
        var sanitized = value.Trim();
        sanitized = JwtPattern().Replace(sanitized, "[REDACTED_TOKEN]");
        sanitized = BearerPattern().Replace(sanitized, "$1[REDACTED_TOKEN]");
        sanitized = SecretAssignmentPattern().Replace(sanitized, "$1=[REDACTED]");
        sanitized = ConnectionStringPasswordPattern().Replace(sanitized, "$1=[REDACTED]");
        return sanitized.Length <= 2000 ? sanitized : sanitized[..2000];
    }

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b")]
    private static partial Regex JwtPattern();

    [GeneratedRegex(@"(?i)\b(Bearer\s+)[A-Za-z0-9._~+/=-]{12,}")]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"(?i)\b(password|passwd|pwd|token|api[_-]?key|secret)\s*[:=]\s*[^\s,;]+")]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(@"(?i)\b(Password|Pwd)\s*=\s*[^;]+")]
    private static partial Regex ConnectionStringPasswordPattern();
}
