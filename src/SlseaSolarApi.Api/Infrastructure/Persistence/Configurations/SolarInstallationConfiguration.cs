using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SlseaSolarApi.Api.Domain.Entities;

namespace SlseaSolarApi.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="SolarInstallation"/>.</summary>
public class SolarInstallationConfiguration : IEntityTypeConfiguration<SolarInstallation>
{
    public void Configure(EntityTypeBuilder<SolarInstallation> builder)
    {
        builder.ToTable("SolarInstallations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).IsRequired().HasMaxLength(150);
        builder.Property(i => i.Reference).IsRequired().HasMaxLength(30);
        builder.Property(i => i.MeterId).IsRequired().HasMaxLength(50);
        builder.Property(i => i.InverterId).HasMaxLength(50);
        builder.Property(i => i.DeviceSecretHash).IsRequired().HasMaxLength(200);
        builder.Property(i => i.CapacityKw).HasPrecision(18, 2);

        builder.HasIndex(i => i.Reference).IsUnique();

        // Decision D1: the meter id is the device's authentication identity, so it must be unique.
        builder.HasIndex(i => i.MeterId).IsUnique();

        builder.HasOne(i => i.GridSubstation)
            .WithMany(s => s.Installations)
            .HasForeignKey(i => i.GridSubstationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}