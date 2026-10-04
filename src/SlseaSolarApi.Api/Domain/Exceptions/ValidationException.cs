namespace SlseaSolarApi.Api.Domain.Exceptions;

/// <summary>
/// Raised when a request parses but fails domain validation. Maps to <b>422</b>.
/// </summary>
/// <remarks>
/// Design decision D5: 422 is kept distinct from 400. 400 means the request could not be parsed;
/// 422 means it parsed but broke a domain rule. The client learns whether to fix syntax or semantics.
/// </remarks>
public sealed class ValidationException : ApiException
{
    public ValidationException(string code, string message, string? detail = null)
        : base(code, message, detail)
    {
    }

    /// <summary>Per-field validation messages, keyed by property name.</summary>
    public IDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
}