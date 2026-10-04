using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SlseaSolarApi.Api.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> when generating migrations. It reads the
/// <c>SolarDb</c> connection string from user-secrets / environment / appsettings, so the
/// migration tooling never hard-codes credentials.
/// </summary>
public class SolarDbContextFactory : IDesignTimeDbContextFactory<SolarDbContext>
{
    public SolarDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<SolarDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SolarDb")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=SlseaSolarDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<SolarDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SolarDbContext(options);
    }
}