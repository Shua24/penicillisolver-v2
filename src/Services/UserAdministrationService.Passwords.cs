namespace penicillisolver_v2.Services;

using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;

/// <summary>
/// The password-reset operation of <see cref="UserAdministrationService"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the in-application recovery path. There is no email in this
/// application, so a reset cannot send the user a link: the pathologist sets a
/// temporary password, reads it off the screen and hands it over in person. The
/// account is then forced to replace it at the next sign-in, so the temporary
/// password is never a lasting credential.
/// </para>
/// <para>
/// <b>Why a pathologist may reset a peer.</b> Refusing it would mean a
/// pathologist who forgets their password has no in-application recovery at
/// all, because every recovery action requires an active pathologist. The
/// trade-off is accepted deliberately: the reset is recorded on the target
/// account, so a peer can see who reset them and when.
/// </para>
/// </remarks>
public sealed partial class UserAdministrationService
{
    /// <summary>
    /// Sets a temporary password for an account and forces a change at the next
    /// sign-in.
    /// </summary>
    /// <param name="actingUserId">The identifier of the pathologist performing the reset.</param>
    /// <param name="targetUserId">The identifier of the account being reset.</param>
    /// <param name="temporaryPassword">The password the pathologist will hand over.</param>
    /// <returns>Success, or a refusal message.</returns>
    public async Task<AdministrationResult> ResetPasswordAsync(
        string actingUserId,
        string targetUserId,
        string temporaryPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actingUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);

        // A blank password is a user error from the form, not a programming
        // error, so it is refused rather than thrown.
        if (string.IsNullOrWhiteSpace(temporaryPassword))
        {
            return AdministrationResult.Failure(Localizer["Settings_ResetPasswordRequired"]);
        }

        AdministrationResult authorizationResult = await AuthorizeAdministratorAsync(actingUserId);

        if (!authorizationResult.IsSuccess)
        {
            return authorizationResult;
        }

        ApplicationUser? targetUser = await userManager.FindByIdAsync(targetUserId);

        if (targetUser is null)
        {
            return AdministrationResult.Failure(Localizer["Service_AccountNoLongerExists"]);
        }

        // A disabled account cannot sign in, so handing its holder a new
        // password would be handing them a password that does not work. The
        // pathologist has to activate the account first, which is a separate,
        // deliberate action.
        if (targetUser.AccountStatus == AccountStatus.Disabled)
        {
            return AdministrationResult.Failure(Localizer["Service_ResetDisabledAccount"]);
        }

        // A reset token is used rather than RemovePassword/AddPassword because
        // it runs the same password validators as registration, so a temporary
        // password cannot be weaker than a normal one. It also rotates the
        // security stamp, which terminates every live session for the account.
        string resetToken = await userManager.GeneratePasswordResetTokenAsync(targetUser);

        IdentityResult resetResult = await userManager.ResetPasswordAsync(
            targetUser,
            resetToken,
            temporaryPassword);

        if (!resetResult.Succeeded)
        {
            return AdministrationResult.Failure(DescribeIdentityFailures(resetResult));
        }

        targetUser.MustChangePassword = true;
        targetUser.LastPasswordResetByUserId = actingUserId;
        targetUser.LastPasswordResetAtUtc = DateTimeOffset.UtcNow;

        IdentityResult updateResult = await userManager.UpdateAsync(targetUser);

        if (!updateResult.Succeeded)
        {
            return AdministrationResult.Failure(DescribeIdentityFailures(updateResult));
        }

        string successMessage = Localizer[
            "Service_PasswordReset",
            DisplayNameOf(targetUser)];

        AdministrationResult successResult = AdministrationResult.Success(successMessage);

        return successResult;
    }
}
