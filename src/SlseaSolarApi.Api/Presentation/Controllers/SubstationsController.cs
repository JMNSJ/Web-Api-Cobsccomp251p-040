using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlseaSolarApi.Api.Application.Dtos;
using SlseaSolarApi.Api.Infrastructure.Persistence;

namespace SlseaSolarApi.Api.Presentation.Controllers;

[ApiController]
[Route("api/substations")]
public class SubstationsController : ApiControllerBase
{
    private readonly SolarDbContext _db;

    public SubstationsController(SolarDbContext db) => _db = db;

    /// <summary>Lists all grid substations.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubstations(CancellationToken cancellationToken)
    {
        var substations = await _db.GridSubstations
            .AsNoTracking()
            .OrderBy(s => s.Id)
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

    /// <summary>Gets one grid substation.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubstation(int id, CancellationToken cancellationToken)
    {
        var substation = await _db.GridSubstations
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new GridSubstationDto
            {
                Id = s.Id,
                Name = s.Name,
                Code = s.Code,
                CapacityKw = s.CapacityKw,
                DistrictId = s.DistrictId,
                DistrictName = s.District.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (substation is null)
        {
            return NotFound(ProvincesController.NotFoundBody("grid substation", id));
        }

        return ConditionalOk(substation);
    }

    /// <summary>Lists the solar installations connected to one substation.</summary>
    [HttpGet("{id:int}/installations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubstationInstallations(int id, CancellationToken cancellationToken)
    {
        var exists = await _db.GridSubstations.AnyAsync(s => s.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound(ProvincesController.NotFoundBody("grid substation", id));
        }

        var installations = await _db.SolarInstallations
            .AsNoTracking()
            .Where(i => i.GridSubstationId == id)
            .OrderBy(i => i.Id)
            .Select(i => new SolarInstallationDto
            {
                Id = i.Id,
                Name = i.Name,
                Reference = i.Reference,
                MeterId = i.MeterId,
                InverterId = i.InverterId,
                CapacityKw = i.CapacityKw,
                IsActive = i.IsActive,
                CommissionedAt = i.CommissionedAt,
                GridSubstationId = i.GridSubstationId,
                GridSubstationName = i.GridSubstation.Name,
                DistrictId = i.GridSubstation.DistrictId,
                DistrictName = i.GridSubstation.District.Name,
                ProvinceId = i.GridSubstation.District.ProvinceId,
                ProvinceName = i.GridSubstation.District.Province.Name
            })
            .ToListAsync(cancellationToken);

        return ConditionalOk(installations);
    }
}
