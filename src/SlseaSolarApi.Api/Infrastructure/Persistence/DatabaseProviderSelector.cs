using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SlseaSolarApi.Api.Infrastructure.Persistence;

/// <summary>
/// Chooses the EF Core database provider from configuration.
/// </summary>
/// <remarks>
/// Design decision D16: the provider is selectable via <c>Database:Provider</c> so the same code
/// runs against the deployment database and a local one.
/// <list type="bullet">
///   <item><description><c>Postgres</c> — the public deployment target. Managed hosts offer a free
///     PostgreSQL instance, and (unlike a local file) it survives the ephemeral filesystem and
///     idle spin-downs of a free container host.</description></item>
///   <item><description><c>SqlServer</c> — the module's stated target stack, for a host that
///     provides SQL Server.</description></item>
///   <item><description><c>Sqlite</c> — local development, where neither of the above is installed.</description></item>
/// </list>
/// The domain model, configurations and migrations are provider agnostic; only the connection differs.
/// </remarks>
public static class DatabaseProviderSelector
{
    public const string SqlServer = "SqlServer";
    public const string Sqlite = "Sqlite";
    public const string Postgres = "Postgres";

    // Migrations live in the API assembly. Only one provider's migration set can occupy that
    // assembly at a time, because EF's model snapshot is a single class. PostgreSQL is the
    // public-deployment target, so its migrations are the ones kept there. SQLite (local dev)
    // creates its schema from the same model without a migration set — see EnsureSchemaAsync.
    private const string MigrationsAssembly = "SlseaSolarApi.Api";

    /// <summary>Reads the configured provider name, defaulting to PostgreSQL (the deploy target).</summary>
    public static string Resolve(IConfiguration configuration) =>
        configuration["Database:Provider"] ?? Postgres;

    /// <summary>Applies the configured provider to the context options builder.</summary>
    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var provider = Resolve(configuration);
        var connectionString = configuration.GetConnectionString("SolarDb");

        if (string.Equals(provider, Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlite(
                NormaliseSqliteConnection(connectionString),
                sqlite => sqlite.MigrationsAssembly(MigrationsAssembly));
        }
        else if (string.Equals(provider, SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlServer(
                connectionString
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=SlseaSolarDb;Trusted_Connection=True;MultipleActiveResultSets=true",
                sql => sql.EnableRetryOnFailure());
        }
        else
        {
            // Managed PostgreSQL hands out a URI-style connection string; Npgsql wants key/value.
            options.UseNpgsql(
                NormalisePostgresConnection(connectionString)
                    ?? throw new InvalidOperationException(
                        "No 'SolarDb' connection string was supplied for the Postgres provider. " +
                        "Set ConnectionStrings__SolarDb in the host environment."),
                npgsql =>
                {
                    npgsql.EnableRetryOnFailure();
                    npgsql.MigrationsAssembly(MigrationsAssembly);
                });
        }
    }

    /// <summary>
    /// Accepts either a key/value connection string or the <c>postgres://user:pass@host:port/db</c>
    /// URI that managed hosts (Render, Heroku, Neon) hand out, and converts the latter to the
    /// key/value form Npgsql expects.
    /// </summary>
    private static string? NormalisePostgresConnection(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            if (HasDataSourceKeyword(connectionString))
            {
                throw new InvalidOperationException(
                    "The Postgres provider received a connection string containing 'Data Source', " +
                    "which is typically a SQLite or SQL Server connection string. Configure " +
                    "ConnectionStrings__SolarDb with the PostgreSQL URL or a PostgreSQL " +
                    "key/value connection string (Host, Port, Database, Username, Password).");
            }

            return connectionString;
        }

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            // Managed Postgres requires TLS; this is the permissive mode that works with their certs.
            SslMode = Npgsql.SslMode.Require
        };

        return builder.ConnectionString;
    }

    private static bool HasDataSourceKeyword(string connectionString) =>
        connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part =>
            {
                var separator = part.IndexOf('=');
                return separator >= 0 &&
                    string.Equals(
                        part[..separator].Trim(),
                        "Data Source",
                        StringComparison.OrdinalIgnoreCase);
            });

    /// <summary>
    /// Turns a relative SQLite file path into an absolute one rooted at the application's content
    /// root. Without this the database file lands wherever the process happened to be launched from,
    /// so <c>dotnet run</c> and a published binary would use different databases.
    /// </summary>
    private static string NormaliseSqliteConnection(string? connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString ?? "Data Source=slsea-solar.db");

        if (!string.IsNullOrWhiteSpace(builder.DataSource) && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);
        }

        return builder.ToString();
    }
}