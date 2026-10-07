using SlseaSolarApi.Api.Domain.Entities;

namespace SlseaSolarApi.Api.Application.Dtos;

/// <summary>Province resource.</summary>
public record ProvinceDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;

    public static ProvinceDto From(Province province) => new()
    {
        Id = province.Id,
        Name = province.Name,
        Code = province.Code
    };
}

/// <summary>District resource.</summary>
public record DistrictDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int ProvinceId { get; init; }
    public string ProvinceName { get; init; } = string.Empty;

    public static DistrictDto From(District district) => new()
    {
        Id = district.Id,
        Name = district.Name,
        Code = district.Code,
        ProvinceId = district.ProvinceId,
        ProvinceName = district.Province?.Name ?? string.Empty
    };
}

/// <summary>Grid substation resource.</summary>
public record GridSubstationDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public decimal CapacityKw { get; init; }
    public int DistrictId { get; init; }
    public string DistrictName { get; init; } = string.Empty;

    public static GridSubstationDto From(GridSubstation substation) => new()
    {
        Id = substation.Id,
        Name = substation.Name,
        Code = substation.Code,
        CapacityKw = substation.CapacityKw,
        DistrictId = substation.DistrictId,
        DistrictName = substation.District?.Name ?? string.Empty
    };
}

/// <summary>Solar installation resource. The meter/inverter ids are attributes (decision D1).</summary>
public record SolarInstallationDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string MeterId { get; init; } = string.Empty;
    public string? InverterId { get; init; }
    public decimal CapacityKw { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CommissionedAt { get; init; }
    public int GridSubstationId { get; init; }
    public string GridSubstationName { get; init; } = string.Empty;
    public int DistrictId { get; init; }
    public string DistrictName { get; init; } = string.Empty;
    public int ProvinceId { get; init; }
    public string ProvinceName { get; init; } = string.Empty;

    public static SolarInstallationDto From(SolarInstallation installation) => new()
    {
        Id = installation.Id,
        Name = installation.Name,
        Reference = installation.Reference,
        MeterId = installation.MeterId,
        InverterId = installation.InverterId,
        CapacityKw = installation.CapacityKw,
        IsActive = installation.IsActive,
        CommissionedAt = installation.CommissionedAt,
        GridSubstationId = installation.GridSubstationId,
        GridSubstationName = installation.GridSubstation?.Name ?? string.Empty,
        DistrictId = installation.GridSubstation?.DistrictId ?? 0,
        DistrictName = installation.GridSubstation?.District?.Name ?? string.Empty,
        ProvinceId = installation.GridSubstation?.District?.ProvinceId ?? 0,
        ProvinceName = installation.GridSubstation?.District?.Province?.Name ?? string.Empty
    };
}

/// <summary>Generation reading resource — one point of the time series.</summary>
public record GenerationReadingDto
{
    public long Id { get; init; }
    public int InstallationId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public decimal PowerKw { get; init; }
    public decimal EnergyKwh { get; init; }
    public decimal Voltage { get; init; }

    public static GenerationReadingDto From(GenerationReading reading) => new()
    {
        Id = reading.Id,
        InstallationId = reading.SolarInstallationId,
        Timestamp = reading.Timestamp,
        PowerKw = reading.PowerKw,
        EnergyKwh = reading.EnergyKwh,
        Voltage = reading.Voltage
    };
}

/// <summary>Request body for <c>POST /api/installations/{id}/readings</c>.</summary>
public record CreateReadingRequest
{
    /// <summary>When the reading was taken, UTC.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Instantaneous power in kW (0 … installation capacity).</summary>
    public decimal PowerKw { get; init; }

    /// <summary>Cumulative energy in kWh (non-negative, monotonic within a day).</summary>
    public decimal EnergyKwh { get; init; }

    /// <summary>Voltage in V (0–1000).</summary>
    public decimal Voltage { get; init; }

    /// <summary>Optional monotonic device sequence number.</summary>
    public long? SequenceNo { get; init; }
}

/// <summary>Request body for <c>POST /api/auth/token</c>.</summary>
public record TokenRequest
{
    /// <summary>Device grant: the installation's meter id.</summary>
    public string? MeterId { get; init; }

    /// <summary>Device grant: the installation's device secret.</summary>
    public string? DeviceSecret { get; init; }

    /// <summary>SLSEA user grant: username.</summary>
    public string? Username { get; init; }

    /// <summary>SLSEA user grant: password.</summary>
    public string? Password { get; init; }
}

/// <summary>Token response. The access token is a JWT; clients send it as a Bearer header.</summary>
public record TokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType => "Bearer";
    public string Role { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Stretch goal: per-district generation summary.</summary>
public record DistrictGenerationSummaryDto
{
    public int DistrictId { get; init; }
    public string DistrictName { get; init; } = string.Empty;
    /// <summary>Sum of the newest reading's power across active installations, in kW.</summary>
    public decimal CurrentTotalPowerKw { get; init; }
    /// <summary>Sum of each installation's latest reading cumulative energy today, in kWh.</summary>
    public decimal TodayEnergyKwh { get; init; }
}
