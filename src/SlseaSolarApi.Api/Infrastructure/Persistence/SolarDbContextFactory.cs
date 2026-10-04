using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SlseaSolarApi.Api.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> when generating migrations. It reads the
/// <c>SolarDb</c> connection string from user-secrets / environment / appsettings, so the
/// migration tooling never hard-codes credentials.
/// </summary>
/// <remarks>
/// The provider can be overridden for a single command with the standard configuration key, e.g.
/// <c>Database__Provider=Postgres dotnet ef migrations add InitialCreate</c>. The factory honours
/// that so a Postgres migration set can be generated without editing any file.
/// </remarks>
public class SolarDbContextFactory : IDesignTimeDbContextFactory<SolarDbContext>
{
    public SolarDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<SolarDbContextFactory>(optional: true)
            // Environment variables last so they win — that is how the provider is overridden.
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<SolarDbContext>();
        DatabaseProviderSelector.Configure(options, configuration);

        return new SolarDbContext(options.Options);
    }
}