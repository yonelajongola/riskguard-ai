using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RiskGuard.API.Controllers;
using RiskGuard.Application.DTOs;
using RiskGuard.Domain.Entities;
using RiskGuard.Domain.Enums;
using RiskGuard.Persistence;

namespace RiskGuard.IntegrationTests;

public sealed class ContinuityWorkflowTests
{
    [Fact]
    public async Task CriticalSystemAndRecoveryTest_AreTenantScopedPersistedAndAudited()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RiskGuardDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new RiskGuardDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var organization = new Organization { Name = "Continuity Test" };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        var plan = new BusinessContinuityPlan
        {
            OrganizationId = organization.Id,
            Name = "Enterprise recovery",
            Owner = "Operations",
            Status = RecordStatus.Active
        };
        db.BusinessContinuityPlans.Add(plan);
        await db.SaveChangesAsync();

        var controller = new ContinuityController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = AuthenticatedContext(Guid.NewGuid(), organization.Id)
            }
        };
        var create = await controller.CreateSystem(
            plan.Id,
            new CriticalSystemRequest(
                "Payment gateway",
                "Platform Lead",
                2,
                1,
                "Hourly",
                null,
                null,
                "Revenue processing unavailable",
                55,
                "Needs attention"));
        var created = create.Result.Should().BeOfType<CreatedResult>()
            .Subject.Value.Should().BeOfType<CriticalSystem>().Subject;

        var testResult = await controller.RecordRecoveryTest(
            plan.Id,
            created.Id,
            new RecoveryTestRequest(
                DateTime.UtcNow.AddMinutes(-5),
                88,
                "Ready",
                "Restore and failover completed."));
        testResult.Should().BeOfType<NoContentResult>();

        var persisted = await db.CriticalSystems.AsNoTracking().SingleAsync();
        persisted.ContinuityScore.Should().Be(88);
        persisted.Status.Should().Be("Ready");
        persisted.LastDisasterRecoveryTestDateUtc.Should().NotBeNull();
        (await db.AuditLogs.CountAsync()).Should().Be(2);
    }

    private static DefaultHttpContext AuthenticatedContext(Guid userId, Guid organizationId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "admin@continuity.test"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("organization_id", organizationId.ToString())
        ], "Test"));
        return context;
    }
}
