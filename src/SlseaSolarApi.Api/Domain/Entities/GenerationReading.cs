namespace SlseaSolarApi.Api.Domain.Entities;

/// <summary>
/// A single timestamped generation record. Append-only time series — history is the product.
/// </summary>
/// <remarks>
/// Design decision D2: readings live in their own table rather than as last-value columns on
/// <see cref="SolarInstallation"/>, because the analytical question "how has generation behaved
/// over time?" is unanswerable from a last-value column.
/// <para>
/// Design decision D3: <see cref="Id"/> is the surrogate key and a unique index is placed on
/// <c>(SolarInstallationId, Timestamp)</c>. Devices retransmit on network failure; keying on the
/// timestamp would collapse legitimate retries, whereas a unique index detects them and yields 409.
/// </para>
/// </remarks>
public class GenerationReading
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    public int SolarInstallationId { get; set; }
    public SolarInstallation SolarInstallation { get; set; } = null!;

    /// <summary>When the reading was taken (UTC). Indexed; drives sort and filter.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Instantaneous power in kW — the operational figure.</summary>
    public decimal PowerKw { get; set; }

    /// <summary>Cumulative energy in kWh — the analytical figure. Monotonic within a day.</summary>
    public decimal EnergyKwh { get; set; }

    /// <summary>Voltage in V — asset health / plausibility check.</summary>
    public decimal Voltage { get; set; }

    /// <summary>
    /// Server receipt time (UTC). Separates <i>when it happened</i> from <i>when we learned about
    /// it</i>, which is essential for debugging late or out-of-order device pushes.
    /// </summary>
    public DateTimeOffset IngestedAt { get; set; }

    /// <summary>
    /// Monotonic per-installation sequence number. Detects gaps and duplicates from device
    /// retries — devices <i>will</i> retransmit.
    /// </summary>
    public long SequenceNo { get; set; }
}