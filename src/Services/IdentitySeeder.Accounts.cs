using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Services;

/// <summary>
/// The per-role seed account creation and role assignment, kept in their own
/// partial so the steady-state seeding in <see cref="IdentitySeeder"/> reads as
/// the sequence it is.
/// </summary>
public static partial class IdentitySeeder
{
    /// <summary>
    /// Creates the configured seed account for a role, or assigns the role to
    /// an existing account with the same email. Skips the role, logging an
    /// error, when the email or password configuration is missing or blank.
    /// </summary>
    /// <param name="userManager">The user manager to query and write through.</param>
    /// <param name="configuration">The application configuration holding the seed credentials.</param>
    /// <param name="logger">A logger used to report what happened.</param>
    /// <param name="roleName">The role whose seed account is being created.</param>
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
            await RepairSeedAccountDisplayNameAsync(userManager, logger, existingUser, roleName);
            await EnsureUserHasRoleAsync(userManager, logger, existingUser, roleName);
            return;
        }

        string roleDisplayName = ApplicationRoleDisplayNames.DisplayNameOf(roleName);

        ApplicationUser seedUser = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = $"{roleDisplayName} seed account",
            RequestedRole = roleName,
            AccountStatus = AccountStatus.Active,

            // A seed password is written down in deployment notes and configuration,
            // and re-running the seeder does NOT change an existing account's
            // password. Without this flag the bootstrap credential would be a
            // permanent one that whoever read the notes still knows. The account
            // is forced to replace it at first sign-in. The flag is set only when
            // the account is created, never on an existing one, so a restart does
            // not re-arm the gate on someone who has already changed it.
            MustChangePassword = true,
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

    /// <summary>
    /// Assigns the given role to an existing user when they do not already
    /// hold it.
    /// </summary>
    /// <param name="userManager">The user manager to query and write through.</param>
    /// <param name="logger">A logger used to report what happened.</param>
    /// <param name="existingUser">The user to check and assign the role to.</param>
    /// <param name="roleName">The role to ensure the user holds.</param>
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
}
