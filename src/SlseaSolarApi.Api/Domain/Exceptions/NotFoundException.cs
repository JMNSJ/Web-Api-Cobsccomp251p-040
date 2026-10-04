namespace SlseaSolarApi.Api.Domain.Exceptions;

/// <summary>Raised when a requested resource does not exist. Maps to <b>404</b>.</summary>
public sealed class NotFoundException : ApiException
{
    public NotFoundException(string resource, object id, string? detail = null)
        : base(
            $"{resource.ToUpperInvariant()}_NOT_FOUND",
            $"The requested {resource} was not found.",
            detail ?? $"No {resource} exists with id '{id}'.")
    {
    }

    public override int StatusCode => StatusCodes.Status404NotFound;
}