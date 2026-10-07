namespace penicillisolver_v2.Services;

using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Entities;

/// <summary>
/// The account-deletion operation of <see cref="UserAdministrationService"/>.
/// It serves two callers: a clinical pathologist deleting another account from
/// the user and team settings page, and any user voluntarily deleting their own
/// account. The two paths differ only in who may invoke them and whether the
/// last-active-pathologist lockout guard applies.
/// </summary>
public sealed partial class UserAdministrationService
{
    /// <summary>
    /// Deletes an account. In administrator mode (<paramref name="isSelfDelete"/>)
    /// is false the caller must be an active clinical pathologist and deleting the
    /// last active pathologist is refused. In self mode it is exempt from that
    /// guard, because it is the escape hatch: if it leaves no active pathologist,
    /// the bootstrap seeder repairs the pool on the next startup.
    /// </summary>
    /// <param name="actingUserId">The identifier of the account performing the delete.</param>
    /// <param name="targetUserId">The identifier of the account being deleted.</param>
    /// <param name="isSelfDelete">When true, the caller deletes their own account and the lockout guard is skipped.</param>
    /// <returns>Success, or a refusal message.</returns>
    public async Task<AdministrationResult> DeleteUserAsync(
        string actingUserId,
        string targetUserId,
        bool isSelfDelete)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actingUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);

        if (isSelfDelete)
        {
            bool isTheSameAccount =
                string.Equals(actingUserId, targetUserId, StringComparison.Ordinal);

            if (!isTheSameAccount)
            {
                return AdministrationResult.Failure(Localizer["Service_DeleteOnlyOwnAccount"]);
            }
        }
        else
        {
            AdministrationResult authorizationResult = await AuthorizeAdministratorAsync(actingUserId);

            if (!authorizationResult.IsSuccess)
            {
                return authorizationResult;
            }
        }

        ApplicationUser? targetUser = await userManager.FindByIdAsync(targetUserId);

        if (targetUser is null)
        {
            return AdministrationResult.Failure(Localizer["Service_AccountNoLongerExists"]);
        }

        if (!isSelfDelete)
        {
            AdministrationResult guardResult = await GuardLastActivePathologistAsync(targetUser.Id);

            if (!guardResult.IsSuccess)
            {
                AdministrationResult refusalResult = AdministrationResult.Failure(
                    "This is the last active clinical pathologist. Deleting this account " +
                    "would leave nobody able to manage users or the spreadsheet.");

                return refusalResult;
            }
        }

        string targetName = DisplayNameOf(targetUser);

        IdentityResult deletionResult = await userManager.DeleteAsync(targetUser);

        if (!deletionResult.Succeeded)
        {
            return AdministrationResult.Failure(DescribeIdentityFailures(deletionResult));
        }

        // The audit columns on spreadsheet uploads and abbreviation mappings hold
        // this user's identifier as a plain string (not a foreign key), so the
        // delete simply orphans those references; their history is preserved.
        string successMessage = Localizer["Service_AccountDeleted", targetName];

        AdministrationResult successResult = AdministrationResult.Success(successMessage);

        return successResult;
    }
}
