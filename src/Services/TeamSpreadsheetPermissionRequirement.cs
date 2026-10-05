using Microsoft.AspNetCore.Authorization;

namespace penicillisolver_v2.Services;

/// <summary>
/// Requires that the Infectious Disease Control Team has been granted the
/// update/delete permission by a clinical pathologist.
/// </summary>
/// <remarks>
/// Unlike the role requirements, this cannot be answered from the principal's
/// claims alone. A clinical pathologist can flip the team's permission while a
/// member is signed in, so the decision has to be read from current state at
/// the moment of the request. The policy that uses this requirement also grants
/// the clinical pathologist unconditional access, so a pathologist never needs
/// the flag.
/// </remarks>
public sealed class TeamSpreadsheetPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Creates the requirement.
    /// </summary>
    /// <param name="permissionKind">Which permission on the team record satisfies this requirement.</param>
    public TeamSpreadsheetPermissionRequirement(TeamSpreadsheetPermissionKind permissionKind)
    {
        PermissionKind = permissionKind;
    }

    /// <summary>Which permission on the team record satisfies this requirement.</summary>
    public TeamSpreadsheetPermissionKind PermissionKind { get; }
}

/// <summary>
/// The permission on the team record that a requirement checks.
/// </summary>
public enum TeamSpreadsheetPermissionKind
{
    /// <summary>The team may replace the current spreadsheet.</summary>
    Update = 0,

    /// <summary>The team may remove the current spreadsheet.</summary>
    Delete = 1,
}
