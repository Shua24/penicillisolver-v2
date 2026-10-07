namespace penicillisolver_v2.Domain.Constants;

/// <summary>
/// The human readable names the application shows next to a role. The stable
/// PascalCase tokens in <see cref="ApplicationRoleNames"/> remain the values
/// stored in the database, carried in authentication claims, and used as
/// configuration keys; only what a person reads in the UI is mapped here.
/// </summary>
/// <remarks>
/// <b>This mapping is deliberately English and is NOT localised.</b> It lives in
/// the service layer, where the messages that name a role are also kept in
/// English on purpose (see the localisation boundary note on
/// <c>UserAdministrationService</c>): role names are a display concern owned by
/// the pages, and a service layer that translated them itself would drift from
/// what the Razor components show. The user facing translations live in the
/// <c>Role_*</c> resource keys and are resolved at the point of display, in the
/// components that actually render a role to a reader.
/// </remarks>
public static class ApplicationRoleDisplayNames
{
    /// <summary>
    /// Returns the display name for a role token. An unknown token is returned
    /// unchanged so that a new role never renders as an empty label.
    /// </summary>
    public static string DisplayNameOf(string roleToken)
    {
        ArgumentNullException.ThrowIfNull(roleToken);

        string displayName = roleToken switch
        {
            ApplicationRoleNames.ClinicalPathologist => "Clinical Pathologist",
            ApplicationRoleNames.OtherDoctor => "Doctor",
            ApplicationRoleNames.InfectiousDiseaseControlTeam => "Infectious Disease Control Team",
            _ => roleToken,
        };

        return displayName;
    }
}
