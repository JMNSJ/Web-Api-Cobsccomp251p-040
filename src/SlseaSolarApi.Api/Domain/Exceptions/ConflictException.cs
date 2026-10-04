namespace SlseaSolarApi.Api.Domain.Exceptions;

/// <summary>
/// Raised when a write would violate a state rule, e.g. deleting a province that still has
/// districts. Maps to <b>409</b>.
/// </summary>
public sealed class ConflictException : ApiException
{
    public ConflictException(string code, string message, string? detail = null)
        : base(code, message, detail)
    {
    }

    public override int StatusCode => StatusCodes.Status409Conflict;
}