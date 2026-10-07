using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlseaSolarApi.Api.Application.Dtos;
using SlseaSolarApi.Api.Domain.Exceptions;
using SlseaSolarApi.Api.Infrastructure.Persistence;

namespace SlseaSolarApi.Api.Presentation.Controllers;

[ApiController]
[Route("api/districts")]
public class DistrictsController : ApiControllerBase
{
    private readonly SolarDbContext _db;

    public DistrictsController(SolarDbContext db) => _db = db;

    /// <summary>Lists all districts.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDistricts(CancellationToken cancellationToken)
    {
        var districts = await _db.Districts
            .AsNoTracking()
            .OrderBy(d => d.Id)
            .Select(d => new DistrictDto
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                ProvinceId = d.ProvinceId,
                ProvinceName = d.Province.Name
            })
            .ToListAsync(cancellationToken);

        return ConditionalOk(districts);
    }

    /// <summary>Gets one district.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDistrict(int id, CancellationToken cancellationToken)
    {
        var district = await _db.Districts
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DistrictDto
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                ProvinceId = d.ProvinceId,
                ProvinceName = d.Province.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (district is null)
        {
            return NotFound(ProvincesController.NotFoundBody("district", id));
        }

        return ConditionalOk(district);
    }

    /// <summary>Lists the grid substations of one district.</summary>
    [HttpGet("{id:int}/substations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDistrictSubstations(int id, CancellationToken cancellationToken)
    {
        var district = await _db.Districts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (district is null)
        {
            return NotFound(ProvincesController.NotFoundBody("district", id));
        }

        var substations = await _db.GridSubstations
            .AsNoTracking()
            .Where(s => s.DistrictId == id)
            .OrderBy(s => s.Name)
            .Select(s => new GridSubstationDto
            {
                Id = s.Id,
                Name = s.Name,
                Code = s.Code,
                CapacityKw = s.CapacityKw,
                DistrictId = s.DistrictId,
                DistrictName = s.District.Name
            })
            .ToListAsync(cancellationToken);

        return ConditionalOk(substations);
    }

    /// <summary>
    /// Stretch goal: generation summary for a district — current total power (kW) across active
    /// installations and today's energy (kWh), computed server-side from the latest readings.
    /// </summary>
    [HttpGet("{id:int}/generation-summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGenerationSummary(int id, CancellationToken cancellationToken)
    {
        var district = await _db.Districts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (district is null)
        {
            return NotFound(ProvincesController.NotFoundBody("district", id));
        }

        // Latest reading per installation: a correlated subquery pushed into the database.
        var installations = _db.SolarInstallations
            .AsNoTracking()
            .Where(i => i.GridSubstation.DistrictId == id && i.IsActive);

        var latestPerInstallation = installations.Select(i => new
        {
            i.Id,
            Power = _db.GenerationReadings
                .Where(r => r.SolarInstallationId == i.Id)
                .OrderByDescending(r => r.Timestamp)
                .Select(r => (decimal?)r.PowerKw)
                .FirstOrDefault(),
            Energy = _db.GenerationReadings
                .Where(r => r.SolarInstallationId == i.Id && r.Timestamp >= DateTime.UtcNow.Date)
                .OrderByDescending(r => r.Timestamp)
                .Select(r => (decimal?)r.EnergyKwh)
                .FirstOrDefault()
        });

        var rows = await latestPerInstallation.ToListAsync(cancellationToken);

        var summary = new DistrictGenerationSummaryDto
        {
            DistrictId = district.Id,
            DistrictName = district.Name,
            CurrentTotalPowerKw = rows.Sum(r => r.Power ?? 0m),
            TodayEnergyKwh = rows.Sum(r => r.Energy ?? 0m)
        };

        return ConditionalOk(summary);
    }
}
