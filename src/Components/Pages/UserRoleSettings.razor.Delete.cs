using penicillisolver_v2.Services;

namespace penicillisolver_v2.Components.Pages;

public partial class UserRoleSettings
{
    private string? pendingDeleteUserId;
    private bool isDeleting;

    private async Task DeleteAccountAsync(UserAccountSummary account)
    {
        if (isDeleting || pendingDeleteUserId != account.UserId)
        {
            return;
        }

        isDeleting = true;

        try
        {
            if (actingUserId is null)
            {
                statusMessage = "Error: your session has expired. Sign in again and retry.";
                return;
            }

            // Administrator deletion retains authorization and the last-active-
            // pathologist guard. Voluntary self-deletion uses a separate route.
            AdministrationResult result = await AdministrationService.DeleteUserAsync(
                actingUserId,
                account.UserId,
                isSelfDelete: false);

            await ApplyResultAsync(result);
        }
        finally
        {
            pendingDeleteUserId = null;
            isDeleting = false;
        }
    }
}
