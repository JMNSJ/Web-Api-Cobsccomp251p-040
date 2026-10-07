using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SlseaSolarApi.Api.Domain.Entities;
using SlseaSolarApi.Api.Domain.Enums;

namespace SlseaSolarApi.Api.Application.Security;

/// <summary>
/// Issues the API's JWTs. There are exactly two token shapes (the security model's two actors):
/// <list type="bullet">
///   <item><description><b>Device</b> — issued by <c>POST /api/auth/token</c> with the installation's
///     meter id + device secret. Carries <c>role=device</c> and the installation id it may write to.</description></item>
///   <item><description><b>SLSEA user</b> — issued with username + password. Carries <c>role</c>
///     (<c>national|provincial|district</c>) plus the jurisdiction claims <c>provinceId</c>/<c>districtId</c>.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// The signing key comes from configuration (<c>Jwt:SigningKey</c>, supplied by the host environment
/// on Render — never committed). Device tokens live for a year so a meter does not have to
/// re-authenticate often; user tokens are short-lived (8 hours).
/// </remarks>
public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string SigningKey =>
        _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException(
                "No JWT signing key is configured. Set Jwt__SigningKey in the host environment " +
                "(Render generates one automatically) or via user secrets locally.");

    private string Issuer => _configuration["Jwt:Issuer"] ?? "SlseaSolarApi";
    private string Audience => _configuration["Jwt:Audience"] ?? "SlseaSolarApiClients";

    /// <summary>Issues a device token bound to one installation.</summary>
    public string CreateDeviceToken(SolarInstallation installation)
    {
        var lifetimeDays = _configuration.GetValue("Jwt:DeviceTokenLifetimeDays", 365);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "device"),
            new("role", "device"),
            new("installationId", installation.Id.ToString()),
            new("meterId", installation.MeterId)
        };

        return WriteToken(claims, TimeSpan.FromDays(lifetimeDays));
    }

    /// <summary>Issues an SLSEA read-client token carrying the user's jurisdiction.</summary>
    public string CreateUserToken(User user)
    {
        var lifetimeHours = _configuration.GetValue("Jwt:UserTokenLifetimeHours", 8);
        var role = user.Role switch
        {
            UserRole.National => "national",
            UserRole.Provincial => "provincial",
            _ => "district"
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new("role", role),
            new("name", user.FullName)
        };

        if (user.ProvinceId is int provinceId)
        {
            claims.Add(new Claim("provinceId", provinceId.ToString()));
        }

        if (user.DistrictId is int districtId)
        {
            claims.Add(new Claim("districtId", districtId.ToString()));
        }

        return WriteToken(claims, TimeSpan.FromHours(lifetimeHours));
    }

    private string WriteToken(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
