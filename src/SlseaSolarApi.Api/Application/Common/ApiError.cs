namespace SlseaSolarApi.Api.Application.Common;

/// <summary>
/// The single error-body schema used by the whole API (design decision D10). Every 4xx — and,
/// where possible, 5xx — returns exactly this shape, so clients switch on <see cref="Code"/> rather
/// than parsing prose.
/// </summary>
public class ApiError
{
    /// <summary>Machine-readable, <c>SCREAMING_SNAKE_CASE</c>, stable across releases.</summary>
    public string Code { get; init; } = "UNKNOWN_ERROR";

    /// <summary>Human-readable and safe to show a user. Never leaks internals.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Optional supporting context for developers.</summary>
    public string? Detail { get; init; }

    /// <summary>W3C traceparent id, correlating this response to the server logs.</summary>
    public string? TraceId { get; init; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Per-field validation messages, present only for validation failures.</summary>
    public IDictionary<string, string[]>? Errors { get; init; }
}