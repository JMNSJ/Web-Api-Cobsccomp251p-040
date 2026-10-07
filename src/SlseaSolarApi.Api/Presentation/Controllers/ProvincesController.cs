using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlseaSolarApi.Api.Application.Dtos;
using SlseaSolarApi.Api.Infrastructure.Persistence;
using SlseaSolarApi.Api.Presentation.Controllers;

namespace SlseaSolarApi.Api.Presentation.Controllers;

[ApiController]
[Route("api/provinces")]
public class ProvincesController : ApiControllerBase
{
    private readonly SolarDbContext _db;

    public ProvincesController(SolarDbContext db) => _db = db;

    /// <summary>Lists all provinces.</summary>
    /// <remarks>Public metadata — no authentication required (jurisdiction applies to operational data).</remarks>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProvinces(CancellationToken cancellationToken)
    {
        var provinces = await _db.Provinces
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new ProvinceDto
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code
            })
            .ToListAsync(cancellationToken);

        return ConditionalOk(provinces);
    }

    /// <summary>Gets one province.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProvince(int id, CancellationToken cancellationToken)
    {
        var province = await _db.Provinces
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProvinceDto { Id = p.Id, Name = p.Name, Code = p.Code })
            .FirstOrDefaultAsync(cancellationToken);

        if (province is null)
        {
            return NotFound(new Application.Common.ApiError
            {
                Code = "PROVINCE_NOT_FOUND",
                Message = $"The requested province was not found.",
                Detail = $"No province exists with id '{id}'.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return ConditionalOk(province);
    }

    /// <summary>Lists the districts of one province.</summary>
    [HttpGet("{id:int}/districts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProvinceDistricts(int id, CancellationToken cancellationToken)
    {
        var exists = await _db.Provinces.AnyAsync(p => p.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound(NotFoundBody("province", id));
        }

        var districts = await _db.Districts
            .AsNoTracking()
            .Where(d => d.ProvinceId == id)
            .OrderBy(d => d.Name)
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

    internal static Application.Common.ApiError NotFoundBody(string resource, object id) => new()
    {
        Code = $"{resource.ToUpperInvariant()}_NOT_FOUND",
        Message = $"The requested {resource} was not found.",
        Detail = $"No {resource} exists with id '{id}'.",
        TraceId = System.Diagnostics.Activity.Current?.Id
    };
}
