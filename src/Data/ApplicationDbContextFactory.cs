using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace penicillisolver_v2.Data;

/// <summary>
/// Lets the Entity Framework tools build an <see cref="ApplicationDbContext"/>
/// at design time without starting the application host. The application's
/// <c>Program</c> runs schema migration and identity seeding at startup, which
/// the tools cannot execute just to read the model, so this factory supplies a
/// plain context instead.
/// </summary>
/// <remarks>
/// <para>
/// This factory decides which provider the migration files are generated for,
/// and that is PostgreSQL. Allowing it to differ from the deployment provider is
/// what made PostgreSQL undeployable: the migrations were generated for SQLite,
/// so every column type was <c>TEXT</c> or <c>INTEGER</c>, and Npgsql refused to
/// apply them.
/// </para>
/// <para>
/// SQLite is still supported at runtime — development and the integration test
/// host use it — but it is built from the model with <c>EnsureCreatedAsync</c>
/// rather than migrated. Migrations are provider specific and cannot serve both,
/// so the deployed provider is the one they are generated for. See
/// <c>Program.cs</c> where the provider chooses between the two.
/// </para>
/// </remarks>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <summary>
    /// The connection the tools use only while building migrations. Any valid,
    /// reachable PostgreSQL connection works: the tools read the provider from
    /// it, not the schema.
    /// </summary>
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=penicillisolver;Username=penicillisolver;Password=design-time";

    /// <inheritdoc />
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ApplicationDbContext> optionsBuilder = new();

        optionsBuilder.UseNpgsql(DesignTimeConnectionString);

        DbContextOptions<ApplicationDbContext> options = optionsBuilder.Options;
        ApplicationDbContext database = new(options);

        return database;
    }
}
