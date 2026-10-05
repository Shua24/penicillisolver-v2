using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Services;

/// <summary>
/// Decides the team-update/delete requirement by reading the current
/// <see cref="Domain.Entities.TeamPermission"/> row for the infectious disease
/// control team.
/// </summary>
/// <remarks>
/// This handler deliberately succeeds in two cases:
/// <list type="bullet">
/// <item>
/// the principal is a clinical pathologist, who holds update and delete
/// permissions unconditionally and whose permission the requirements section of
/// the brief marks as unchangeable; and
/// </item>
/// <item>
/// the principal is a member of the infectious disease control team and the
/// team's current record grants the specific permission being asked about.
/// </item>
/// </list>
/// Every other principal fails. A member of the team whose account is not
/// active also fails, because the account status is checked here as well; the
/// policy's own active-account requirement is the primary gate, and this
/// handler must not become a way around it.
/// </remarks>
public sealed class TeamSpreadsheetPermissionHandler(ApplicationDbContext database)
    : AuthorizationHandler<TeamSpreadsheetPermissionRequirement>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TeamSpreadsheetPermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        bool isActiveAccount = IsActiveAccount(context.User);

        if (!isActiveAccount)
        {
            return;
        }

        bool isClinicalPathologist = context.User.IsInRole(ApplicationRoleNames.ClinicalPathologist);

        if (isClinicalPathologist)
        {
            context.Succeed(requirement);
            return;
        }

        bool isTeamMember = context.User.IsInRole(ApplicationRoleNames.InfectiousDiseaseControlTeam);

        if (!isTeamMember)
        {
            return;
        }

        bool teamHoldsPermission = await TeamHoldsPermissionAsync(requirement.PermissionKind);

        if (teamHoldsPermission)
        {
            context.Succeed(requirement);
        }
    }

    /// <summary>
    /// Reports whether the principal carries an active account status claim.
    /// </summary>
    private static bool IsActiveAccount(System.Security.Claims.ClaimsPrincipal principal)
    {
        System.Security.Claims.Claim? accountStatusClaim =
            principal.FindFirst(ApplicationClaimTypes.AccountStatus);

        bool isActive = accountStatusClaim is not null
            && string.Equals(
                accountStatusClaim.Value,
                nameof(AccountStatus.Active),
                StringComparison.Ordinal);

        return isActive;
    }

    /// <summary>
    /// Reads the team's current permission row and reports the asked-about flag.
    /// </summary>
    private async Task<bool> TeamHoldsPermissionAsync(TeamSpreadsheetPermissionKind permissionKind)
    {
        Domain.Entities.TeamPermission? teamPermission = await database.TeamPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TeamName == ApplicationRoleNames.InfectiousDiseaseControlTeam);

        if (teamPermission is null)
        {
            return false;
        }

        if (permissionKind == TeamSpreadsheetPermissionKind.Update)
        {
            return teamPermission.CanUpdateSpreadsheet;
        }

        return teamPermission.CanDeleteSpreadsheet;
    }
}
