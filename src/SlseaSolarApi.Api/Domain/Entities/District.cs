namespace SlseaSolarApi.Api.Domain.Entities;

/// <summary>
/// Mid-level jurisdiction scope. A district belongs to exactly one
/// <see cref="Province"/> and owns many <see cref="GridSubstation"/>s.
/// </summary>
public class District
{
    public int Id { get; set; }

    /// <summary>Human-readable district name, e.g. "Colombo".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable code, e.g. "CO". Unique.</summary>
    public string Code { get; set; } = string.Empty;

    public int ProvinceId { get; set; }
    public Province Province { get; set; } = null!;

    // Navigation
    public ICollection<GridSubstation> GridSubstations { get; set; } = new List<GridSubstation>();
}