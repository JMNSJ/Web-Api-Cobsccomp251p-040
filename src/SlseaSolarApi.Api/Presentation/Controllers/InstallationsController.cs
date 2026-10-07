using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlseaSolarApi.Api.Application.Common;
using SlseaSolarApi.Api.Application.Dtos;
using SlseaSolarApi.Api.Application.Security;
using SlseaSolarApi.Api.Domain.Entities;
using SlseaSolarApi.Api.Domain.Exceptions;
using SlseaSolarApi.Api.Infrastructure.Persistence;

namespace SlseaSolarApi.Api.Presentation.Controllers;

[ApiController]
[Route("api/installations")]
public class InstallationsController : ApiControllerBase
{
    private readonly SolarDbContext _db;

    public InstallationsController(SolarDbContext db) => _db = db;

    /// <summary>Lists all solar installations (paged).</summary>
    /// <remarks>Requires an SLSEA read user; results are scoped to the caller's jurisdiction.</remarks>
    /// <param name="provinceId">Filter by province.</param>
    /// <param name="districtId">Filter by district.</param>
    /// <param name="substationId">Filter by grid substation.</param>
    /// <param name="isActive">Filter on the active flag.</param>
    /// <param name="paging">Paging options (<c>page</c>, <c>pageSize</c>).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SolarInstallationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInstallations(
        [FromQuery] int? provinceId,
        [FromQuery] int? districtId,
        [FromQuery] int? substationId,
        [FromQuery] bool? isActive,
        [FromQuery] PagedQuery paging,
        CancellationToken cancellationToken)
    {
        var user = User;
        JurisdictionGuard.EnsureIsReader(user);

        var query = _db.SolarInstallations
            .AsNoTracking()
            .Include(i => i.GridSubstation.District.Province)
            .AsQueryable();

        // Jurisdiction scoping happens in the query, so a scoped user can never enumerate
        // out-of-scope rows no matter which ids they pass (requirement 8).
        query = ScopeToJurisdiction(user, query);

        if (provinceId is int p)
        {
            query = query.Where(i => i.GridSubstation.District.ProvinceId == p);
        }

        if (districtId is int d)
        {
            query = query.Where(i => i.GridSubstation.DistrictId == d);
        }

        if (substationId is int s)
        {
            query = query.Where(i => i.GridSubstationId == s);
        }

        if (isActive is bool active)
        {
            query = query.Where(i => i.IsActive == active);
        }

        query = query.OrderBy(i => i.Id);

        var page = await query
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
            .ToPagedResultAsync(paging, BuildPageLink, cancellationToken);

        return Ok(page);
    }

    /// <summary>Gets one solar installation.</summary>
    /// <remarks>Requires an SLSEA read user with jurisdiction, or the installation's own device.</remarks>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInstallation(int id, CancellationToken cancellationToken)
    {
        var user = User;

        var installation = await _db.SolarInstallations
            .AsNoTracking()
            .Include(i => i.GridSubstation.District.Province)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (installation is null)
        {
            return NotFound(ProvincesController.NotFoundBody("installation", id));
        }

        JurisdictionGuard.EnsureCanSeeInstallation(user, installation);

        return ConditionalOk(SolarInstallationDto.From(installation));
    }

    /// <summary>
    /// Applies the caller's jurisdiction to an installations query. National users get the query
    /// back untouched; provincial users are limited to their province; district users to their
    /// district. This is the requirement-8 guarantee — the filter is server-side, so changing an
    /// id in the URL cannot escape it.
    /// </summary>
    internal static IQueryable<SolarInstallation> ScopeToJurisdiction(CurrentUser user, IQueryable<SolarInstallation> query) =>
        user.Role switch
        {
            "provincial" => query.Where(i => i.GridSubstation.District.ProvinceId == user.ProvinceId),
            "district" => query.Where(i => i.GridSubstation.DistrictId == user.DistrictId),
            _ => query
        };
}
