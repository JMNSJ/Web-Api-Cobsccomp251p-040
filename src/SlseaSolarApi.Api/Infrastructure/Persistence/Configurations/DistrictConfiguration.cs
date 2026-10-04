using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SlseaSolarApi.Api.Domain.Entities;

namespace SlseaSolarApi.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="District"/>.</summary>
public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("Districts");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Code).IsRequired().HasMaxLength(10);

        builder.HasIndex(d => d.Code).IsUnique();
        builder.HasIndex(d => new { d.ProvinceId, d.Name }).IsUnique();

        builder.HasOne(d => d.Province)
            .WithMany(p => p.Districts)
            .HasForeignKey(d => d.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}