using SlseaSolarApi.Api.Domain.Enums;

namespace SlseaSolarApi.Api.Domain.Entities;

/// <summary>
/// An SLSEA person with a role and a jurisdiction scope. This is the read-client actor.
/// </summary>
/// <remarks>
/// A user belongs to exactly one scope, expressed by <see cref="Role"/> plus the optional
/// <see cref="ProvinceId"/> / <see cref="DistrictId"/> that the role implies:
/// <list type="bullet">
///   <item><description><c>national</c> — no scope ids; sees everything.</description></item>
///   <item><description><c>provincial</c> — <see cref="ProvinceId"/> set; sees one province.</description></item>
///   <item><description><c>district</c> — <see cref="DistrictId"/> set; sees one district.</description></item>
/// </list>
/// </remarks>
public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    /// <summary>Display name, safe to show in an audit trail.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Password hash. Stored as a salted hash; never the plaintext. Seeded users use a
    /// deterministic hash so the demo credentials are reproducible.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    /// <summary>Set only when <see cref="Role"/> is <see cref="UserRole.Provincial"/>.</summary>
    public int? ProvinceId { get; set; }
    public Province? Province { get; set; }

    /// <summary>Set only when <see cref="Role"/> is <see cref="UserRole.District"/>.</summary>
    public int? DistrictId { get; set; }
    public District? District { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}