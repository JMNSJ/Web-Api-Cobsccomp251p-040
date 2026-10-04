namespace SlseaSolarApi.Api.Domain.Entities;

/// <summary>
/// Top-level jurisdiction scope. Sri Lanka has nine provinces.
/// A province owns many <see cref="District"/>s.
/// </summary>
public class Province
{
    public int Id { get; set; }

    /// <summary>Human-readable province name, e.g. "Western".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable two-letter code, e.g. "WP". Unique.</summary>
    public string Code { get; set; } = string.Empty;

    // Navigation
    public ICollection<District> Districts { get; set; } = new List<District>();
}