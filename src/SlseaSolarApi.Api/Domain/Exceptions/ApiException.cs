namespace SlseaSolarApi.Api.Domain.Exceptions;

/// <summary>
/// Base type for every <i>expected</i> domain failure. The global exception filter maps each
/// subclass to a status code and a stable machine-readable <c>code</c>, so no controller ever
/// hand-rolls an error body (design decision D10).
/// </summary>
public abstract class ApiException : Exception
{
    protected ApiException(string code, string message, string? detail = null, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
        Detail = detail;
    }

    /// <summary>Stable <c>SCREAMING_SNAKE_CASE</c> code that clients switch on.</summary>
    public string Code { get; }

    /// <summary>Optional supporting context for developers. Never leaks internals.</summary>
    public string? Detail { get; }

    /// <summary>The HTTP status code this failure maps to.</summary>
    public abstract int StatusCode { get; }
}