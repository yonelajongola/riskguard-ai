using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Services;
using RiskGuard.Application.DTOs;
using RiskGuard.Persistence;
using RiskGuard.Persistence.Identity;

namespace RiskGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    RiskGuardDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Administrators")]
    public async Task<ActionResult> GetAll()
    {
        var organizationId = User.OrganizationId();
        var users = await userManager.Users
            .Where(user => user.OrganizationId == organizationId)
            .OrderBy(user => user.Email)
            .ToListAsync();
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            result.Add(ToDto(user, await userManager.GetRolesAsync(user)));
        }
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Administrators")]
    public async Task<ActionResult<UserDto>> Get(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        return user is null || user.OrganizationId != User.OrganizationId()
            ? NotFound()
            : Ok(ToDto(user, await userManager.GetRolesAsync(user)));
    }

    [HttpGet("assignees")]
    [Authorize(Policy = "RiskProfessionals")]
    public async Task<ActionResult> GetAssignees()
    {
        var organizationId = User.OrganizationId();
        var users = await userManager.Users
            .Where(user => user.OrganizationId == organizationId && user.IsActive)
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .ToListAsync();
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            result.Add(ToDto(user, await userManager.GetRolesAsync(user)));
        }
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Administrators")]
    public async Task<ActionResult<UserDto>> Create(AdminCreateUserRequest request)
    {
        if (!await roleManager.RoleExistsAsync(request.Role))
        {
            return BadRequest(new { message = "Unknown role." });
        }
        var organizationId = User.OrganizationId();
        if (!organizationId.HasValue ||
            request.OrganizationId.HasValue && request.OrganizationId != organizationId ||
            request.DepartmentId.HasValue &&
            !await db.Departments.AnyAsync(x => x.Id == request.DepartmentId && x.OrganizationId == organizationId))
        {
            return BadRequest(new { message = "Organization or department is invalid." });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim().ToLowerInvariant(),
            UserName = request.Email.Trim().ToLowerInvariant(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = true,
            OrganizationId = organizationId,
            DepartmentId = request.DepartmentId
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }
        await userManager.AddToRoleAsync(user, request.Role);
        await db.WriteAuditAsync(User, HttpContext, "User created", "ApplicationUser", user.Id, $"Created {user.Email} as {request.Role}.");
        return CreatedAtAction(nameof(Get), new { id = user.Id }, ToDto(user, [request.Role]));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Administrators")]
    public async Task<IActionResult> Update(Guid id, AdminUpdateUserRequest request)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || user.OrganizationId != User.OrganizationId())
        {
            return NotFound();
        }
        if (!await roleManager.RoleExistsAsync(request.Role) ||
            request.DepartmentId.HasValue &&
            !await db.Departments.AnyAsync(x => x.Id == request.DepartmentId && x.OrganizationId == User.OrganizationId()))
        {
            return BadRequest(new { message = "Role or department is invalid." });
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.IsActive = request.IsActive;
        user.DepartmentId = request.DepartmentId;
        var currentRoles = await userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, request.Role);
        }
        await userManager.UpdateAsync(user);
        await db.WriteAuditAsync(User, HttpContext, "User role changed", "ApplicationUser", user.Id, $"Updated {user.Email}; role {request.Role}.");
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Administrators")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || user.OrganizationId != User.OrganizationId())
        {
            return NotFound();
        }
        if (user.Id.ToString() == User.UserId())
        {
            return Conflict(new { message = "You cannot disable your own account." });
        }
        user.IsActive = false;
        await userManager.UpdateAsync(user);
        await db.WriteAuditAsync(User, HttpContext, "User disabled", "ApplicationUser", user.Id, $"Disabled {user.Email}.");
        return NoContent();
    }

    public sealed record AdminCreateUserRequest(
        string FirstName, string LastName, string Email, string Password, string Role,
        Guid? OrganizationId, Guid? DepartmentId);

    public sealed record AdminUpdateUserRequest(
        string FirstName, string LastName, string Role, bool IsActive, Guid? DepartmentId);

    private static UserDto ToDto(ApplicationUser user, IEnumerable<string> roles) =>
        new(user.Id, user.Email!, user.FullName, roles.ToArray(), user.OrganizationId, user.DepartmentId, user.IsActive);
}
