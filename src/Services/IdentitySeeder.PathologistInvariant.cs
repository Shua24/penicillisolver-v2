using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Services;

/// <summary>
/// Refuses to let the application run without an active clinical pathologist.
/// </summary>
/// <remarks>
/// Activation by a clinical pathologist is the only way an account ever gains a
/// role, so an empty pool is a permanent lockout: no registration can be
/// approved and no role can be granted. Seeding is therefore required to leave
/// at least one active clinical pathologist behind, and a configuration that
/// does not is treated as a startup failure rather than a warning, because the
/// application would otherwise come up healthy-looking but unusable.
/// </remarks>
public static partial class IdentitySeeder
{
    /// <summary>
    /// Throws when no active clinical pathologist exists after seeding.
    /// </summary>
    /// <param name="userManager">The user manager to query through.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the clinical pathologist role holds no active account.
    /// </exception>
    private static async Task EnsureAnActiveClinicalPathologistExistsAsync(
        UserManager<ApplicationUser> userManager)
    {
        IList<ApplicationUser> pathologists = await userManager.GetUsersInRoleAsync(
            ApplicationRoleNames.ClinicalPathologist);

        bool activePathologistExists = pathologists.Any(user =>
            user.AccountStatus == AccountStatus.Active);

        if (activePathologistExists)
        {
            return;
        }

        // The message is deliberately explicit about the fix: the failure is a
        // configuration gap, and the operator reading this log is the only one
        // who can close it.
        throw new InvalidOperationException(
            "No active clinical pathologist exists, so no account can be activated or " +
            "managed. Configure a seed account first, in development with " +
            "'dotnet user-secrets set \"SeedAccounts:ClinicalPathologist:Email\" \"<email>\"' " +
            "and 'dotnet user-secrets set \"SeedAccounts:ClinicalPathologist:Password\" " +
            "\"<password>\"', or the equivalent SeedAccounts environment variables in " +
            "production, then start the application again.");
    }
}
