using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Services;
using RiskGuard.Application.DTOs;
using RiskGuard.Domain.Entities;
using RiskGuard.Persistence;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations")]
public sealed class OrganizationsController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll() =>
        Ok(await db.Organizations.AsNoTracking()
            .Include(x => x.Departments)
            .Where(x => x.Id == User.OrganizationId())
            .OrderBy(x => x.Name)
            .ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id)
    {
        var organization = await db.Organizations.AsNoTracking()
            .Include(x => x.Departments)
            .FirstOrDefaultAsync(x => x.Id == id && x.Id == User.OrganizationId());
        return organization is null ? NotFound() : Ok(organization);
    }

    [Authorize(Policy = "Administrators")]
    [HttpPost]
    public async Task<ActionResult> Create(CreateOrganizationRequest request)
    {
        if (User.OrganizationId().HasValue)
        {
            return Conflict(new { message = "This account already belongs to a workspace." });
        }
        var organization = new Organization
        {
            Name = request.Name.Trim(),
            Industry = request.Industry.Trim(),
            Country = request.Country.Trim(),
            EmployeeCount = request.EmployeeCount,
            RegistrationNumber = request.RegistrationNumber.Trim(),
            PrimaryContact = request.PrimaryContact.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim()
        };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Organization created", "Organization", organization.Id, $"Created {organization.Name}.");
        return CreatedAtAction(nameof(Get), new { id = organization.Id }, organization);
    }

    [Authorize(Policy = "Administrators")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateOrganizationRequest request)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(x => x.Id == id && x.Id == User.OrganizationId());
        if (organization is null) return NotFound();
        organization.Name = request.Name.Trim();
        organization.Industry = request.Industry.Trim();
        organization.Country = request.Country.Trim();
        organization.EmployeeCount = request.EmployeeCount;
        organization.RegistrationNumber = request.RegistrationNumber.Trim();
        organization.PrimaryContact = request.PrimaryContact.Trim();
        organization.Email = request.Email.Trim();
        organization.Phone = request.Phone.Trim();
        organization.Address = request.Address.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "Administrators")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(x => x.Id == id && x.Id == User.OrganizationId());
        if (organization is null) return NotFound();
        organization.Status = RiskGuard.Domain.Enums.RecordStatus.Archived;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/departments")]
public sealed class DepartmentsController(RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] Guid? organizationId)
    {
        var query = db.Departments.AsNoTracking().AsQueryable();
        var currentOrganizationId = User.OrganizationId();
        if (!currentOrganizationId.HasValue) return Ok(Array.Empty<Department>());
        query = query.Where(x => x.OrganizationId == currentOrganizationId.Value);
        if (organizationId.HasValue && organizationId != currentOrganizationId) return Forbid();
        return Ok(await query.OrderBy(x => x.Name).ToListAsync());
    }

    [Authorize(Policy = "Administrators")]
    [HttpPost]
    public async Task<ActionResult> Create(CreateDepartmentRequest request)
    {
        if (request.OrganizationId != User.OrganizationId()) return Forbid();
        var department = new Department
        {
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            ManagerName = request.ManagerName.Trim(),
            EmployeeCount = request.EmployeeCount,
            BusinessFunction = request.BusinessFunction.Trim(),
            Criticality = request.Criticality,
            RiskOwner = request.RiskOwner.Trim()
        };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return Created($"/api/departments/{department.Id}", department);
    }

    [Authorize(Roles = "Admin,Risk Manager")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateDepartmentRequest request)
    {
        var department = await db.Departments.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == User.OrganizationId());
        if (department is null) return NotFound();
        department.Name = request.Name.Trim();
        department.ManagerName = request.ManagerName.Trim();
        department.EmployeeCount = request.EmployeeCount;
        department.BusinessFunction = request.BusinessFunction.Trim();
        department.Criticality = request.Criticality;
        department.RiskOwner = request.RiskOwner.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "Administrators")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var department = await db.Departments.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == User.OrganizationId());
        if (department is null) return NotFound();
        department.Status = RiskGuard.Domain.Enums.RecordStatus.Archived;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
