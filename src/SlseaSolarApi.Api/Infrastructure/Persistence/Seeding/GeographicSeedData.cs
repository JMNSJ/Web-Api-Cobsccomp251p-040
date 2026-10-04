namespace SlseaSolarApi.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// A province and the districts inside it, as plain seed data. Kept as a static literal so the
/// geography is realistic and stable across runs (design decision D12).
/// </summary>
public record ProvinceSeed(string Name, string Code, DistrictSeed[] Districts);

/// <summary>A district within a province.</summary>
public record DistrictSeed(string Name, string Code);