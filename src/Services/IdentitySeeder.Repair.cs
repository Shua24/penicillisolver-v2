using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Services;

/// <summary>
/// Repairs data that an older version of the application wrote in a shape the
/// current code no longer produces. Kept in its own partial so the one-time
/// fixes stay apart from the steady-state seeding in <see cref="IdentitySeeder"/>
/// and from the bootstrap guarantee in <see cref="IdentitySeeder"/>.
/// </summary>
public static partial class IdentitySeeder
{
    /// <summary>
    /// Rewrites a seed account's legacy display name, which an older seeder set
    /// to the raw role token (for example "ClinicalPathologist seed account"),
    /// to the human readable form. The repair only runs when the name still
    /// equals that legacy literal, so a display name a clinical pathologist
    /// changed by hand is left untouched.
    /// </summary>
    /// <param name="userManager">The user manager that persists the account.</param>
    /// <param name="logger">The logger used to report what changed.</param>
    /// <param name="existingUser">The seed account found by its configured email.</param>
    /// <param name="roleName">The application role this seed account belongs to.</param>
    private static async Task RepairSeedAccountDisplayNameAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        ApplicationUser existingUser,
        string roleName)
    {
        string legacyDisplayName = $"{roleName} seed account";

        if (!string.Equals(existingUser.DisplayName, legacyDisplayName, StringComparison.Ordinal))
        {
            return;
        }

        string roleDisplayName = ApplicationRoleDisplayNames.DisplayNameOf(roleName);

        existingUser.DisplayName = $"{roleDisplayName} seed account";

        IdentityResult updateResult = await userManager.UpdateAsync(existingUser);

        if (!updateResult.Succeeded)
        {
            logger.LogError(
                "Could not rename the {RoleName} seed account away from the legacy " +
                "display name: {Errors}",
                roleName,
                DescribeErrors(updateResult));
            return;
        }

        logger.LogInformation(
            "Renamed the {RoleName} seed account to its human readable display name.",
            roleName);
    }
}
