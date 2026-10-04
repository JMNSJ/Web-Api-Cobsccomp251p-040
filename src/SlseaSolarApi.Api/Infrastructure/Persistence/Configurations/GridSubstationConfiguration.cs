using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SlseaSolarApi.Api.Domain.Entities;

namespace SlseaSolarApi.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="GridSubstation"/>.</summary>
public class GridSubstationConfiguration : IEntityTypeConfiguration<GridSubstation>
{
    public void Configure(EntityTypeBuilder<GridSubstation> builder)
    {
        builder.ToTable("GridSubstations");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(150);
        builder.Property(s => s.Code).IsRequired().HasMaxLength(20);
        builder.Property(s => s.CapacityKw).HasPrecision(18, 2);

        builder.HasIndex(s => s.Code).IsUnique();

        builder.HasOne(s => s.District)
            .WithMany(d => d.GridSubstations)
            .HasForeignKey(s => s.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}