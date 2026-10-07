namespace penicillisolver_v2.Services;

using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;

/// <summary>
/// The authorization guard, the last-pathologist guard, and the small
/// formatting helpers shared by the public write operations of
/// <see cref="UserAdministrationService"/>.
/// </summary>
public sealed partial class UserAdministrationService
{
    /// <summary>
    /// Refuses an operation that would leave no active clinical pathologist.
    /// </summary>
    /// <remarks>
    /// The guard runs inside the change being attempted, so the account being
    /// demoted or disabled must not be counted as a survivor. The target
    /// account is therefore excluded from the tally before the comparison is
    /// made: demoting one of two pathologists is permitted, demoting the only
    /// one is refused.
    /// </remarks>
    /// <param name="targetUserId">The account being demoted or disabled.</param>
    /// <returns>Failure when the change would lock everyone out, otherwise success.</returns>
    private async Task<AdministrationResult> GuardLastActivePathologistAsync(string targetUserId)
    {
        bool targetIsActivePathologist = await IsActiveClinicalPathologistAsync(targetUserId);

        if (!targetIsActivePathologist)
        {
            // The account is not an active pathologist, so its removal from
            // that pool cannot change the count.
            //
            // This message and the two below are deliberately NOT localised:
            // each one names a role, and role names are a UI display concern.
            // See the localisation boundary note on UserAdministrationService.
            AdministrationResult permittedResult =
                AdministrationResult.Success("The change does not affect the last pathologist.");

            return permittedResult;
        }

        List<string> activePathologistIds = await GetActiveClinicalPathologistIdsAsync();

        int survivingPathologistCount = activePathologistIds.Count(
            pathologistId => !string.Equals(pathologistId, targetUserId, StringComparison.Ordinal));

        if (survivingPathologistCount == 0)
        {
            // Deliberately English: this sentence names a role. See the
            // localisation boundary note on UserAdministrationService.
            return AdministrationResult.Failure(
                "This is the last active clinical pathologist. Disabling or changing this " +
                "account's role would leave nobody able to manage users or the spreadsheet.");
        }

        // Deliberately English: names a role.
        AdministrationResult allowedResult =
            AdministrationResult.Success("Another active pathologist remains.");

        return allowedResult;
    }

    /// <summary>
    /// Reports whether an account is currently an active clinical pathologist.
    /// </summary>
    private async Task<bool> IsActiveClinicalPathologistAsync(string userId)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return false;
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            return false;
        }

        bool holdsRole = await userManager.IsInRoleAsync(
            user,
            ApplicationRoleNames.ClinicalPathologist);

        return holdsRole;
    }

    /// <summary>
    /// Returns the identifiers of every active account holding the clinical
    /// pathologist role.
    /// </summary>
    private async Task<List<string>> GetActiveClinicalPathologistIdsAsync()
    {
        IList<ApplicationUser> pathologists = await userManager.GetUsersInRoleAsync(
            ApplicationRoleNames.ClinicalPathologist);

        IEnumerable<ApplicationUser> activePathologists =
            pathologists.Where(user => user.AccountStatus == AccountStatus.Active);

        List<string> activePathologistIds = activePathologists
            .Select(user => user.Id)
            .ToList();

        return activePathologistIds;
    }

    /// <summary>
    /// Confirms the acting account is an active clinical pathologist.
    /// </summary>
    private async Task<AdministrationResult> AuthorizeAdministratorAsync(string actingUserId)
    {
        ApplicationUser? actingUser = await userManager.FindByIdAsync(actingUserId);

        if (actingUser is null)
        {
            return AdministrationResult.Failure(Localizer["Service_YourAccountNotFound"]);
        }

        if (actingUser.AccountStatus != AccountStatus.Active)
        {
            return AdministrationResult.Failure(Localizer["Service_YourAccountNotActive"]);
        }

        bool holdsPathologistRole = await userManager.IsInRoleAsync(
            actingUser,
            ApplicationRoleNames.ClinicalPathologist);

        if (!holdsPathologistRole)
        {
            // Deliberately English: names a role.
            return AdministrationResult.Failure(
                "Only a clinical pathologist may change roles, account states or team permissions.");
        }

        AdministrationResult authorizedResult =
            AdministrationResult.Success(Localizer["Service_Authorized"]);

        return authorizedResult;
    }

    /// <summary>
    /// Removes the single application role the account currently holds.
    /// </summary>
    private async Task<AdministrationResult> RemoveAllApplicationRolesAsync(
        ApplicationUser targetUser,
        string currentRoleName)
    {
        if (string.IsNullOrWhiteSpace(currentRoleName))
        {
            AdministrationResult nothingToRemove =
                AdministrationResult.Success(Localizer["Service_AccountHeldNoRole"]);

            return nothingToRemove;
        }

        IdentityResult removeResult = await userManager.RemoveFromRoleAsync(
            targetUser,
            currentRoleName);

        if (!removeResult.Succeeded)
        {
            return AdministrationResult.Failure(DescribeIdentityFailures(removeResult));
        }

        AdministrationResult removedResult = AdministrationResult.Success(Localizer["Service_RoleRemoved"]);

        return removedResult;
    }

    /// <summary>
    /// Resolves the single application role an account holds, or the empty
    /// string when it holds none.
    /// </summary>
    private async Task<string> ResolveSingleRoleAsync(ApplicationUser account)
    {
        IList<string> roles = await userManager.GetRolesAsync(account);

        IEnumerable<string> applicationRoles = roles.Where(IsApplicationRole);

        string roleName = applicationRoles.FirstOrDefault() ?? string.Empty;

        return roleName;
    }

    /// <summary>
    /// Reports whether a role name is one of the three application roles.
    /// </summary>
    private static bool IsApplicationRole(string roleName)
    {
        bool isRecognised =
            string.Equals(roleName, ApplicationRoleNames.ClinicalPathologist, StringComparison.Ordinal)
            || string.Equals(roleName, ApplicationRoleNames.OtherDoctor, StringComparison.Ordinal)
            || string.Equals(roleName, ApplicationRoleNames.InfectiousDiseaseControlTeam, StringComparison.Ordinal);

        return isRecognised;
    }

    /// <summary>
    /// Produces the display label for an account.
    /// </summary>
    private static string DisplayNameOf(ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return user.Email;
        }

        return "That account";
    }

    /// <summary>
    /// Produces the lower case human wording for an account state.
    /// </summary>
    private static string DescribeState(AccountStatus accountStatus)
    {
        if (accountStatus == AccountStatus.Active)
        {
            return "active";
        }

        if (accountStatus == AccountStatus.Disabled)
        {
            return "disabled";
        }

        return "pending";
    }

    /// <summary>
    /// Flattens identity failures into one user facing sentence. The prefix is
    /// localised; the identity error descriptions themselves come from the
    /// framework and are not localisable here.
    /// </summary>
    private string DescribeIdentityFailures(IdentityResult identityResult)
    {
        IEnumerable<string> descriptions =
            identityResult.Errors.Select(error => error.Description);

        string joinedDescriptions = string.Join("; ", descriptions);

        return Localizer["Service_IdentityFailuresPrefix", joinedDescriptions];
    }
}
