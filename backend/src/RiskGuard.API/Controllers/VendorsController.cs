using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Services;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;
using RiskGuard.Persistence;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize(Policy = "ReadSensitive")]
[Route("api/vendors")]
public sealed class VendorsController(RiskGuardDbContext db, IVendorRiskService riskService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var vendors = await db.Vendors.AsNoTracking()
            .Include(x => x.Organization)
            .Where(x => x.OrganizationId == User.OrganizationId())
            .ToListAsync();
        return Ok(vendors.OrderByDescending(x => x.RiskScore));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id)
    {
        var vendor = await db.Vendors.AsNoTracking()
            .Include(x => x.Assessments)
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == User.OrganizationId());
        return vendor is null ? NotFound() : Ok(vendor);
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost]
    public async Task<ActionResult> Create(CreateVendorRequest request)
    {
        if (request.OrganizationId != User.OrganizationId() || !ValidRequest(request))
        {
            return BadRequest(new { message = "Vendor data is invalid for this workspace." });
        }
        var vendor = FromRequest(request);
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Vendor created", "Vendor", vendor.Id, vendor.Name);
        return CreatedAtAction(nameof(Get), new { id = vendor.Id }, vendor);
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateVendorRequest request)
    {
        var vendor = await db.Vendors.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == User.OrganizationId());
        if (vendor is null) return NotFound();
        if (!ValidRequest(request)) return BadRequest(new { message = "Vendor data is invalid." });
        vendor.Name = request.Name.Trim();
        vendor.ServiceProvided = request.ServiceProvided.Trim();
        vendor.Criticality = request.Criticality;
        vendor.ContractStartDateUtc = request.ContractStartDateUtc;
        vendor.ContractExpiryDateUtc = request.ContractExpiryDateUtc;
        vendor.ComplianceStatus = request.ComplianceStatus;
        vendor.SecurityRating = request.SecurityRating;
        vendor.DependencyLevel = request.DependencyLevel;
        vendor.Owner = request.Owner;
        vendor.Notes = request.Notes;
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Vendor updated", "Vendor", vendor.Id, vendor.Name);
        return NoContent();
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpPost("{id:guid}/calculate")]
    public async Task<ActionResult> Calculate(Guid id, VendorRiskInput request)
    {
        var vendor = await db.Vendors.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == User.OrganizationId());
        if (vendor is null) return NotFound();
        if (new[]
            {
                request.ContractExpiryRisk, request.SecurityWeakness, request.ComplianceWeakness,
                request.SingleSupplierDependency, request.ServiceReliabilityRisk, request.DataAccessRisk
            }.Any(value => value is < 0 or > 100))
        {
            return BadRequest(new { message = "Vendor risk inputs must be between 0 and 100." });
        }
        var result = riskService.Calculate(request);
        vendor.RiskScore = result.Score;
        vendor.RiskLevel = result.Level;
        db.VendorAssessments.Add(new VendorAssessment
        {
            VendorId = id,
            ContractExpiryRisk = request.ContractExpiryRisk,
            SecurityWeakness = request.SecurityWeakness,
            ComplianceWeakness = request.ComplianceWeakness,
            SingleSupplierDependency = request.SingleSupplierDependency,
            ServiceReliabilityRisk = request.ServiceReliabilityRisk,
            DataAccessRisk = request.DataAccessRisk,
            OverallScore = result.Score
        });
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Vendor risk calculated", "Vendor", vendor.Id, $"Score {result.Score:0.##}.");
        return Ok(result);
    }

    [Authorize(Policy = "RiskProfessionals")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var vendor = await db.Vendors.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == User.OrganizationId());
        if (vendor is null) return NotFound();
        db.Vendors.Remove(vendor);
        await db.SaveChangesAsync();
        await db.WriteAuditAsync(User, HttpContext, "Vendor deleted", "Vendor", vendor.Id, vendor.Name);
        return NoContent();
    }

    private static Vendor FromRequest(CreateVendorRequest request) => new()
    {
        OrganizationId = request.OrganizationId,
        Name = request.Name.Trim(),
        ServiceProvided = request.ServiceProvided.Trim(),
        Criticality = request.Criticality,
        ContractStartDateUtc = request.ContractStartDateUtc,
        ContractExpiryDateUtc = request.ContractExpiryDateUtc,
        ComplianceStatus = request.ComplianceStatus,
        SecurityRating = request.SecurityRating,
        DependencyLevel = request.DependencyLevel,
        Owner = request.Owner.Trim(),
        Notes = request.Notes
    };

    private static bool ValidRequest(CreateVendorRequest request) =>
        !string.IsNullOrWhiteSpace(request.Name) &&
        !string.IsNullOrWhiteSpace(request.ServiceProvided) &&
        !string.IsNullOrWhiteSpace(request.Owner) &&
        request.SecurityRating is >= 0 and <= 100 &&
        request.ContractExpiryDateUtc > request.ContractStartDateUtc;
}
