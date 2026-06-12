using System.Security.Claims;
using RiskGuard.Domain.Entities;
using RiskGuard.Persistence;

namespace RiskGuard.API.Services;

public static class ControllerHelpers
{
    public static Guid? OrganizationId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("organization_id"), out var id) ? id : null;

    public static Guid? DepartmentId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("department_id"), out var id) ? id : null;

    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public static bool IsRiskProfessional(this ClaimsPrincipal user) =>
        user.IsInRole("Admin") ||
        user.IsInRole("Risk Manager") ||
        user.IsInRole("Compliance Officer") ||
        user.IsInRole("Security Analyst");

    public static async Task WriteAuditAsync(
        this RiskGuardDbContext db,
        ClaimsPrincipal user,
        HttpContext context,
        string action,
        string entityType,
        object entityId,
        string description,
        string oldValue = "",
        string newValue = "")
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            UserEmail = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "anonymous",
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString() ?? string.Empty,
            Description = description,
            IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            OldValue = oldValue,
            NewValue = newValue
        });
        await db.SaveChangesAsync(context.RequestAborted);
    }
}
