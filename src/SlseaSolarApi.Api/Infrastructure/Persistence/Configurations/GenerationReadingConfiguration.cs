using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SlseaSolarApi.Api.Domain.Entities;

namespace SlseaSolarApi.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="GenerationReading"/> — the append-only time series.</summary>
public class GenerationReadingConfiguration : IEntityTypeConfiguration<GenerationReading>
{
    public void Configure(EntityTypeBuilder<GenerationReading> builder)
    {
        builder.ToTable("GenerationReadings");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.PowerKw).HasPrecision(18, 3);
        builder.Property(r => r.EnergyKwh).HasPrecision(18, 3);
        builder.Property(r => r.Voltage).HasPrecision(18, 2);

        // Decision D3: surrogate Id key + unique index on (installation, timestamp). The index is
        // both the retransmit detector (409) and the covering index for the history read path.
        builder.HasIndex(r => new { r.SolarInstallationId, r.Timestamp }).IsUnique();

        // Decision D3: a monotonic per-installation sequence number lets us detect gaps.
        builder.HasIndex(r => new { r.SolarInstallationId, r.SequenceNo }).IsUnique();

        builder.HasOne(r => r.SolarInstallation)
            .WithMany(i => i.Readings)
            .HasForeignKey(r => r.SolarInstallationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}