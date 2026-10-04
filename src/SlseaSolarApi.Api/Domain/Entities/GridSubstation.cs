namespace SlseaSolarApi.Api.Domain.Entities;

/// <summary>
/// The grid node that installations connect to. A substation belongs to exactly
/// one <see cref="District"/> and owns many <see cref="SolarInstallation"/>s.
/// </summary>
public class GridSubstation
{
    public int Id { get; set; }

    /// <summary>Human-readable substation name, e.g. "Pannipitiya Grid Substation".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>CEB/Ceylon Electricity Board asset code. Unique.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Installed capacity of the node in kilowatts (informational).</summary>
    public decimal CapacityKw { get; set; }

    public int DistrictId { get; set; }
    public District District { get; set; } = null!;

    // Navigation
    public ICollection<SolarInstallation> Installations { get; set; } = new List<SolarInstallation>();
}