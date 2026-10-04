namespace SlseaSolarApi.Api.Domain.Exceptions;

/// <summary>
/// Raised when a device retransmits a reading we already hold. Maps to <b>409</b>.
/// </summary>
/// <remarks>
/// Design decision D3: the unique index on <c>(SolarInstallationId, Timestamp)</c> is what detects
/// the retransmit. POST is not idempotent — repeating a reading push creates a duplicate — so we
/// answer a genuine duplicate with 409 Conflict rather than silently accepting it.
/// </remarks>
public sealed class DuplicateReadingException : ApiException
{
    public DuplicateReadingException(object installationId, DateTimeOffset timestamp, string? detail = null)
        : base(
            "DUPLICATE_READING",
            "A reading for this installation at this timestamp already exists.",
            detail ?? $"Installation {installationId} already has a reading at {timestamp:O}.")
    {
    }

    public override int StatusCode => StatusCodes.Status409Conflict;
}