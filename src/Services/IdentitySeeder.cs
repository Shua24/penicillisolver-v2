using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Services;

/// <summary>
/// Creates the three application roles and one seed account per role on
/// startup, and guarantees that at least one active clinical pathologist
/// exists. Seeding is tolerant by design: a missing email or password is
/// logged as an error and that account is skipped, because an incomplete
/// development configuration must not stop the application from starting.
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

        // TODO(prod-secrets): how the production seed credentials are supplied is
        // deliberately undecided. Do not invent a mechanism here; agree on one
        // (user secrets, environment variables, or a one-off provisioning step)
        // with the owner before production deployment.
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

        await EnsureBootstrapClinicalPathologistAsync(userManager, logger);

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

    private static async Task SeedAccountForRoleAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        string roleName)
    {
        string emailKey = $"{SeedAccountsConfigurationSection}:{roleName}:Email";
        string passwordKey = $"{SeedAccountsConfigurationSection}:{roleName}:Password";

        string? email = configuration[emailKey];
        string? password = configuration[passwordKey];

        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogError(
                "Skipping the {RoleName} seed account because the configuration key " +
                "'{EmailKey}' is missing or blank. Set it with 'dotnet user-secrets set \"{EmailKey}\" \"...\"'.",
                roleName,
                emailKey,
                emailKey);
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogError(
                "Skipping the {RoleName} seed account because the configuration key " +
                "'{PasswordKey}' is missing or blank. Set it with 'dotnet user-secrets set \"{PasswordKey}\" \"...\"'.",
                roleName,
                passwordKey,
                passwordKey);
            return;
        }

        ApplicationUser? existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            await EnsureUserHasRoleAsync(userManager, logger, existingUser, roleName);
            return;
        }

        ApplicationUser seedUser = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = $"{roleName} seed account",
            RequestedRole = roleName,
            AccountStatus = AccountStatus.Active,
        };

        IdentityResult creationResult = await userManager.CreateAsync(seedUser, password);

        if (!creationResult.Succeeded)
        {
            logger.LogError(
                "Could not create the {RoleName} seed account for {Email}: {Errors}",
                roleName,
                email,
                DescribeErrors(creationResult));
            return;
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(seedUser, roleName);

        if (roleResult.Succeeded)
        {
            logger.LogInformation(
                "Created the {RoleName} seed account for {Email}.",
                roleName,
                email);
        }
        else
        {
            logger.LogError(
                "Created the {RoleName} seed account for {Email} but could not assign the role: {Errors}",
                roleName,
                email,
                DescribeErrors(roleResult));
        }
    }

    private static async Task EnsureUserHasRoleAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        ApplicationUser existingUser,
        string roleName)
    {
        bool alreadyInRole = await userManager.IsInRoleAsync(existingUser, roleName);

        if (alreadyInRole)
        {
            logger.LogInformation(
                "The {RoleName} seed account {Email} already exists.",
                roleName,
                existingUser.Email);
            return;
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(existingUser, roleName);

        if (roleResult.Succeeded)
        {
            logger.LogInformation(
                "Assigned the {RoleName} role to the existing account {Email}.",
                roleName,
                existingUser.Email);
        }
        else
        {
            logger.LogError(
                "Could not assign the {RoleName} role to {Email}: {Errors}",
                roleName,
                existingUser.Email,
                DescribeErrors(roleResult));
        }
    }

    private static string DescribeErrors(IdentityResult identityResult)
    {
        ArgumentNullException.ThrowIfNull(identityResult);

        IEnumerable<string> descriptions =
            identityResult.Errors.Select(error => $"{error.Code}: {error.Description}");

        return string.Join("; ", descriptions);
    }
}
