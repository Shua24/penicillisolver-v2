namespace penicillisolver_v2.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;

/// <summary>
/// The public write operations of <see cref="UserAdministrationService"/>: role
/// assignment, account activation and team permissions. Each one authorizes the
/// caller against current database state before applying anything, so the
/// service refuses a change even when a caller reaches it by some route the
/// page's <c>[Authorize]</c> attribute does not cover.
/// </summary>
public sealed partial class UserAdministrationService
{
    /// <summary>
    /// Assigns an account's single application role.
    /// </summary>
    /// <param name="actingUserId">The identifier of the administrator performing the change.</param>
    /// <param name="targetUserId">The identifier of the account being changed.</param>
    /// <param name="newRoleName">The role to assign.</param>
    /// <returns>Success, or a refusal message.</returns>
    public async Task<AdministrationResult> SetRoleAsync(
        string actingUserId,
        string targetUserId,
        string newRoleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actingUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRoleName);

        AdministrationResult authorizationResult = await AuthorizeAdministratorAsync(actingUserId);

        if (!authorizationResult.IsSuccess)
        {
            return authorizationResult;
        }

        if (!IsApplicationRole(newRoleName))
        {
            return AdministrationResult.Failure($"'{newRoleName}' is not a recognised role.");
        }

        ApplicationUser? targetUser = await userManager.FindByIdAsync(targetUserId);

        if (targetUser is null)
        {
            return AdministrationResult.Failure("That account no longer exists.");
        }

        string currentRoleName = await ResolveSingleRoleAsync(targetUser);

        bool roleIsChanging =
            !string.Equals(currentRoleName, newRoleName, StringComparison.Ordinal);

        if (!roleIsChanging)
        {
            string roleDisplayName = ApplicationRoleDisplayNames.DisplayNameOf(newRoleName);

            string unchangedMessage =
                $"{DisplayNameOf(targetUser)} already holds the {roleDisplayName} role.";

            return AdministrationResult.Success(unchangedMessage);
        }

        bool leavingPathologistRole = string.Equals(
            currentRoleName,
            ApplicationRoleNames.ClinicalPathologist,
            StringComparison.Ordinal);

        if (leavingPathologistRole)
        {
            AdministrationResult guardResult =
                await GuardLastActivePathologistAsync(targetUser.Id);

            if (!guardResult.IsSuccess)
            {
                return guardResult;
            }
        }

        AdministrationResult removeResult =
            await RemoveAllApplicationRolesAsync(targetUser, currentRoleName);

        if (!removeResult.IsSuccess)
        {
            return removeResult;
        }

        IdentityResult addRoleResult = await userManager.AddToRoleAsync(targetUser, newRoleName);

        if (!addRoleResult.Succeeded)
        {
            return AdministrationResult.Failure(DescribeIdentityFailures(addRoleResult));
        }

        string newRoleDisplayName = ApplicationRoleDisplayNames.DisplayNameOf(newRoleName);

        string successMessage =
            $"{DisplayNameOf(targetUser)}'s role is now {newRoleDisplayName}.";

        AdministrationResult successResult = AdministrationResult.Success(successMessage);

        return successResult;
    }

    /// <summary>
    /// Activates or disables an account. This is the step that moves a freshly
    /// registered account out of <see cref="AccountStatus.Pending"/>.
    /// </summary>
    /// <param name="actingUserId">The identifier of the administrator performing the change.</param>
    /// <param name="targetUserId">The identifier of the account being changed.</param>
    /// <param name="newState">The state to assign.</param>
    /// <returns>Success, or a refusal message.</returns>
    public async Task<AdministrationResult> SetAccountStatusAsync(
        string actingUserId,
        string targetUserId,
        AccountStatus newState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actingUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);

        AdministrationResult authorizationResult = await AuthorizeAdministratorAsync(actingUserId);

        if (!authorizationResult.IsSuccess)
        {
            return authorizationResult;
        }

        if (newState == AccountStatus.Pending)
        {
            return AdministrationResult.Failure(
                "An account cannot be returned to the pending state.");
        }

        ApplicationUser? targetUser = await userManager.FindByIdAsync(targetUserId);

        if (targetUser is null)
        {
            return AdministrationResult.Failure("That account no longer exists.");
        }

        if (targetUser.AccountStatus == newState)
        {
            string unchangedMessage =
                $"{DisplayNameOf(targetUser)} is already {DescribeState(newState)}.";

            return AdministrationResult.Success(unchangedMessage);
        }

        bool disablingAccount = newState == AccountStatus.Disabled;

        if (disablingAccount)
        {
            AdministrationResult guardResult =
                await GuardLastActivePathologistAsync(targetUser.Id);

            if (!guardResult.IsSuccess)
            {
                return guardResult;
            }
        }

        targetUser.AccountStatus = newState;

        // UpdateAsync rotates the security stamp, so the next sign-in issues a
        // principal carrying the new status claim. See the claim staleness note
        // on this class for what a live session sees.
        IdentityResult updateResult = await userManager.UpdateAsync(targetUser);

        if (!updateResult.Succeeded)
        {
            return AdministrationResult.Failure(DescribeIdentityFailures(updateResult));
        }

        string successMessage =
            $"{DisplayNameOf(targetUser)} is now {DescribeState(newState)}. " +
            "The change applies on that account's next sign-in.";

        AdministrationResult successResult = AdministrationResult.Success(successMessage);

        return successResult;
    }

    /// <summary>
    /// Sets the team's update and delete permissions on the single team row.
    /// This is per-team, not per-user.
    /// </summary>
    /// <param name="actingUserId">The identifier of the administrator performing the change.</param>
    /// <param name="canUpdateSpreadsheet">Whether the team may replace the current spreadsheet.</param>
    /// <param name="canDeleteSpreadsheet">Whether the team may remove the current spreadsheet.</param>
    /// <returns>Success, or a refusal message.</returns>
    public async Task<AdministrationResult> SetTeamPermissionsAsync(
        string actingUserId,
        bool canUpdateSpreadsheet,
        bool canDeleteSpreadsheet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actingUserId);

        AdministrationResult authorizationResult = await AuthorizeAdministratorAsync(actingUserId);

        if (!authorizationResult.IsSuccess)
        {
            return authorizationResult;
        }

        TeamPermission? teamPermission = await database.TeamPermissions
            .FirstOrDefaultAsync(row =>
                row.TeamName == ApplicationRoleNames.InfectiousDiseaseControlTeam);

        if (teamPermission is null)
        {
            teamPermission = new TeamPermission
            {
                TeamName = ApplicationRoleNames.InfectiousDiseaseControlTeam,
            };

            database.TeamPermissions.Add(teamPermission);
        }

        teamPermission.CanUpdateSpreadsheet = canUpdateSpreadsheet;
        teamPermission.CanDeleteSpreadsheet = canDeleteSpreadsheet;
        teamPermission.LastModifiedByUserId = actingUserId;
        teamPermission.LastModifiedAtUtc = DateTimeOffset.UtcNow;

        await database.SaveChangesAsync();

        string successMessage =
            "Team permissions updated. The change applies to the team's next request.";

        AdministrationResult successResult = AdministrationResult.Success(successMessage);

        return successResult;
    }
}
