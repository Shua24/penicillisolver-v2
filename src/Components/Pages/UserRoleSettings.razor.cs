using System.Security.Claims;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;

namespace penicillisolver_v2.Components.Pages;

/// <summary>
/// The code behind the user and team settings page. It lives in its own file so
/// the markup stays readable and the page stays under the project's 300-line
/// cap; the page was already over it before password recovery was added.
/// </summary>
public partial class UserRoleSettings
{
    private static readonly string[] AssignableRoleNames =
    [
        ApplicationRoleNames.ClinicalPathologist,
        ApplicationRoleNames.OtherDoctor,
        ApplicationRoleNames.InfectiousDiseaseControlTeam,
    ];

    private IReadOnlyList<UserAccountSummary> accounts = [];
    private TeamPermissionSummary teamPermissions = new(
        ApplicationRoleNames.InfectiousDiseaseControlTeam,
        CanUpdateSpreadsheet: false,
        CanDeleteSpreadsheet: false);
    private string? statusMessage;

    /// <summary>
    /// Whether <see cref="statusMessage"/> describes a failure. Set alongside
    /// the message so the banner's severity is explicit rather than inferred
    /// from an "Error" prefix, which cannot survive translation.
    /// </summary>
    private bool statusFailed;
    private string? actingUserId;
    private bool isLoading = true;

    /// <summary>The account whose password reset form is open, or null.</summary>
    private string? pendingResetUserId;

    /// <summary>The temporary password typed into the open reset form.</summary>
    private string resetPassword = string.Empty;

    /// <summary>Whether a reset is in flight, so the buttons can be disabled.</summary>
    private bool isResetting;

    /// <summary>Which team permission a toggle controls.</summary>
    private enum PermissionToggle
    {
        /// <summary>The team may replace the current spreadsheet.</summary>
        Update,

        /// <summary>The team may remove the current spreadsheet.</summary>
        Delete,
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        actingUserId = await ResolveCurrentUserIdAsync();

        await ReloadAsync();

        isLoading = false;
    }

    private async Task ReloadAsync()
    {
        accounts = await AdministrationService.GetAccountsAsync();
        teamPermissions = await AdministrationService.GetTeamPermissionsAsync();
    }

    /// <summary>
    /// The localized label for a role token. The token itself stays as the
    /// option value because it is what the database stores, but what the reader
    /// sees is translated here at the point of display. The service layer keeps
    /// its own English mapping; this page is the user facing side.
    /// </summary>
    private string DescribeRole(string roleName)
    {
        string resourceKey = roleName switch
        {
            ApplicationRoleNames.ClinicalPathologist => "Role_ClinicalPathologist",
            ApplicationRoleNames.OtherDoctor => "Role_OtherDoctor",
            ApplicationRoleNames.InfectiousDiseaseControlTeam => "Role_InfectiousDiseaseControlTeam",
            _ => string.Empty,
        };

        if (resourceKey.Length == 0)
        {
            return ApplicationRoleDisplayNames.DisplayNameOf(roleName);
        }

        return Localizer[resourceKey];
    }

    /// <summary>
    /// Describes the last password reset for an account, naming the pathologist
    /// who performed it or saying that it came from the break-glass command.
    /// </summary>
    private string DescribeLastReset(UserAccountSummary account)
    {
        DateTimeOffset resetAt = account.LastPasswordResetAtUtc!.Value;

        string resetAtText = resetAt.ToLocalTime().ToString("f");

        if (string.IsNullOrWhiteSpace(account.LastPasswordResetByDisplayName))
        {
            return Localizer["Settings_LastResetByOperator", resetAtText];
        }

        return Localizer[
            "Settings_LastResetBy",
            account.LastPasswordResetByDisplayName,
            resetAtText];
    }

    private void BeginReset(UserAccountSummary account)
    {
        pendingResetUserId = account.UserId;
        resetPassword = string.Empty;
    }

    private void CancelReset()
    {
        pendingResetUserId = null;
        resetPassword = string.Empty;
    }

    private async Task ResetPasswordAsync(UserAccountSummary account)
    {
        if (actingUserId is null)
        {
            statusFailed = true;
            statusMessage = Localizer["Status_SessionExpired"];
            return;
        }

        if (string.IsNullOrWhiteSpace(resetPassword))
        {
            statusFailed = true;
            statusMessage = Localizer["Status_ErrorPrefix"] + Localizer["Settings_ResetPasswordRequired"];
            return;
        }

        isResetting = true;

        AdministrationResult result = await AdministrationService.ResetPasswordAsync(
            actingUserId,
            account.UserId,
            resetPassword);

        isResetting = false;

        if (result.IsSuccess)
        {
            pendingResetUserId = null;
            resetPassword = string.Empty;
        }

        await ApplyResultAsync(result);
    }

    private async Task ChangeRoleAsync(UserAccountSummary account, ChangeEventArgs eventArgs)
    {
        if (actingUserId is null)
        {
            statusFailed = true;
            statusMessage = Localizer["Status_SessionExpired"];
            return;
        }

        string? newRoleName = eventArgs.Value?.ToString();

        if (string.IsNullOrWhiteSpace(newRoleName))
        {
            return;
        }

        AdministrationResult result = await AdministrationService.SetRoleAsync(
            actingUserId,
            account.UserId,
            newRoleName);

        await ApplyResultAsync(result);
    }

    private async Task ChangeStatusAsync(UserAccountSummary account, AccountStatus newState)
    {
        if (actingUserId is null)
        {
            statusFailed = true;
            statusMessage = Localizer["Status_SessionExpired"];
            return;
        }

        AdministrationResult result = await AdministrationService.SetAccountStatusAsync(
            actingUserId,
            account.UserId,
            newState);

        await ApplyResultAsync(result);
    }

    private async Task ToggleTeamPermissionAsync(ChangeEventArgs eventArgs, PermissionToggle toggle)
    {
        if (actingUserId is null)
        {
            statusFailed = true;
            statusMessage = Localizer["Status_SessionExpired"];
            return;
        }

        bool isChecked = eventArgs.Value is true;

        bool canUpdate = teamPermissions.CanUpdateSpreadsheet;
        bool canDelete = teamPermissions.CanDeleteSpreadsheet;

        if (toggle == PermissionToggle.Update)
        {
            canUpdate = isChecked;
        }
        else
        {
            canDelete = isChecked;
        }

        AdministrationResult result = await AdministrationService.SetTeamPermissionsAsync(
            actingUserId,
            canUpdate,
            canDelete);

        await ApplyResultAsync(result);
    }

    private async Task ApplyResultAsync(AdministrationResult result)
    {
        statusFailed = !result.IsSuccess;

        statusMessage = result.IsSuccess
            ? result.Message
            : Localizer["Status_ErrorPrefix"] + result.Message;

        await ReloadAsync();
    }

    private async Task<string?> ResolveCurrentUserIdAsync()
    {
        AuthenticationState authenticationState =
            await AuthenticationStateProvider.GetAuthenticationStateAsync();

        Claim? nameIdentifierClaim =
            authenticationState.User.FindFirst(ClaimTypes.NameIdentifier);

        string? userId = nameIdentifierClaim?.Value;

        return userId;
    }
}
