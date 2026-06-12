using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RiskGuard.Domain.Entities;
using RiskGuard.Persistence;

namespace RiskGuard.IntegrationTests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task OrganizationAndDepartment_RoundTripThroughSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RiskGuardDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new RiskGuardDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var organization = new Organization
        {
            Name = "Integration Test Organization",
            Industry = "Technology",
            Country = "South Africa"
        };
        organization.Departments.Add(new Department { Name = "IT", RiskOwner = "IT Manager" });
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        var reloaded = await db.Organizations.Include(x => x.Departments).SingleAsync();

        reloaded.Name.Should().Be("Integration Test Organization");
        reloaded.Departments.Should().ContainSingle(x => x.Name == "IT");
    }
}
