namespace SlseaSolarApi.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// The nine Sri Lankan provinces and their twenty-five districts, used to seed the jurisdiction
/// hierarchy. This is reference geography, not generated data.
/// </summary>
public static class SriLankaGeography
{
    /// <summary>All nine provinces with their districts.</summary>
    public static readonly ProvinceSeed[] Provinces =
    {
        new("Western", "WP", new[]
        {
            new DistrictSeed("Colombo", "CO"),
            new DistrictSeed("Gampaha", "GP"),
            new DistrictSeed("Kalutara", "KT")
        }),
        new("Central", "CP", new[]
        {
            new DistrictSeed("Kandy", "KY"),
            new DistrictSeed("Matale", "MT"),
            new DistrictSeed("Nuwara Eliya", "NE")
        }),
        new("Southern", "SP", new[]
        {
            new DistrictSeed("Galle", "GL"),
            new DistrictSeed("Matara", "MR"),
            new DistrictSeed("Hambantota", "HB")
        }),
        new("Northern", "NP", new[]
        {
            new DistrictSeed("Jaffna", "JF"),
            new DistrictSeed("Kilinochchi", "KL"),
            new DistrictSeed("Mannar", "MN"),
            new DistrictSeed("Mullaitivu", "ML"),
            new DistrictSeed("Vavuniya", "VA")
        }),
        new("Eastern", "EP", new[]
        {
            new DistrictSeed("Trincomalee", "TC"),
            new DistrictSeed("Batticaloa", "BC"),
            new DistrictSeed("Ampara", "AP")
        }),
        new("North Western", "NW", new[]
        {
            new DistrictSeed("Kurunegala", "KG"),
            new DistrictSeed("Puttalam", "PU")
        }),
        new("North Central", "NC", new[]
        {
            new DistrictSeed("Anuradhapura", "AN"),
            new DistrictSeed("Polonnaruwa", "PL")
        }),
        new("Uva", "UV", new[]
        {
            new DistrictSeed("Badulla", "BD"),
            new DistrictSeed("Monaragala", "MG")
        }),
        new("Sabaragamuwa", "SG", new[]
        {
            new DistrictSeed("Ratnapura", "RP"),
            new DistrictSeed("Kegalle", "KE")
        })
    };
}