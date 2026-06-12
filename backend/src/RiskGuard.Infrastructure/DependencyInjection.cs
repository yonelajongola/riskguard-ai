using Microsoft.Extensions.DependencyInjection;
using RiskGuard.Application.Interfaces;
using RiskGuard.Infrastructure.AI;
using RiskGuard.Infrastructure.Authentication;
using RiskGuard.Infrastructure.Reporting;
using RiskGuard.Infrastructure.Services;

namespace RiskGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRiskGuardInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IEmailService, LoggingEmailService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<MockAiRiskService>();
        services.AddHttpClient<AiRiskService>(client =>
            client.Timeout = TimeSpan.FromSeconds(45));
        services.AddScoped<IAiRiskService>(provider =>
            provider.GetRequiredService<AiRiskService>());
        return services;
    }
}
