using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiskGuard.Application.Interfaces;
using RiskGuard.Persistence.Repositories;

namespace RiskGuard.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddRiskGuardPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var configuredConnection = configuration.GetConnectionString("RiskGuard");
        var connectionString = string.IsNullOrWhiteSpace(configuredConnection) ||
                               configuredConnection.Equals("SQL_CONNECTION_STRING", StringComparison.Ordinal)
            ? Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING") ?? "Data Source=riskguard.db"
            : configuredConnection;

        services.AddDbContext<RiskGuardDbContext>(options =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(
                    connectionString,
                    sqlServer => sqlServer.MigrationsAssembly("RiskGuard.Persistence.SqlServerMigrations"));
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddScoped<IAssessmentRepository, AssessmentRepository>();
        return services;
    }
}
