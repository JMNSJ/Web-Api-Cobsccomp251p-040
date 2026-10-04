namespace SlseaSolarApi.Api.Domain.Enums;

/// <summary>
/// The jurisdiction scope of an SLSEA read-client. Serialised as a lower-case string on the wire
/// (<c>national</c>, <c>provincial</c>, <c>district</c>) to match the JWT <c>role</c> claim.
/// </summary>
public enum UserRole
{
    /// <summary>Sees all provinces, districts, substations, installations and readings.</summary>
    National = 0,

    /// <summary>Sees only the rows inside their province.</summary>
    Provincial = 1,

    /// <summary>Sees only the rows inside their district.</summary>
    District = 2
}