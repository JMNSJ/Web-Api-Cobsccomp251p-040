using SlseaSolarApi.Api.Domain.Entities;
using SlseaSolarApi.Api.Domain.Exceptions;

namespace SlseaSolarApi.Api.Application.Security;

/// <summary>
/// Enforces the jurisdiction rules (requirement 8). A national user sees everything; a provincial
/// user only their province; a district user only their district; a device only its installation.
/// Every check takes the claim-derived <see cref="CurrentUser"/> — never URL/body input alone.
/// </summary>
public static class JurisdictionGuard
{
    /// <summary>
    /// Throws <see cref="ForbiddenScopeException"/> unless the caller may see a resource that lives
    /// under <paramref name="district"/> (null for national scope).
    /// </summary>
    public static void EnsureCanSeeDistrict(CurrentUser user, District? district)
    {
        switch (user.Role)
        {
            case "national":
                return;
            case "provincial" when district is not null && district.ProvinceId == user.ProvinceId:
            case "district" when district is not null && district.Id == user.DistrictId:
                return;
            default:
                throw ForbiddenScopeException.OutOfJurisdiction(
                    $"Caller's scope is {user.Role}" +
                    (user.ProvinceId is { } p ? $" (province {p})" : string.Empty) +
                    (user.DistrictId is { } d ? $" (district {d})" : string.Empty) +
                    ", which does not cover the requested resource.");
        }
    }

    /// <summary>Convenience overload resolving the district from an installation.</summary>
    public static void EnsureCanSeeInstallation(CurrentUser user, SolarInstallation installation)
    {
        if (user.IsDevice)
        {
            // A device token is bound to exactly one installation. It may read its own
            // installation's data but nothing else.
            if (installation.Id == user.InstallationId)
            {
                return;
            }

            throw ForbiddenScopeException.ReadingInstallationMismatch(
                user.InstallationId, installation.Id);
        }

        EnsureCanSeeDistrict(user, installation.GridSubstation?.District);
    }

    /// <summary>Devices may not use read endpoints other than their own installation's data.</summary>
    public static void EnsureIsReader(CurrentUser user)
    {
        if (!user.IsReader)
        {
            throw new ForbiddenScopeException(
                "READERS_ONLY",
                "This endpoint is for SLSEA read users.",
                "Device tokens cannot list jurisdiction-wide resources; use the installation endpoints bound to your token.");
        }
    }

    /// <summary>Only devices may push readings (requirement 7).</summary>
    public static void EnsureIsDevice(CurrentUser user)
    {
        if (!user.IsDevice)
        {
            throw new ForbiddenScopeException(
                "DEVICE_TOKEN_REQUIRED",
                "Only an authenticated device may submit generation readings.",
                "SLSEA users are read-only; POST /api/installations/{id}/readings requires a device token.");
        }
    }

    /// <summary>The installation a device token is bound to, for write path checks.</summary>
    public static void EnsureCanWriteInstallation(CurrentUser user, SolarInstallation installation)
    {
        if (!user.IsDevice)
        {
            throw new ForbiddenScopeException(
                "DEVICE_TOKEN_REQUIRED",
                "Only an authenticated device may submit generation readings.",
                "SLSEA users are read-only.");
        }

        if (installation.Id != user.InstallationId)
        {
            throw ForbiddenScopeException.ReadingInstallationMismatch(
                user.InstallationId, installation.Id);
        }
    }
}
