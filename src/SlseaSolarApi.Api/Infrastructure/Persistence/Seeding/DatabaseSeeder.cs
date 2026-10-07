using Microsoft.EntityFrameworkCore;
using SlseaSolarApi.Api.Domain.Entities;
using SlseaSolarApi.Api.Domain.Enums;

namespace SlseaSolarApi.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// Generates and inserts the full demonstration dataset: 9 provinces, 25 districts, 20+
/// substations, 200+ installations and a week of 15-minute readings for each.
/// </summary>
/// <remarks>
/// Design decision D12: generation is deterministic (fixed random seed), so repeated runs produce
/// an identical dataset and tests are reproducible.
/// <para>
/// Design decision D13: readings are inserted in batches via <c>AddRange</c> + <c>SaveChanges</c>,
/// never row-by-row. ~134k rows inserted one <c>SaveChanges</c> at a time would take minutes; in
/// batches of a few thousand it takes seconds.
/// </para>
/// </remarks>
public class DatabaseSeeder
{
    /// <summary>Fixed seed: same dataset every run.</summary>
    private const int RandomSeed = 20251004;

    /// <summary>One reading every 15 minutes.</summary>
    private static readonly TimeSpan ReadingInterval = TimeSpan.FromMinutes(15);

    /// <summary>Seven days of history per installation = 672 readings.</summary>
    private const int ReadingDays = 7;

    /// <summary>How many readings to stage before flushing to the database.</summary>
    private const int ReadingBatchSize = 5000;

    /// <summary>Number of rooftop installations to create.</summary>
    private const int InstallationCount = 210;

    private readonly SolarDbContext _db;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// The demo device secret, read from configuration (never hard-coded in source). On Render it
    /// is generated; locally the Development settings provide the documented demo value.
    /// </summary>
    private string DeviceSecret =>
        _configuration["Solar:DeviceSecret"]
        ?? throw new InvalidOperationException(
            "No device secret is configured. Set Solar__DeviceSecret in the host environment " +
            "(Render generates one) or via user secrets / appsettings.Development.json locally.");

    public DatabaseSeeder(SolarDbContext db, IConfiguration configuration, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Seeds the database if it is empty. Returns the number of readings inserted (0 when the
    /// database was already populated).
    /// </summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.Provinces.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Database already contains data; skipping seed.");
            return 0;
        }

        var random = new Random(RandomSeed);

        _logger.LogInformation("Seeding provinces and districts...");
        var provinces = SeedGeography();
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeding grid substations...");
        var substations = SeedSubstations(provinces, random);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeding installations...");
        var installations = SeedInstallations(substations, random);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeding users...");
        SeedUsers(provinces);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeding readings for {Count} installations ({Days} days at 15-minute intervals)...",
            installations.Count, ReadingDays);
        var readingCount = await SeedReadingsAsync(installations, random, cancellationToken);

        _logger.LogInformation(
            "Seed complete: {Provinces} provinces, {Districts} districts, {Substations} substations, " +
            "{Installations} installations, {Readings} readings.",
            provinces.Count,
            provinces.Sum(p => p.Districts.Count),
            substations.Count,
            installations.Count,
            readingCount);

