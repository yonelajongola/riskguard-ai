using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskGuard.Persistence;

namespace RiskGuard.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public sealed class HealthController(
    RiskGuardDbContext db,
    IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseAvailable = false;
        try
        {
            databaseAvailable = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            // The response reports dependency status without exposing connection details.
        }

        var response = new
        {
            apiStatus = databaseAvailable ? "Healthy" : "Degraded",
            databaseStatus = databaseAvailable ? "Healthy" : "Unavailable",
            environment = environment.EnvironmentName,
            timestamp = DateTime.UtcNow
        };

        return databaseAvailable
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
