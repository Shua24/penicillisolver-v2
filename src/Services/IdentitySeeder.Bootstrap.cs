using System.Security.Cryptography;

using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Services;

/// <summary>
/// Guarantees that the clinical pathologist role is never left without an
/// active account: without one, no pending registration can ever be
/// activated, which is the only way an account gains a role.
/// </summary>
public static partial class IdentitySeeder
{
    private const string BootstrapPathologistEmail = "bootstrap.clinicalpathologist@localhost.invalid";

    private const int BootstrapPasswordLength = 24;

    /// <summary>
    /// Creates or repairs the bootstrap clinical pathologist when no active
    /// account holds the role. The check runs on every startup so that an
    /// invariant is re-guaranteed even after every pathologist was disabled.
    /// </summary>
    private static async Task EnsureBootstrapClinicalPathologistAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        IList<ApplicationUser> pathologists = await userManager.GetUsersInRoleAsync(
            ApplicationRoleNames.ClinicalPathologist);

        bool activePathologistExists = pathologists.Any(user =>
            user.AccountStatus == AccountStatus.Active);

        if (activePathologistExists)
        {
            return;
        }

        await EnsureBootstrapPathologistActiveAsync(userManager, logger);
    }

    /// <summary>
    /// Makes the deterministic bootstrap account active again when it already
    /// exists, and creates it with a generated password when it does not.
    /// Repairing instead of stacking a duplicate keeps one account per email
    /// so that email-based sign-in stays unambiguous.
    /// </summary>
    private static async Task EnsureBootstrapPathologistActiveAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        ApplicationUser? existingBootstrapUser =
            await userManager.FindByEmailAsync(BootstrapPathologistEmail);

        if (existingBootstrapUser is not null)
        {
            bool needsActivation =
                existingBootstrapUser.AccountStatus != AccountStatus.Active;

            if (needsActivation)
            {
                existingBootstrapUser.AccountStatus = AccountStatus.Active;
                IdentityResult activationResult =
                    await userManager.UpdateAsync(existingBootstrapUser);

                if (!activationResult.Succeeded)
                {
                    logger.LogError(
                        "Could not reactivate the bootstrap clinical pathologist: {Errors}",
                        DescribeErrors(activationResult));
                    return;
                }

                logger.LogInformation(
                    "Reactivated the bootstrap clinical pathologist {Email} after no " +
                    "active account held the role.",
                    BootstrapPathologistEmail);
            }

            bool bootstrapUserHoldsRole = await userManager.IsInRoleAsync(
                existingBootstrapUser,
                ApplicationRoleNames.ClinicalPathologist);

            if (bootstrapUserHoldsRole)
            {
                return;
            }

            IdentityResult restoredRoleResult = await userManager.AddToRoleAsync(
                existingBootstrapUser,
                ApplicationRoleNames.ClinicalPathologist);

            if (!restoredRoleResult.Succeeded)
            {
                logger.LogError(
                    "Reactivated the bootstrap clinical pathologist but could not " +
                    "restore the role: {Errors}",
                    DescribeErrors(restoredRoleResult));
            }

            return;
        }

        string generatedPassword = GenerateBootstrapPassword();

        ApplicationUser bootstrapUser = new()
        {
            UserName = BootstrapPathologistEmail,
            Email = BootstrapPathologistEmail,
            EmailConfirmed = true,
            DisplayName = "Bootstrap Clinical Pathologist",
            RequestedRole = ApplicationRoleNames.ClinicalPathologist,
            AccountStatus = AccountStatus.Active,
        };

        IdentityResult creationResult = await userManager.CreateAsync(
            bootstrapUser,
            generatedPassword);

        if (!creationResult.Succeeded)
        {
            logger.LogError(
                "Could not create the bootstrap clinical pathologist: {Errors}",
                DescribeErrors(creationResult));
            return;
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(
            bootstrapUser,
            ApplicationRoleNames.ClinicalPathologist);

        if (!roleResult.Succeeded)
        {
            logger.LogError(
                "Created the bootstrap clinical pathologist but could not assign " +
                "the role: {Errors}",
                DescribeErrors(roleResult));
            return;
        }

        logger.LogInformation(
            "Created the bootstrap clinical pathologist {Email} with a generated " +
            "password because no active account held the role. The password was " +
            "printed to this log exactly once: {Password}. The account stays " +
            "active until it is disabled from the user settings page.",
            BootstrapPathologistEmail,
            generatedPassword);
    }

    /// <summary>
    /// Builds a random 24-character password guaranteed to satisfy the default
    /// Identity password complexity rules (one character from each of
    /// lowercase, uppercase, digit, and symbol groups), then shuffles it.
    /// </summary>
    private static string GenerateBootstrapPassword()
    {
        string[] characterGroups =
        [
            "abcdefghijklmnopqrstuvwxyz",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            "0123456789",
            "!@#$%^&*",
        ];

        int charactersPerGroup = BootstrapPasswordLength / characterGroups.Length;

        List<char> passwordCharacters = new(BootstrapPasswordLength);
        foreach (string characterGroup in characterGroups)
        {
            for (int groupPosition = 0; groupPosition < charactersPerGroup; groupPosition++)
            {
                int randomIndex = RandomNumberGenerator.GetInt32(characterGroup.Length);
                passwordCharacters.Add(characterGroup[randomIndex]);
            }
        }

        ShufflePasswordCharacters(passwordCharacters);

        string password = new(passwordCharacters.ToArray());
        return password;
    }

    /// <summary>Fisher-Yates shuffle so the group letters are not predictable.</summary>
    private static void ShufflePasswordCharacters(List<char> characters)
    {
        for (int position = characters.Count - 1; position > 0; position--)
        {
            int swapIndex = RandomNumberGenerator.GetInt32(position + 1);

            (characters[position], characters[swapIndex]) =
                (characters[swapIndex], characters[position]);
        }
    }
}
