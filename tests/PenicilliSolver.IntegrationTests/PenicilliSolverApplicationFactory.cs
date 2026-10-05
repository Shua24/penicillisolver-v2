using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using penicillisolver_v2.Data;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// Boots the real application in the test process against a private SQLite
/// database file. The production provider registration is removed and replaced
/// so the tests never depend on a running PostgreSQL server, and a connection
/// kept open for the lifetime of the factory keeps the file based schema alive.
/// </summary>
/// <remarks>
/// Not sealed: the antibiotic-mapping integration tests extend this factory to
/// add a test-only endpoint, and they need its configured SQLite database and
/// seeding. Sealing it has no benefit for a test host.
/// </remarks>
public class PenicilliSolverApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databaseFilePath =
        Path.Combine(Path.GetTempPath(), $"penicillisolver-tests-{Guid.NewGuid():N}.db");

    /// <summary>
    /// A private storage directory for this test run.
    /// </summary>
    /// <remarks>
    /// This MUST be redirected away from the application's own
    /// <c>App_Data/spreadsheets</c>. The storage service resolves its directory
    /// against the content root, and the test host's content root is the project
    /// directory, so without this override every integration run writes
    /// <c>current-spreadsheet.csv</c> over whatever the developer had actually
    /// uploaded. That is not a test artifact, it is data loss: the rows in the
    /// database keep describing the old file, and the app then serves the test
    /// seed's placeholder organisms.
    /// </remarks>
    private readonly string storageDirectoryPath =
        Path.Combine(Path.GetTempPath(), $"penicillisolver-tests-storage-{Guid.NewGuid():N}");

    private SqliteConnection? heldConnection;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);

        // Redirect spreadsheet storage before any service reads it.
        builder.UseSetting(
            "SpreadsheetStorage:Directory",
            storageDirectoryPath);

        builder.ConfigureServices(services =>
        {
            // Remove every service descriptor that belongs to the production
            // DbContext registration, then register the same context over a
            // dedicated SQLite connection.
            List<ServiceDescriptor> descriptorsToRemove = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                    || descriptor.ServiceType == typeof(ApplicationDbContext)
                    || (descriptor.ServiceType.IsGenericType
                        && descriptor.ServiceType.GetGenericTypeDefinition()
                            == typeof(DbContextOptions<>)))
                .ToList();

            foreach (ServiceDescriptor descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            heldConnection = new SqliteConnection($"Data Source={databaseFilePath}");
            heldConnection.Open();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(heldConnection));
        });
    }

    /// <summary>Creates the schema for the private database.</summary>
    public async Task InitialiseDatabaseAsync()
    {
        using IServiceScope scope = Services.CreateScope();

        ApplicationDbContext database = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await database.Database.EnsureCreatedAsync();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (heldConnection is not null)
        {
            await heldConnection.DisposeAsync();
            heldConnection = null;
        }

        TryDeleteDatabaseFiles();
        TryDeleteStorageDirectory();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDeleteDatabaseFiles();
        TryDeleteStorageDirectory();
    }

    private void TryDeleteStorageDirectory()
    {
        if (Directory.Exists(storageDirectoryPath))
        {
            Directory.Delete(storageDirectoryPath, recursive: true);
        }
    }

    private void TryDeleteDatabaseFiles()
    {
        DeleteIfPresent(databaseFilePath);
        DeleteIfPresent($"{databaseFilePath}-wal");
        DeleteIfPresent($"{databaseFilePath}-shm");
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
