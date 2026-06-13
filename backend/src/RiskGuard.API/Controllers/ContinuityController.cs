using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.Application.DTOs;
using RiskGuard.API.Services;
using RiskGuard.Domain.Entities;
using RiskGuard.Persistence;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize(Policy = "ReadSensitive")]
[Route("api/continuity")]
public sealed class ContinuityController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll() =>
        Ok(await db.BusinessContinuityPlans.AsNoTracking()
            .Include(x => x.CriticalSystems)
            .Where(x => x.OrganizationId == User.OrganizationId())
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync());

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost]
    public async Task<ActionResult> Create(ContinuityRequest request)
    {
        if (request.OrganizationId != User.OrganizationId() || request.ContinuityScore is < 0 or > 100)
        {
            return BadRequest(new { message = "Continuity plan data is invalid." });
        }
        var plan = new BusinessContinuityPlan
        {
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Owner = request.Owner.Trim(),
            ContinuityScore = request.ContinuityScore,
            Status = request.Status
        };
        db.BusinessContinuityPlans.Add(plan);
        await db.SaveChangesAsync();
        return Created($"/api/continuity/{plan.Id}", plan);
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ContinuityRequest request)
    {
        var plan = await db.BusinessContinuityPlans.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == User.OrganizationId());
        if (plan is null) return NotFound();
        if (request.ContinuityScore is < 0 or > 100) return BadRequest(new { message = "Continuity score must be between 0 and 100." });
        plan.Name = request.Name.Trim();
        plan.Owner = request.Owner.Trim();
        plan.ContinuityScore = request.ContinuityScore;
        plan.Status = request.Status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ContinuityDashboard>> Dashboard()
    {
        var plans = await db.BusinessContinuityPlans.AsNoTracking()
            .Where(x => x.OrganizationId == User.OrganizationId())
            .ToListAsync();
        var planIds = plans.Select(x => x.Id).ToArray();
        var systems = await db.CriticalSystems.AsNoTracking()
            .Where(x => planIds.Contains(x.BusinessContinuityPlanId))
            .ToListAsync();
        var readiness = plans.Select(x => x.ContinuityScore).DefaultIfEmpty(0).Average();
        var overdue = systems.Count(x =>
            !x.LastDisasterRecoveryTestDateUtc.HasValue ||
            x.LastDisasterRecoveryTestDateUtc < DateTime.UtcNow.AddDays(-180));
        var exposure = systems.Sum(x => x.RecoveryTimeObjectiveHours * 15000m);
        return Ok(new ContinuityDashboard(Math.Round(readiness, 2), systems.Count, overdue, exposure));
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost("{planId:guid}/systems")]
    public async Task<ActionResult<CriticalSystem>> CreateSystem(Guid planId, CriticalSystemRequest request)
    {
        var plan = await db.BusinessContinuityPlans
            .FirstOrDefaultAsync(x => x.Id == planId && x.OrganizationId == User.OrganizationId());
        if (plan is null) return NotFound();
        if (!ValidSystem(request)) return BadRequest(new { message = "Critical system data is invalid." });

        var system = new CriticalSystem
        {
            BusinessContinuityPlanId = planId,
            Name = request.Name.Trim(),
            SystemOwner = request.SystemOwner.Trim(),
            RecoveryTimeObjectiveHours = request.RecoveryTimeObjectiveHours,
            RecoveryPointObjectiveHours = request.RecoveryPointObjectiveHours,
            BackupFrequency = request.BackupFrequency.Trim(),
            LastBackupTestDateUtc = request.LastBackupTestDateUtc,
            LastDisasterRecoveryTestDateUtc = request.LastDisasterRecoveryTestDateUtc,
            DowntimeImpact = request.DowntimeImpact.Trim(),
            ContinuityScore = request.ContinuityScore,
            Status = request.Status.Trim()
        };
        db.CriticalSystems.Add(system);
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Critical system created", "CriticalSystem", system.Id, system.Name);
        return Created($"/api/continuity/{planId}/systems/{system.Id}", system);
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPut("{planId:guid}/systems/{id:guid}")]
    public async Task<IActionResult> UpdateSystem(Guid planId, Guid id, CriticalSystemRequest request)
    {
        var system = await OwnedSystemAsync(planId, id);
        if (system is null) return NotFound();
        if (!ValidSystem(request)) return BadRequest(new { message = "Critical system data is invalid." });

        system.Name = request.Name.Trim();
        system.SystemOwner = request.SystemOwner.Trim();
        system.RecoveryTimeObjectiveHours = request.RecoveryTimeObjectiveHours;
        system.RecoveryPointObjectiveHours = request.RecoveryPointObjectiveHours;
        system.BackupFrequency = request.BackupFrequency.Trim();
        system.LastBackupTestDateUtc = request.LastBackupTestDateUtc;
        system.LastDisasterRecoveryTestDateUtc = request.LastDisasterRecoveryTestDateUtc;
        system.DowntimeImpact = request.DowntimeImpact.Trim();
        system.ContinuityScore = request.ContinuityScore;
        system.Status = request.Status.Trim();
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Critical system updated", "CriticalSystem", system.Id, system.Name);
        return NoContent();
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost("{planId:guid}/systems/{id:guid}/recovery-test")]
    public async Task<IActionResult> RecordRecoveryTest(Guid planId, Guid id, RecoveryTestRequest request)
    {
        var system = await OwnedSystemAsync(planId, id);
        if (system is null) return NotFound();
        if (request.TestedAtUtc > DateTime.UtcNow.AddMinutes(5) ||
            request.ContinuityScore is < 0 or > 100 ||
            string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Recovery test data is invalid." });
        }

        system.LastDisasterRecoveryTestDateUtc = request.TestedAtUtc;
        system.ContinuityScore = request.ContinuityScore;
        system.Status = request.Status.Trim();
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(
            User,
            HttpContext,
            "Recovery test recorded",
            "CriticalSystem",
            system.Id,
            $"{system.Name}: {request.ContinuityScore:0.##}% - {request.Notes.Trim()}");
        return NoContent();
    }

    public sealed record ContinuityRequest(
        Guid OrganizationId,
        string Name,
        string Owner,
        decimal ContinuityScore,
        RiskGuard.Domain.Enums.RecordStatus Status);

    private Task<CriticalSystem?> OwnedSystemAsync(Guid planId, Guid id) =>
        db.CriticalSystems.FirstOrDefaultAsync(x =>
            x.Id == id &&
            x.BusinessContinuityPlanId == planId &&
            x.Plan != null &&
            x.Plan.OrganizationId == User.OrganizationId());

    private static bool ValidSystem(CriticalSystemRequest request) =>
        !string.IsNullOrWhiteSpace(request.Name) &&
        !string.IsNullOrWhiteSpace(request.SystemOwner) &&
        !string.IsNullOrWhiteSpace(request.BackupFrequency) &&
        !string.IsNullOrWhiteSpace(request.Status) &&
        request.RecoveryTimeObjectiveHours is >= 0 and <= 8760 &&
        request.RecoveryPointObjectiveHours is >= 0 and <= 8760 &&
        request.ContinuityScore is >= 0 and <= 100;
}
