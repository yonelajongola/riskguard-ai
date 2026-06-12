using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Services;
using RiskGuard.Persistence;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Auditor")]
[Route("api/audit-logs")]
public sealed class AuditLogsController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get([FromQuery] int take = 200)
    {
        var organizationId = User.OrganizationId();
        var userIds = await db.Users.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.Id.ToString())
            .ToListAsync();
        var emails = await db.Users.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.Email!)
            .ToListAsync();
        return Ok(await db.AuditLogs.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) || emails.Contains(x.UserEmail))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync());
    }
}

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        return Ok(await db.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync());
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var notification = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (notification is null) return NotFound();
        notification.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var notifications = await db.Notifications.Where(x => x.UserId == userId).ToListAsync();
        foreach (var notification in notifications) notification.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
