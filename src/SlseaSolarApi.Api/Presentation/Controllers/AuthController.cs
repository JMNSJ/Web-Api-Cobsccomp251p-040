using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlseaSolarApi.Api.Application.Dtos;
using SlseaSolarApi.Api.Application.Security;
using SlseaSolarApi.Api.Domain.Exceptions;
using SlseaSolarApi.Api.Infrastructure.Persistence;

namespace SlseaSolarApi.Api.Presentation.Controllers;

/// <summary>
/// Token issuance. Two grants over one endpoint:
/// <list type="bullet">
///   <item><b>Device</b> — <c>meterId</c> + <c>deviceSecret</c> → a token bound to that installation.
///     The secret comes from the host environment (<c>Solar__DeviceSecret</c>); it is never stored in
///     git and the database holds only its salted PBKDF2 hash.</item>
///   <item><b>SLSEA user</b> — <c>username</c> + <c>password</c> → a read-only token carrying the
///     user's jurisdiction claims (role national/provincial/district + scope ids).</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SolarDbContext _db;
    private readonly JwtTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AuthController(
        SolarDbContext db,
        JwtTokenService tokenService,
        IConfiguration configuration)
    {
        _db = db;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    /// <summary>Exchanges credentials for a JWT access token.</summary>
    /// <remarks>
    /// Device grant: <c>{"meterId": "MTR-000001", "deviceSecret": "&lt;from environment&gt;"}</c><br/>
    /// User grant: <c>{"username": "national", "password": "&lt;seeded demo password&gt;"}</c>
    /// (see the project README for the demo credentials).<br/>
    /// 401 is returned for unknown identifiers and wrong secrets alike — the response does not
    /// reveal which part failed.
    /// </remarks>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateToken([FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        // --- Device grant ---------------------------------------------------
        if (!string.IsNullOrWhiteSpace(request.MeterId))
        {
            if (string.IsNullOrWhiteSpace(request.DeviceSecret))
            {
                throw new ValidationException(
                    "MISSING_DEVICE_SECRET",
                    "A device grant requires deviceSecret alongside meterId.");
            }

            var installation = await _db.SolarInstallations
                .FirstOrDefaultAsync(i => i.MeterId == request.MeterId, cancellationToken);

            var configuredSecret = _configuration["Solar:DeviceSecret"];

            if (installation is null || !installation.IsActive ||
                string.IsNullOrEmpty(configuredSecret) ||
                !DeviceSecretHasher.Verify(request.DeviceSecret, installation.DeviceSecretHash) ||
                !string.Equals(request.DeviceSecret, configuredSecret, StringComparison.Ordinal))
            {
                // Same answer for unknown meter, wrong secret and inactive installation.
                throw new UnauthorizedException("Invalid device credentials.");
            }

            var deviceToken = _tokenService.CreateDeviceToken(installation);
            return Ok(new TokenResponse
            {
                AccessToken = deviceToken,
                Role = "device",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(
                    _configuration.GetValue("Jwt:DeviceTokenLifetimeDays", 365))
            });
        }

        // --- SLSEA user grant -----------------------------------------------
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new ValidationException(
                    "MISSING_PASSWORD",
                    "A user grant requires password alongside username.");
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

            if (user is null || !user.IsActive ||
                !Infrastructure.Persistence.Seeding.PasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedException("Invalid username or password.");
            }

            var userToken = _tokenService.CreateUserToken(user);
            return Ok(new TokenResponse
            {
                AccessToken = userToken,
                Role = user.Role.ToString().ToLowerInvariant(),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(
                    _configuration.GetValue("Jwt:UserTokenLifetimeHours", 8))
            });
        }

        throw new ValidationException(
            "MISSING_CREDENTIALS",
            "Provide either (meterId, deviceSecret) or (username, password).");
    }
}
