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

    public sealed record ContinuityRequest(
        Guid OrganizationId,
        string Name,
        string Owner,
        decimal ContinuityScore,
        RiskGuard.Domain.Enums.RecordStatus Status);
}
