using Microsoft.Extensions.Logging;
using RiskGuard.Application.Interfaces;

namespace RiskGuard.Infrastructure.Services;

public sealed class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email placeholder invoked for {Recipient}. Subject: {Subject}. Body length: {BodyLength}",
            recipient,
            subject,
            body.Length);
        return Task.CompletedTask;
    }
}

public sealed class LocalFileStorageService : IFileStorageService
{
    public async Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(root);
        var safeName = $"{Guid.NewGuid():N}-{Path.GetFileName(fileName)}";
        var path = Path.Combine(root, safeName);
        await using var destination = File.Create(path);
        await stream.CopyToAsync(destination, cancellationToken);
        return $"/uploads/{safeName}";
    }
}
