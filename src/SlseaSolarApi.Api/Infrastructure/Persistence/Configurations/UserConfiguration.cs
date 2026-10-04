using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SlseaSolarApi.Api.Domain.Entities;

namespace SlseaSolarApi.Api.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="User"/> — the SLSEA read-client actor.</summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);

        // Stored as the readable name; the JWT role claim is produced from it.
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(u => u.Username).IsUnique();

        // A user belongs to exactly one scope. Both FKs are optional at the schema level because
        // only one of them is populated, depending on the role.
        builder.HasOne(u => u.Province)
            .WithMany()
            .HasForeignKey(u => u.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.District)
            .WithMany()
            .HasForeignKey(u => u.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}