using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Services;

/// <summary>
/// Creates the three application roles and one seed account per role on
/// startup, then refuses to start when seeding left no active clinical
/// pathologist behind. A missing email or password is logged as an error and
/// that account is skipped; the invariant check decides whether to continue.
/// </summary>
public static partial class IdentitySeeder
{
    private const string SeedAccountsConfigurationSection = "SeedAccounts";

    /// <summary>
    /// Creates any missing roles and seed accounts.
    /// </summary>
    /// <param name="serviceProvider">The root service provider of the running host.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">A logger used to report what happened.</param>
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        RoleManager<IdentityRole> roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationDbContext database =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Seed credentials come from configuration, never from source: user
        // secrets in development, SeedAccounts__* environment variables in
        // production. Nothing is generated here, so a host given no credentials
        // seeds nothing and fails the invariant check.
        string[] roleNames =
        [
            ApplicationRoleNames.ClinicalPathologist,
            ApplicationRoleNames.OtherDoctor,
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
        ];

        foreach (string roleName in roleNames)
        {
            await EnsureRoleExistsAsync(roleManager, logger, roleName);
        }

        foreach (string roleName in roleNames)
        {
            await SeedAccountForRoleAsync(userManager, configuration, logger, roleName);
        }

        await EnsureAnActiveClinicalPathologistExistsAsync(userManager);

        await EnsureTeamPermissionRowExistsAsync(database, logger);
    }

    /// <summary>
    /// Creates the single infectious disease control team permission row when it
    /// is absent.
    /// </summary>
    /// <remarks>
    /// The team's update and delete permissions are stored on this one row and
    /// default to denied. Without the row the authorization handler has nothing
    /// to read and the team can never be granted access through the settings
    /// page, so a missing row would be a silent permanent denial.
    /// </remarks>
    private static async Task EnsureTeamPermissionRowExistsAsync(
        ApplicationDbContext database,
        ILogger logger)
    {
        Domain.Entities.TeamPermission? existingRow = await database.TeamPermissions
            .FirstOrDefaultAsync(row =>
                row.TeamName == ApplicationRoleNames.InfectiousDiseaseControlTeam);

        if (existingRow is not null)
        {
            return;
        }

        Domain.Entities.TeamPermission teamPermission = new()
        {
            TeamName = ApplicationRoleNames.InfectiousDiseaseControlTeam,
            CanUpdateSpreadsheet = false,
            CanDeleteSpreadsheet = false,
            LastModifiedByUserId = string.Empty,
            LastModifiedAtUtc = DateTimeOffset.UtcNow,
        };

        database.TeamPermissions.Add(teamPermission);
        int writtenRowCount = await database.SaveChangesAsync();

        logger.LogInformation(
            "Created the {TeamName} team permission row with update and delete denied " +
            "({WrittenRowCount} row written).",
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
            writtenRowCount);
    }

    /// <summary>
    /// Creates the given role when it does not already exist.
    /// </summary>
    /// <param name="roleManager">The role manager to query and write through.</param>
    /// <param name="logger">A logger used to report what happened.</param>
    /// <param name="roleName">The role to ensure exists.</param>
    private static async Task EnsureRoleExistsAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        string roleName)
    {
        bool roleExists = await roleManager.RoleExistsAsync(roleName);

        if (roleExists)
        {
            return;
        }

        IdentityRole role = new(roleName);
        IdentityResult creationResult = await roleManager.CreateAsync(role);

        if (creationResult.Succeeded)
        {
            logger.LogInformation("Created the {RoleName} role.", roleName);
        }
        else
        {
            logger.LogError(
                "Could not create the {RoleName} role: {Errors}",
                roleName,
                DescribeErrors(creationResult));
        }
    }

    /// <summary>
    /// Joins an identity result's errors into a single readable string.
    /// </summary>
    /// <param name="identityResult">The failed result whose errors are described.</param>
    /// <returns>A semicolon-separated list of error codes and descriptions.</returns>
    private static string DescribeErrors(IdentityResult identityResult)
    {
        ArgumentNullException.ThrowIfNull(identityResult);

        IEnumerable<string> descriptions =
            identityResult.Errors.Select(error => $"{error.Code}: {error.Description}");

        return string.Join("; ", descriptions);
    }
}
