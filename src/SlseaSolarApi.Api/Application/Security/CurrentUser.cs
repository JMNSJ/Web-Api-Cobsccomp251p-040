using System.Security.Claims;
using SlseaSolarApi.Api.Domain.Enums;
using SlseaSolarApi.Api.Domain.Exceptions;

namespace SlseaSolarApi.Api.Application.Security;

/// <summary>
/// The authenticated caller, extracted from the JWT. Nothing in the request body or URL is ever
/// trusted for authorization decisions — only these claims.
/// </summary>
public record CurrentUser
{
    /// <summary><c>device</c>, <c>national</c>, <c>provincial</c> or <c>district</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Set only for device tokens: the single installation they may write to.</summary>
    public int? InstallationId { get; init; }

    /// <summary>Set only for provincial users.</summary>
    public int? ProvinceId { get; init; }

    /// <summary>Set only for district users.</summary>
    public int? DistrictId { get; init; }

    public static CurrentUser FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        int? InstallationId() =>
            int.TryParse(principal.FindFirst("installationId")?.Value, out var installationId)
                ? installationId
                : null;

        int? ProvinceId() =>
            int.TryParse(principal.FindFirst("provinceId")?.Value, out var provinceId)
                ? provinceId
                : null;

        int? DistrictId() =>
            int.TryParse(principal.FindFirst("districtId")?.Value, out var districtId)
                ? districtId
                : null;

        var role = principal.FindFirst("role")?.Value
            ?? principal.FindFirst(ClaimTypes.Role)?.Value
            ?? throw new UnauthorizedException("Token carries no role claim.");

        return new CurrentUser
        {
            Role = role,
            InstallationId = InstallationId(),
            ProvinceId = ProvinceId(),
            DistrictId = DistrictId()
        };
    }

    public bool IsDevice => Role == "device";

    public bool IsNational => Role == "national";

    /// <summary>True for any SLSEA read user (national, provincial or district).</summary>
    public bool IsReader =>
        Role is "national" or "provincial" or "district";

    public Domain.Enums.UserRole? UserRole => Role switch
    {
        "national" => Domain.Enums.UserRole.National,
        "provincial" => Domain.Enums.UserRole.Provincial,
        "district" => Domain.Enums.UserRole.District,
        _ => null
    };
}
