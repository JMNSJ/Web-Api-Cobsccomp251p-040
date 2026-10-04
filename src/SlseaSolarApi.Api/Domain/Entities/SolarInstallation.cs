namespace SlseaSolarApi.Api.Domain.Entities;

/// <summary>
/// A rooftop solar site — the metered asset and the write-client identity.
/// </summary>
/// <remarks>
/// Design decision D1: there is deliberately <b>no</b> <c>Device</c> entity. A device has no
/// independent identity or lifecycle in this domain; it <i>is</i> the installation's reporting
/// endpoint, so <see cref="MeterId"/> (and <see cref="InverterId"/>) are plain attributes here.
/// </remarks>
public class SolarInstallation
{
    public int Id { get; set; }

    /// <summary>Human-readable installation name, e.g. "Kandy Rooftop 042".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique installation reference shown to consumers, e.g. "INST-00042".</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// The metering device identifier. Unique across the fleet and used as the device's
    /// authentication identity (see the security model). Decision D1: an attribute, not an entity.
    /// </summary>
    public string MeterId { get; set; } = string.Empty;

    /// <summary>Optional inverter identifier, for asset-health correlation.</summary>
    public string? InverterId { get; set; }

    /// <summary>Nameplate capacity in kilowatts. Upper bound used by the seed generator.</summary>
    public decimal CapacityKw { get; set; }

    /// <summary>Whether the installation is currently reporting. Defaults to true.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When the installation was commissioned (UTC).</summary>
    public DateTimeOffset CommissionedAt { get; set; }

    public int GridSubstationId { get; set; }
    public GridSubstation GridSubstation { get; set; } = null!;

    // Navigation
    public ICollection<GenerationReading> Readings { get; set; } = new List<GenerationReading>();
}