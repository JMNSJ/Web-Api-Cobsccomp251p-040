namespace SlseaSolarApi.Api.Domain.Exceptions;

/// <summary>
/// Raised when the caller is authenticated but acts outside their jurisdiction. Maps to <b>403</b>.
/// </summary>
/// <remarks>
/// The canonical case is a device pushing a reading for an installation other than the one baked
/// into its token — the single most important authorization check in the project (see the security model).
/// </remarks>
public sealed class ForbiddenScopeException : ApiException
{
    public ForbiddenScopeException(string code, string message, string? detail = null)
        : base(code, message, detail)
    {
    }

    /// <summary>The device-token vs route-installation mismatch, the security model's key check.</summary>
    public static ForbiddenScopeException ReadingInstallationMismatch(object tokenInstallationId, object routeInstallationId) =>
        new(
            "READING_FOR_INSTALLATION_MISMATCH",
            "A device may only push readings for its own installation.",
            $"Token is bound to installation {tokenInstallationId} but the request targeted {routeInstallationId}.");

    /// <summary>A read-client asked for a specific resource outside their scope.</summary>
    public static ForbiddenScopeException OutOfJurisdiction(string detail) =>
        new(
            "OUT_OF_JURISDICTION",
            "You do not have jurisdiction over the requested resource.",
            detail);

    public override int StatusCode => StatusCodes.Status403Forbidden;
}