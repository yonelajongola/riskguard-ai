using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Services;
using RiskGuard.Application.DTOs;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;
using RiskGuard.Persistence;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/incidents")]
public sealed class IncidentsController(RiskGuardDbContext db, RiskGuard.Application.Interfaces.IIncidentWorkflowService workflow) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] IncidentStatus? status)
    {
        var query = db.Incidents.AsNoTracking().Include(x => x.Department).Include(x => x.RelatedRisk).AsQueryable();
        query = query.Where(x =>
            x.Department != null && x.Department.OrganizationId == User.OrganizationId() ||
            x.RelatedRisk != null && x.RelatedRisk.Assessment != null &&
            x.RelatedRisk.Assessment.OrganizationId == User.OrganizationId());
        if ((User.IsInRole("Department Manager") || User.IsInRole("Employee")) && User.DepartmentId().HasValue)
        {
            query = query.Where(x => x.DepartmentId == User.DepartmentId());
        }
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return Ok(await query.OrderByDescending(x => x.Severity).ThenByDescending(x => x.DetectedAtUtc).ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id)
    {
        var incident = await db.Incidents.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.RelatedRisk)
            .Include(x => x.Comments.OrderBy(comment => comment.CreatedAtUtc))
            .FirstOrDefaultAsync(x => x.Id == id &&
                (x.Department != null && x.Department.OrganizationId == User.OrganizationId() ||
                 x.RelatedRisk != null && x.RelatedRisk.Assessment != null &&
                 x.RelatedRisk.Assessment.OrganizationId == User.OrganizationId()) &&
                (!User.IsInRole("Employee") && !User.IsInRole("Department Manager") ||
                 x.DepartmentId == User.DepartmentId()));
        return incident is null ? NotFound() : Ok(incident);
    }

    [Authorize(Roles = "Admin,Risk Manager,Security Analyst,Compliance Officer,Department Manager")]
    [HttpPost]
    public async Task<ActionResult> Create(CreateIncidentRequest request)
    {
        if (request.DepartmentId.HasValue &&
            !await db.Departments.AnyAsync(x => x.Id == request.DepartmentId && x.OrganizationId == User.OrganizationId()) ||
            request.RiskItemId.HasValue &&
            !await db.Risks.AnyAsync(x => x.Id == request.RiskItemId &&
                x.Assessment != null && x.Assessment.OrganizationId == User.OrganizationId()))
        {
            return BadRequest(new { message = "Department or related risk is invalid." });
        }
        var incident = new Incident
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category,
            Severity = request.Severity,
            Owner = request.Owner.Trim(),
            DepartmentId = request.DepartmentId,
            RiskItemId = request.RiskItemId,
            DueDateUtc = request.DueDateUtc,
            EvidenceNotes = request.EvidenceNotes
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Incident created", "Incident", incident.Id, incident.Title);
        return CreatedAtAction(nameof(Get), new { id = incident.Id }, incident);
    }

    [Authorize(Roles = "Admin,Risk Manager,Security Analyst,Compliance Officer,Department Manager")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateIncidentRequest request)
    {
        var incident = await FindOwnedAsync(id);
        if (incident is null) return NotFound();
        incident.Title = request.Title.Trim();
        incident.Description = request.Description.Trim();
        incident.Category = request.Category;
        incident.Severity = request.Severity;
        incident.Owner = request.Owner.Trim();
        incident.DepartmentId = request.DepartmentId;
        incident.RiskItemId = request.RiskItemId;
        incident.DueDateUtc = request.DueDateUtc;
        incident.EvidenceNotes = request.EvidenceNotes;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult> AddComment(Guid id, IncidentCommentRequest request)
    {
        if (await FindOwnedAsync(id) is null) return NotFound();
        var comment = new IncidentComment
        {
            IncidentId = id,
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            AuthorName = User.Identity?.Name ?? "RiskGuard user",
            Comment = request.Comment.Trim()
        };
        db.IncidentComments.Add(comment);
        await db.SaveChangesAsync();
        return Ok(comment);
    }

    [Authorize(Roles = "Admin,Risk Manager,Security Analyst,Compliance Officer,Department Manager")]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, IncidentStatusRequest request)
    {
        var incident = await FindOwnedAsync(id);
        if (incident is null) return NotFound();
        workflow.Transition(incident, request.Status);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Task<Incident?> FindOwnedAsync(Guid id) =>
        db.Incidents.FirstOrDefaultAsync(x => x.Id == id &&
            (x.Department != null && x.Department.OrganizationId == User.OrganizationId() ||
             x.RelatedRisk != null && x.RelatedRisk.Assessment != null &&
             x.RelatedRisk.Assessment.OrganizationId == User.OrganizationId()) &&
            (!User.IsInRole("Employee") && !User.IsInRole("Department Manager") ||
             x.DepartmentId == User.DepartmentId()));
}
