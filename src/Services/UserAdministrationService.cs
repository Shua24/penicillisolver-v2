namespace penicillisolver_v2.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Resources;

/// <summary>
/// A single account as the settings page needs to display it.
/// </summary>
/// <param name="UserId">The identity primary key of the account.</param>
/// <param name="DisplayName">The account holder's display name.</param>
/// <param name="Email">The sign-in email address.</param>
/// <param name="RoleName">The account's current application role, or the empty string when it holds none yet.</param>
/// <param name="AccountStatus">The account's current activation state.</param>
public sealed record UserAccountSummary(
    string UserId,
    string DisplayName,
    string Email,
    string RoleName,
    AccountStatus AccountStatus);

/// <summary>
/// The single team's spreadsheet permissions as the settings page needs them.
/// </summary>
/// <param name="TeamName">The team the permissions belong to.</param>
/// <param name="CanUpdateSpreadsheet">Whether the team may replace the current spreadsheet.</param>
/// <param name="CanDeleteSpreadsheet">Whether the team may remove the current spreadsheet.</param>
public sealed record TeamPermissionSummary(
    string TeamName,
    bool CanUpdateSpreadsheet,
    bool CanDeleteSpreadsheet);

/// <summary>
/// The role, activation and team-permission mutation layer behind the settings
/// page.
/// </summary>
/// <remarks>
/// <para>
/// Every write is guarded here, server side, rather than only by the page's
/// <c>[Authorize]</c> attribute. The attribute keeps ordinary navigation out,
/// but the service is the boundary that a crafted circuit message, a future
/// non-page caller, or a mistaken page edit cannot get past, so each public
/// method re-checks the caller's role before touching anything.
/// </para>
/// <para>
/// <b>The last pathologist guard.</b> Disabling or demoting the last active
/// clinical pathologist would leave the application with nobody able to manage
/// users or the spreadsheet, and no in-application way to recover: the
/// administrator who could have restored the role is the one who just lost it.
/// Both <see cref="SetAccountStatusAsync"/> and <see cref="SetRoleAsync"/>
/// therefore refuse an operation that would leave zero active clinical
/// pathologists. The guard counts the account being changed out before
/// comparing, so demoting one of two pathologists is allowed while demoting the
/// only one is not.
/// </para>
/// <para>
/// <b>Claim staleness.</b> The role and the account status both travel as
/// claims issued at sign-in, so a change made here is not visible to a session
/// that is already open until that session is revalidated. The provider
/// (<see cref="IdentityRevalidatingAuthenticationStateProvider"/>) revalidates
/// the security stamp every thirty minutes, and a fresh sign-in always picks
/// the change up immediately. The service does not attempt to force a live
/// session to refresh.
/// </para>
/// <para>
/// <b>Localisation boundary.</b> Messages that name a role stay in English on
/// purpose: role names are display values owned by the UI, and a service layer
/// that invented its own translated role labels would drift from the pages. All
/// other user facing messages are localised here, because a message produced by
/// a refusal must read in the reader's language even though no page authored it.
/// </para>
/// </remarks>
public sealed partial class UserAdministrationService(
    ApplicationDbContext database,
    UserManager<ApplicationUser> userManager,
    IStringLocalizerFactory localizerFactory)
{
    /// <summary>
    /// The localizer for this service's own messages. Built from the factory
    /// because the service is not a Razor component and cannot receive the
    /// component localizer.
    /// </summary>
    private IStringLocalizer Localizer { get; } =
        localizerFactory.Create(typeof(SharedResource));

    /// <summary>
    /// Lists every account with its role and status, ordered by display name.
    /// </summary>
    /// <returns>One summary per account.</returns>
    public async Task<IReadOnlyList<UserAccountSummary>> GetAccountsAsync()
    {
        List<ApplicationUser> accounts = await database.Users
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Email)
            .ToListAsync();

        List<UserAccountSummary> summaries = new List<UserAccountSummary>();

        foreach (ApplicationUser account in accounts)
        {
            string roleName = await ResolveSingleRoleAsync(account);

            UserAccountSummary summary = new(
                UserId: account.Id,
                DisplayName: account.DisplayName,
                Email: account.Email ?? string.Empty,
                RoleName: roleName,
                AccountStatus: account.AccountStatus);

            summaries.Add(summary);
        }

        return summaries;
    }

    /// <summary>
    /// Returns the current permissions of the infectious disease control team.
    /// </summary>
    /// <returns>The team's permissions, defaulting to denied when no row exists.</returns>
    public async Task<TeamPermissionSummary> GetTeamPermissionsAsync()
    {
        TeamPermission? teamPermission = await database.TeamPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(row =>
                row.TeamName == ApplicationRoleNames.InfectiousDiseaseControlTeam);

        if (teamPermission is null)
        {
            TeamPermissionSummary defaultSummary = new(
                TeamName: ApplicationRoleNames.InfectiousDiseaseControlTeam,
                CanUpdateSpreadsheet: false,
                CanDeleteSpreadsheet: false);

            return defaultSummary;
        }

        TeamPermissionSummary summary = new(
            TeamName: teamPermission.TeamName,
            CanUpdateSpreadsheet: teamPermission.CanUpdateSpreadsheet,
            CanDeleteSpreadsheet: teamPermission.CanDeleteSpreadsheet);

        return summary;
    }
}
