using Microsoft.EntityFrameworkCore;

namespace penicillisolver_v2.Data;

/// <summary>
/// Chooses the database provider at startup. Development runs on a local file
/// based SQLite database so that no server is required; production runs on
/// PostgreSQL. The choice is driven by the <c>Database:Provider</c> setting and
/// falls back to SQLite while in development when that setting is absent.
/// </summary>
public static class DatabaseProviderSelector
{
    /// <summary>The configuration key holding the provider short name.</summary>
    public const string ProviderConfigurationKey = "Database:Provider";

    /// <summary>The configuration key holding the connection string.</summary>
    public const string ConnectionStringConfigurationKey = "ConnectionStrings:DefaultConnection";

    private const string SqliteProviderName = "Sqlite";

    /// <summary>
    /// Builds the provider configuration action for the application context.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="isDevelopment">Whether the host is running in development.</param>
    /// <returns>An action that configures the supplied options builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured connection string is missing or blank.
    /// </exception>
    public static Action<DbContextOptionsBuilder> Create(
        IConfiguration configuration,
        bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? configuredProviderName = configuration[ProviderConfigurationKey];
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string '{ConnectionStringConfigurationKey}' is missing. " +
                "Set it in appsettings.json, appsettings.Development.json, user secrets, " +
                "or the environment before starting the application.");
        }

        bool useSqlite = ResolveUseSqlite(configuredProviderName, isDevelopment);

        if (useSqlite)
        {
            return options => options.UseSqlite(connectionString);
        }

        return options => options.UseNpgsql(connectionString);
    }

    private static bool ResolveUseSqlite(string? configuredProviderName, bool isDevelopment)
    {
        if (!string.IsNullOrWhiteSpace(configuredProviderName))
        {
            bool isSqliteName = string.Equals(
                configuredProviderName.Trim(),
                SqliteProviderName,
                StringComparison.OrdinalIgnoreCase);

            return isSqliteName;
        }

        // No provider was configured at all: development defaults to the local
        // SQLite file, everything else defaults to PostgreSQL.
        return isDevelopment;
    }
}
