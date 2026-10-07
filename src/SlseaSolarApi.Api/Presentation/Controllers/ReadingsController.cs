using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlseaSolarApi.Api.Application.Common;
using SlseaSolarApi.Api.Application.Dtos;
using SlseaSolarApi.Api.Application.Security;
using SlseaSolarApi.Api.Domain.Entities;
using SlseaSolarApi.Api.Domain.Exceptions;
using SlseaSolarApi.Api.Infrastructure.Persistence;

namespace SlseaSolarApi.Api.Presentation.Controllers;

/// <summary>
/// The generation-reading time series, nested under its installation. Append-only: there is
/// deliberately no PUT/PATCH/DELETE (design decision D3).
/// </summary>
[ApiController]
[Route("api/installations/{installationId:int}/readings")]
public class ReadingsController : ApiControllerBase
{
    private readonly SolarDbContext _db;

    public ReadingsController(SolarDbContext db) => _db = db;

    /// <summary>
    /// Lists one installation's readings with pagination, timestamp filtering and sorting.
    /// Query runs fully in the database (no in-memory paging).
    /// </summary>
    /// <param name="installationId">The installation whose readings to list.</param>
    /// <param name="from">Only readings at or after this timestamp.</param>
    /// <param name="to">Only readings at or before this timestamp.</param>
    /// <param name="paging">Paging and ordering options (<c>page</c>, <c>pageSize</c>, <c>order</c> = asc|desc by timestamp).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<GenerationReadingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReadings(
        int installationId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] PagedQuery paging,
        CancellationToken cancellationToken)
    {
        var user = User;

        var installation = await _db.SolarInstallations
            .AsNoTracking()
            .Include(i => i.GridSubstation.District)
            .FirstOrDefaultAsync(i => i.Id == installationId, cancellationToken);

        if (installation is null)
        {
            return NotFound(ProvincesController.NotFoundBody("installation", installationId));
        }

        JurisdictionGuard.EnsureCanSeeInstallation(user, installation);

        var query = _db.GenerationReadings
            .AsNoTracking()
            .Where(r => r.SolarInstallationId == installationId);

        // SQLite (local dev only) cannot translate DateTimeOffset comparisons or ordering, so the
        // dev path converts the timestamp window into a surrogate-Id window (Id is monotonic with
        // timestamp per installation). The lookup reads only this installation's ~672 boundary rows
        // and exists for development convenience; PostgreSQL — the deployment target — filters and
        // sorts on Timestamp directly, served by the unique (installationId, timestamp) index.
        if (DatabaseProviderSelector.IsSqlite(_db))
        {
            var idsAndTimestamps = await _db.GenerationReadings
                .AsNoTracking()
                .Where(r => r.SolarInstallationId == installationId)
                .Select(r => new { r.Id, r.Timestamp })
                .ToListAsync(cancellationToken);

            if (from is DateTimeOffset f)
            {
                var fromId = idsAndTimestamps
                    .Where(r => r.Timestamp >= f)
                    .OrderBy(r => r.Timestamp)
                    .Select(r => (long?)r.Id)
                    .FirstOrDefault();

                query = fromId is long fid
                    ? query.Where(r => r.Id >= fid)
                    : query.Where(r => false);
            }

            if (to is DateTimeOffset t)
            {
                var toId = idsAndTimestamps
                    .Where(r => r.Timestamp <= t)
                    .OrderByDescending(r => r.Timestamp)
                    .Select(r => (long?)r.Id)
                    .FirstOrDefault();

                query = toId is long tid
                    ? query.Where(r => r.Id <= tid)
                    : query.Where(r => false);
            }
        }
        else
        {
            if (from is DateTimeOffset f)
            {
                query = query.Where(r => r.Timestamp >= f);
            }

            if (to is DateTimeOffset t)
            {
                query = query.Where(r => r.Timestamp <= t);
            }
        }

        // Sort: timestamp asc or desc (default desc, newest first). All filtering/sorting/paging
        // is translated to SQL against the (installationId, timestamp) index.
        // SQLite (local dev only) cannot ORDER BY DateTimeOffset; surrogate Id increases with
        // timestamp per installation, so it orders equivalently there. Postgres — the deployment
        // target — orders by timestamp directly.
        var isSqlite = DatabaseProviderSelector.IsSqlite(_db);
        query = paging.IsAscending
            ? (isSqlite ? query.OrderBy(r => r.Id) : query.OrderBy(r => r.Timestamp))
            : (isSqlite ? query.OrderByDescending(r => r.Id) : query.OrderByDescending(r => r.Timestamp));

        var page = await query
            .Select(r => new GenerationReadingDto
            {
                Id = r.Id,
                InstallationId = r.SolarInstallationId,
                Timestamp = r.Timestamp,
                PowerKw = r.PowerKw,
                EnergyKwh = r.EnergyKwh,
                Voltage = r.Voltage
            })
            .ToPagedResultAsync(paging, BuildPageLink, cancellationToken);

        return Ok(page);
    }

    /// <summary>
    /// Ingests a new reading. Device-only: the token must be bound to this installation
    /// (a device pushing for another installation gets 403). Returns 201 with a Location header.
    /// </summary>
    /// <remarks>
    /// Validation: timestamp must not be in the future; power must be non-negative and within
    /// nameplate capacity; energy and voltage non-negative; voltage within 0-1000 V.
    /// A retransmitted timestamp already held for this installation yields 409 Conflict.
    /// </remarks>
    /// <param name="installationId">The installation to ingest the reading for (from the route).</param>
    /// <param name="request">The reading body.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostReading(
        int installationId,
        [FromBody] CreateReadingRequest request,
        CancellationToken cancellationToken)
    {
        var user = User;
        JurisdictionGuard.EnsureIsDevice(user);

        var installation = await _db.SolarInstallations
            .FirstOrDefaultAsync(i => i.Id == installationId, cancellationToken);

        if (installation is null)
        {
            return NotFound(ProvincesController.NotFoundBody("installation", installationId));
        }

        // The key check of the security model: token installation vs route installation.
        JurisdictionGuard.EnsureCanWriteInstallation(user, installation);

        if (!installation.IsActive)
        {
            throw new ValidationException(
                "INSTALLATION_INACTIVE",
                "This installation is deactivated and cannot accept readings.",
                $"Installation {installationId} has IsActive = false.");
        }

        var errors = new Dictionary<string, string[]>();
        var now = DateTimeOffset.UtcNow;

        if (request.Timestamp == default)
        {
            errors["timestamp"] = new[] { "Timestamp is required." };
        }
        else if (request.Timestamp > now.AddMinutes(5))
        {
            errors["timestamp"] = new[] { "Timestamp cannot be more than 5 minutes in the future." };
        }

        if (request.PowerKw < 0m)
        {
            errors["powerKw"] = new[] { "Power must be non-negative." };
        }
        else if (request.PowerKw > installation.CapacityKw * 1.2m)
        {
            errors["powerKw"] = new[]
            {
                $"Power {request.PowerKw} kW exceeds the installation's nameplate capacity " +
                $"{installation.CapacityKw} kW (120% tolerance)."
            };
        }

        if (request.EnergyKwh < 0m)
        {
            errors["energyKwh"] = new[] { "Energy must be non-negative." };
        }

        if (request.Voltage <= 0m || request.Voltage > 1000m)
        {
            errors["voltage"] = new[] { "Voltage must be between 0 and 1000 V." };
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(
                "READING_VALIDATION_FAILED",
                "The reading failed domain validation.",
                null)
            {
                Errors = errors
            };
        }

        var duplicate = await _db.GenerationReadings
            .AnyAsync(r => r.SolarInstallationId == installationId && r.Timestamp == request.Timestamp,
                cancellationToken);

        if (duplicate)
        {
            throw new DuplicateReadingException(installationId, request.Timestamp);
        }

        var reading = new GenerationReading
        {
            SolarInstallationId = installationId,
            Timestamp = request.Timestamp,
            PowerKw = request.PowerKw,
            EnergyKwh = request.EnergyKwh,
            Voltage = request.Voltage,
            IngestedAt = now,
            SequenceNo = request.SequenceNo ?? await _db.GenerationReadings
                .Where(r => r.SolarInstallationId == installationId)
                .MaxAsync(r => (long?)r.SequenceNo, cancellationToken) + 1 ?? 0
        };

        _db.GenerationReadings.Add(reading);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetLatestReading),
            new { installationId },
            GenerationReadingDto.From(reading));
    }

    /// <summary>
    /// The newest reading for the installation, derived from the time series with an
    /// <c>ORDER BY timestamp DESC LIMIT 1</c> query — never a stored duplicate.
    /// </summary>
    [HttpGet("latest")]
    [HttpGet("/api/installations/{installationId:int}/latest-reading")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestReading(int installationId, CancellationToken cancellationToken)
    {
        var user = User;

        var installation = await _db.SolarInstallations
            .AsNoTracking()
            .Include(i => i.GridSubstation.District)
            .FirstOrDefaultAsync(i => i.Id == installationId, cancellationToken);

        if (installation is null)
        {
            return NotFound(ProvincesController.NotFoundBody("installation", installationId));
        }

        JurisdictionGuard.EnsureCanSeeInstallation(user, installation);

        // Provider-aware ordering: SQLite (local dev) cannot ORDER BY DateTimeOffset, so it uses
        // the surrogate Id (increases with timestamp per installation); Postgres — the deployment
        // target — orders by Timestamp.
        var isSqlite = DatabaseProviderSelector.IsSqlite(_db);

        var latest = await _db.GenerationReadings
            .AsNoTracking()
            .Where(r => r.SolarInstallationId == installationId)
            .Select(r => new GenerationReadingDto
            {
                Id = r.Id,
                InstallationId = r.SolarInstallationId,
                Timestamp = r.Timestamp,
                PowerKw = r.PowerKw,
                EnergyKwh = r.EnergyKwh,
                Voltage = r.Voltage
            })
            .OrderByDescending(dto => isSqlite ? (object)dto.Id : (object)dto.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return NotFound(new ApiError
            {
                Code = "NO_READINGS",
                Message = "This installation has no readings yet.",
                Detail = $"Installation {installationId} exists but its readings collection is empty.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return ConditionalOk(latest);
    }
}
