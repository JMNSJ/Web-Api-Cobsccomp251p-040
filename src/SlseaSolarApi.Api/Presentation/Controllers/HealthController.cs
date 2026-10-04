using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlseaSolarApi.Api.Infrastructure.Persistence;

namespace SlseaSolarApi.Api.Presentation.Controllers;

/// <summary>
/// Liveness and readiness probe. The host platform uses this to decide whether the container is
/// healthy before routing traffic to it.
/// </summary>
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly SolarDbContext _db;

    public HealthController(SolarDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Reports that the process is up and can reach its database.
    /// </summary>
    /// <remarks>
    /// Deliberately returns 200 only when the database answers, so a deployment is never marked
    /// healthy while its data layer is unreachable.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var canConnect = await _db.Database.CanConnectAsync(cancellationToken);

        if (!canConnect)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                database = "unreachable"
            });
        }

        return Ok(new
        {
            status = "healthy",
            database = "reachable",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}