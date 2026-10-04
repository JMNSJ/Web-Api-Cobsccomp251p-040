namespace SlseaSolarApi.Api.Domain.Exceptions;

/// <summary>Raised when credentials are missing or invalid. Maps to <b>401</b>.</summary>
public sealed class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message = "Authentication is required.", string? detail = null)
        : base("UNAUTHENTICATED", message, detail)
    {
    }

    public override int StatusCode => StatusCodes.Status401Unauthorized;
}