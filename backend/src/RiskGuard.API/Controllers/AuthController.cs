using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Services;
using RiskGuard.Application.DTOs;
using RiskGuard.Application.Interfaces;
using RiskGuard.Domain.Entities;
using RiskGuard.Persistence;
using RiskGuard.Persistence.Identity;

namespace RiskGuard.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    RiskGuardDbContext db,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (!configuration.GetValue("Authentication:AllowPublicRegistration", false))
        {
            return NotFound();
        }
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }
        if (await db.Organizations.AnyAsync(x => x.Name == request.OrganizationName.Trim()))
        {
            return Conflict(new { message = "A workspace with this name already exists." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var organization = new Organization
        {
            Name = request.OrganizationName.Trim(),
            Industry = "Not specified",
            Country = "Not specified",
            PrimaryContact = $"{request.FirstName.Trim()} {request.LastName.Trim()}",
            Email = request.Email.Trim().ToLowerInvariant()
        };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        var department = new Department
        {
            OrganizationId = organization.Id,
            Name = "General",
            ManagerName = $"{request.FirstName.Trim()} {request.LastName.Trim()}",
            EmployeeCount = 1,
            BusinessFunction = "Enterprise administration",
            Criticality = RiskGuard.Domain.Enums.CriticalityLevel.High,
            RiskOwner = $"{request.FirstName.Trim()} {request.LastName.Trim()}"
        };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email.Trim().ToLowerInvariant(),
            Email = request.Email.Trim().ToLowerInvariant(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = !configuration.GetValue("Authentication:RequireConfirmedEmail", false),
            OrganizationId = organization.Id,
            DepartmentId = department.Id
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }

        await userManager.AddToRoleAsync(user, "Admin");
        await transaction.CommitAsync();
        return Ok(await IssueTokensAsync(user));
    }

    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            await WriteFailedLoginAsync(request.Email);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await WriteFailedLoginAsync(request.Email);
            return Unauthorized(new { message = "Invalid email or password." });
        }
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            await WriteFailedLoginAsync(request.Email);
            return Unauthorized(new { message = "Invalid email or password." });
        }
        if (configuration.GetValue("Authentication:RequireConfirmedEmail", false) &&
            !await userManager.IsEmailConfirmedAsync(user))
        {
            return Unauthorized(new { message = "Confirm your email before signing in." });
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var response = await IssueTokensAsync(user);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id.ToString(),
            UserEmail = user.Email ?? string.Empty,
            Action = "Login",
            EntityType = "ApplicationUser",
            EntityId = user.Id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Description = "User authenticated successfully."
        });
        await db.SaveChangesAsync();
        return Ok(response);
    }

    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var hash = tokenService.HashToken(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == hash && token.RevokedAtUtc == null);
        if (stored is null || !stored.IsActive || !Guid.TryParse(stored.UserId, out var userId))
        {
            return Unauthorized(new { message = "Refresh token is invalid or expired." });
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        stored.RevokedAtUtc = DateTime.UtcNow;
        var response = await IssueTokensAsync(user);
        await transaction.CommitAsync();
        return Ok(response);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var hash = tokenService.HashToken(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(item => item.TokenHash == hash);
        if (token is not null && token.UserId == User.UserId())
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        await db.WriteAuditAsync(User, HttpContext, "Logout", "ApplicationUser", User.Identity?.Name ?? "current", "User logged out.");
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        string? resetToken = null;
        if (user is not null)
        {
            resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        }

        return Accepted(new
        {
            message = "If the account exists, password reset instructions will be sent.",
            resetToken = environment.IsDevelopment() ? resetToken : null
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return BadRequest(new { message = "The reset request is invalid or expired." });
        }
        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }
        var activeTokens = await db.RefreshTokens
            .Where(token => token.UserId == user.Id.ToString() && token.RevokedAtUtc == null)
            .ToListAsync();
        foreach (var token in activeTokens) token.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await userManager.FindByIdAsync(User.UserId());
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        return Ok(await ToDtoAsync(user));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await userManager.FindByIdAsync(User.UserId());
        if (user is null || !user.IsActive) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(new { message = "First name and last name are required." });
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }

        await db.WriteAuditAsync(User, HttpContext, "Profile updated", "ApplicationUser", user.Id, user.Email ?? user.Id.ToString());
        return Ok(await ToDtoAsync(user));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(User.UserId());
        if (user is null || !user.IsActive) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Current and new passwords are required." });
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }

        await db.WriteAuditAsync(User, HttpContext, "Password changed", "ApplicationUser", user.Id, "User changed their password.");
        return NoContent();
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var expires = DateTime.UtcNow.AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 30));
        var access = tokenService.CreateAccessToken(
            user.Id,
            user.Email!,
            user.FullName,
            roles,
            user.OrganizationId,
            user.DepartmentId,
            expires);
        var refresh = tokenService.CreateRefreshToken();
        var expiredTokens = await db.RefreshTokens
            .Where(token => token.UserId == user.Id.ToString() && token.ExpiresAtUtc <= DateTime.UtcNow)
            .ToListAsync();
        db.RefreshTokens.RemoveRange(expiredTokens);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id.ToString(),
            TokenHash = tokenService.HashToken(refresh),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(configuration.GetValue("Jwt:RefreshTokenDays", 7))
        });
        await db.SaveChangesAsync();
        return new AuthResponse(access, refresh, expires, await ToDtoAsync(user));
    }

    private async Task<UserDto> ToDtoAsync(ApplicationUser user) =>
        new(
            user.Id,
            user.Email!,
            user.FullName,
            (await userManager.GetRolesAsync(user)).ToArray(),
            user.OrganizationId,
            user.DepartmentId,
            user.IsActive);

    private async Task WriteFailedLoginAsync(string email)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserEmail = email,
            Action = "Failed login",
            EntityType = "ApplicationUser",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Description = "Authentication failed."
        });
        await db.SaveChangesAsync();
    }

    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
}
