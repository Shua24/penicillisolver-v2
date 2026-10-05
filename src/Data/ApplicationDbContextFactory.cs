using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace penicillisolver_v2.Data;

/// <summary>
/// Lets the Entity Framework tools build an <see cref="ApplicationDbContext"/>
/// at design time without starting the application host. The application's
/// <c>Program</c> runs schema migration and identity seeding at startup, which
/// the tools cannot execute just to read the model, so this factory supplies a
/// plain SQLite context instead.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <summary>The database file used only while the tools build migrations.</summary>
    private const string DesignTimeConnectionString = "Data Source=penicillisolver-design.db";

    /// <inheritdoc />
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ApplicationDbContext> optionsBuilder = new();

        optionsBuilder.UseSqlite(DesignTimeConnectionString);

        DbContextOptions<ApplicationDbContext> options = optionsBuilder.Options;
        ApplicationDbContext database = new(options);

        return database;
    }
}
