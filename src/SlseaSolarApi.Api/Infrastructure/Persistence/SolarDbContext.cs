using Microsoft.EntityFrameworkCore;
using SlseaSolarApi.Api.Domain.Entities;

namespace SlseaSolarApi.Api.Infrastructure.Persistence;

/// <summary>
/// The EF Core context. Entity shape is configured in the per-entity
/// <c>IEntityTypeConfiguration</c> classes, keeping this class free of configuration noise.
/// </summary>
public class SolarDbContext : DbContext
{
    public SolarDbContext(DbContextOptions<SolarDbContext> options) : base(options)
    {
    }

    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<GridSubstation> GridSubstations => Set<GridSubstation>();
    public DbSet<SolarInstallation> SolarInstallations => Set<SolarInstallation>();
    public DbSet<GenerationReading> GenerationReadings => Set<GenerationReading>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pull in every IEntityTypeConfiguration<T> declared in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SolarDbContext).Assembly);
    }
}