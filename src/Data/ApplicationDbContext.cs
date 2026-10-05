using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Data;

/// <summary>
/// The application database context. It carries the standard identity schema
/// for <see cref="ApplicationUser"/> plus the two application tables: the
/// spreadsheet upload log and the per-team permissions.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>Creates the context with the supplied provider options.</summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>Every accepted spreadsheet upload.</summary>
    public DbSet<SpreadsheetUpload> SpreadsheetUploads => Set<SpreadsheetUpload>();

    /// <summary>Every per-team permissions row.</summary>
    public DbSet<TeamPermission> TeamPermissions => Set<TeamPermission>();

    /// <summary>Every manual antibiotic abbreviation mapping.</summary>
    public DbSet<AntibioticAbbreviation> AntibioticAbbreviations => Set<AntibioticAbbreviation>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
