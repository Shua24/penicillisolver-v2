namespace penicillisolver_v2.Services;

using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Entities;

/// <summary>
/// The account-deletion operation of <see cref="UserAdministrationService"/>.
/// It serves two callers: a clinical pathologist deleting another account from
/// the user and team settings page, and any user voluntarily deleting their own
/// account. The two paths differ only in who may invoke them; the
/// last-active-pathologist lockout guard applies to both.
/// </summary>
public sealed partial class UserAdministrationService
{
    /// <summary>
    /// Deletes an account. In administrator mode (<paramref name="isSelfDelete"/>
    /// is false) the caller must be an active clinical pathologist. In both modes
    /// deleting the last active pathologist is refused, because activation is the
    /// only way an account gains a role and an empty pool is a permanent lockout.
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

        // The lockout guard runs for self-delete too. It used to be skipped
        // there because the bootstrap seeder rebuilt the pool on the next
        // startup, and that account generator no longer exists.
        AdministrationResult guardResult = await GuardLastActivePathologistAsync(targetUser.Id);

        if (!guardResult.IsSuccess)
        {
            AdministrationResult refusalResult = AdministrationResult.Failure(
                "This is the last active clinical pathologist. Deleting this account " +
                "would leave nobody able to manage users or the spreadsheet.");

            return refusalResult;
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
