namespace penicillisolver_v2.Domain.Constants;

/// <summary>
/// The names of the authorization policies the application registers. Each name
/// is a string constant equal to the corresponding property name so that
/// <c>[Authorize(Policy = AuthorizationPolicyNames.CanReadSpreadsheet)]</c>
/// reads naturally at the call site.
/// </summary>
public static class AuthorizationPolicyNames
{
    /// <summary>Allows managing the shared spreadsheet. Requires the clinical pathologist role.</summary>
    public const string CanManageSpreadsheet = "CanManageSpreadsheet";

    /// <summary>Allows reading the shared spreadsheet. Requires any of the three roles.</summary>
    public const string CanReadSpreadsheet = "CanReadSpreadsheet";

    /// <summary>Allows assigning roles to other accounts. Requires the clinical pathologist role.</summary>
    public const string CanManageUserRoles = "CanManageUserRoles";

    /// <summary>
    /// Allows creating, editing, and deleting antibiotic abbreviation mappings.
    /// Held only by the clinical pathologist; the other two roles may view them.
    /// </summary>
    public const string CanManageAntibioticMappings = "CanManageAntibioticMappings";

    /// <summary>
    /// Allows replacing the shared spreadsheet. Held by the clinical pathologist
    /// unconditionally, and by the infectious disease control team when the team
    /// record grants update.
    /// </summary>
    public const string CanUpdateSpreadsheet = "CanUpdateSpreadsheet";

    /// <summary>
    /// Allows removing the shared spreadsheet. Held by the clinical pathologist
    /// unconditionally, and by the infectious disease control team when the team
    /// record grants delete.
    /// </summary>
    public const string CanDeleteSpreadsheet = "CanDeleteSpreadsheet";
}