        return readingCount;
    }

    // -----------------------------------------------------------------------
    // Geography
    // -----------------------------------------------------------------------
    private List<Province> SeedGeography()
    {
        var provinces = new List<Province>();

        foreach (var provinceSeed in SriLankaGeography.Provinces)
        {
            var province = new Province
            {
                Name = provinceSeed.Name,
                Code = provinceSeed.Code
            };

            foreach (var districtSeed in provinceSeed.Districts)
            {
                province.Districts.Add(new District
                {
                    Name = districtSeed.Name,
                    Code = districtSeed.Code
                });
            }

            provinces.Add(province);
        }

        _db.Provinces.AddRange(provinces);
        return provinces;
    }

    // -----------------------------------------------------------------------
    // Substations — at least one per district, a second in the larger districts.
    // -----------------------------------------------------------------------
    private List<GridSubstation> SeedSubstations(List<Province> provinces, Random random)
    {
        var substations = new List<GridSubstation>();
        var districts = provinces.SelectMany(p => p.Districts).ToList();

        foreach (var district in districts)
        {
            var count = district.Name is "Colombo" or "Gampaha" or "Kandy" or "Galle" ? 2 : 1;

            for (var i = 1; i <= count; i++)
            {
                substations.Add(new GridSubstation
                {
                    Name = $"{district.Name} Grid Substation {i}",
                    Code = $"GSS-{district.Code}-{i:00}",
                    CapacityKw = random.Next(20, 60) * 100m, // 2,000 - 5,900 kW
                    District = district
                });
            }
        }

        _db.GridSubstations.AddRange(substations);
        return substations;
    }

    // -----------------------------------------------------------------------
    // Installations — 210 rooftop sites distributed round-robin across substations.
    // -----------------------------------------------------------------------
    private List<SolarInstallation> SeedInstallations(List<GridSubstation> substations, Random random)
    {
        var installations = new List<SolarInstallation>();

        for (var i = 1; i <= InstallationCount; i++)
        {
            var substation = substations[(i - 1) % substations.Count];

            // Rooftop systems here are small: 3 - 12 kW nameplate.
            var capacity = Math.Round((decimal)(random.NextDouble() * 9 + 3), 1);

            installations.Add(new SolarInstallation
            {
                Name = $"{substation.District.Name} Rooftop {i:000}",
                Reference = $"INST-{i:00000}",
                MeterId = $"MTR-{i:000000}",
                InverterId = i % 3 == 0 ? null : $"INV-{i:000000}",
                // Device secret for the demo write-client. The plaintext comes from the host
                // environment (Solar__DeviceSecret, generated by Render); the database holds only
                // its salted hash. Locally it defaults to the documented demo value.
                DeviceSecretHash = PasswordHasher.Hash(DeviceSecret),
                CapacityKw = capacity,
                IsActive = i % 25 != 0, // ~4% decommissioned, so filters have something to exclude
                CommissionedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(90, 1200)),
                GridSubstation = substation
            });
        }

        _db.SolarInstallations.AddRange(installations);
        return installations;
    }

    // -----------------------------------------------------------------------
    // Users — one per role, plus a provincial and a district user, so the
    // jurisdiction-scoping rules are demonstrable.
    // -----------------------------------------------------------------------
    private void SeedUsers(List<Province> provinces)
    {
        var western = provinces.First(p => p.Code == "WP");
        var colombo = western.Districts.First(d => d.Code == "CO");

        _db.Users.AddRange(
            new User
            {
                Username = "national",
                FullName = "National Control Room",
                PasswordHash = PasswordHasher.Hash("National@123"),
                Role = UserRole.National,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new User
            {
                Username = "western",
                FullName = "Western Province Office",
                PasswordHash = PasswordHasher.Hash("Western@123"),
                Role = UserRole.Provincial,
                Province = western,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new User
            {
                Username = "colombo",
                FullName = "Colombo District Office",
                PasswordHash = PasswordHasher.Hash("Colombo@123"),
                Role = UserRole.District,
                District = colombo,
                CreatedAt = DateTimeOffset.UtcNow
            });
    }

    // -----------------------------------------------------------------------
    // Readings — 7 days x 24h x 4 = 672 per installation, on a diurnal curve.
    // -----------------------------------------------------------------------
    private async Task<int> SeedReadingsAsync(
        List<SolarInstallation> installations,
        Random random,
        CancellationToken cancellationToken)
    {
        // Align the window to a 15-minute boundary so every installation shares the same grid.
        var end = DateTimeOffset.UtcNow;
        end = new DateTimeOffset(
            end.Year, end.Month, end.Day, end.Hour, end.Minute / 15 * 15, 0, end.Offset);

        var start = end.AddDays(-ReadingDays);

        var staged = 0;
        var total = 0;

        foreach (var installation in installations)
        {
            var sequence = 0L;
            var cumulativeEnergy = 0m;
            var currentDay = start.Date;

            for (var t = start; t < end; t = t.Add(ReadingInterval))
            {
                // Energy resets at midnight (a daily cumulative figure).
                if (t.Date != currentDay)
                {
                    currentDay = t.Date;
                    cumulativeEnergy = 0m;
                }

                var hour = t.Hour + t.Minute / 60.0;
                var power = DiurnalPower(hour, installation.CapacityKw, random);

                // Energy accumulates: kWh += kW * (15 min = 0.25 h).
                cumulativeEnergy += power * 0.25m;

                // Voltage hovers around 230 V, sagging slightly under high load.
                var voltage = 230m
                    + (decimal)(random.NextDouble() * 6 - 3)
                    - (power / installation.CapacityKw) * 4m;

                _db.GenerationReadings.Add(new GenerationReading
                {
                    SolarInstallationId = installation.Id,
                    Timestamp = t,
                    PowerKw = Math.Round(power, 3),
                    EnergyKwh = Math.Round(cumulativeEnergy, 3),
                    Voltage = Math.Round(voltage, 2),
                    IngestedAt = t.AddSeconds(random.Next(1, 30)),
                    SequenceNo = sequence++
                });

                staged++;
                total++;

                if (staged >= ReadingBatchSize)
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    staged = 0;
                }
            }
        }

        if (staged > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }

        return total;
    }

    /// <summary>
    /// The diurnal shape: zero before ~06:00, rising to a midday peak, back to zero by ~18:30.
    /// Modelled as a half-sine over the daylight window, scaled by nameplate capacity, plus noise.
    /// </summary>
    private static decimal DiurnalPower(double hour, decimal capacityKw, Random random)
    {
        const double sunrise = 6.0;
        const double sunset = 18.5;

        if (hour < sunrise || hour > sunset)
        {
            return 0m;
        }

        // Half-sine over the daylight window, normalised to 0..1.
        var fraction = Math.Sin(Math.PI * (hour - sunrise) / (sunset - sunrise));

        // Peak output is ~82% of nameplate — real rooftop systems rarely hit nameplate.
        var power = (decimal)fraction * capacityKw * 0.82m;

        // Small measurement noise.
        power += (decimal)(random.NextDouble() * 0.4 - 0.2);

        return power < 0m ? 0m : power;
    }
}