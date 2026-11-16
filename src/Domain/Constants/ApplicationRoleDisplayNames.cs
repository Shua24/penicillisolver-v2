namespace penicillisolver_v2.Domain.Constants;

/// <summary>
/// The human readable names the application shows next to a role. The stable
/// PascalCase tokens in <see cref="ApplicationRoleNames"/> remain the values
/// stored in the database, carried in authentication claims, and used as
/// configuration keys; only what a person reads in the UI is mapped here.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DisplayNameOf"/> is the English label, used where no localizer is
/// available: the seeder's log lines and the startup diagnostics. Everything a
/// reader sees goes through <see cref="ResourceKeyOf"/> and the shared
/// resources, because a role name inside an Indonesian sentence has to be
/// Indonesian too.
/// </para>
/// <para>
/// The token-to-key mapping lives here, and only here, so the registration
/// page, the settings page and the administration service cannot disagree about
/// which resource key a role uses. Each of them resolves the key with its own
/// localizer, which is the part that differs between an HTTP request and an
/// interactive circuit.
/// </para>
/// </remarks>
public static class ApplicationRoleDisplayNames
{
    /// <summary>
    /// Returns the display name for a role token. An unknown token is returned
    /// unchanged so that a new role never renders as an empty label.
    /// </summary>
    /// <param name="roleToken">The stable role token.</param>
    /// <returns>The English label, or the token itself when it is not recognised.</returns>
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

    /// <summary>
    /// Returns the shared resource key holding a role's translated display name.
    /// </summary>
    /// <param name="roleToken">The stable role token.</param>
    /// <returns>The resource key, or the empty string when the token is not a recognised role.</returns>
    public static string ResourceKeyOf(string roleToken)
    {
        ArgumentNullException.ThrowIfNull(roleToken);

        string resourceKey = roleToken switch
        {
            ApplicationRoleNames.ClinicalPathologist => "Role_ClinicalPathologist",
            ApplicationRoleNames.OtherDoctor => "Role_OtherDoctor",
            ApplicationRoleNames.InfectiousDiseaseControlTeam => "Role_InfectiousDiseaseControlTeam",
            _ => string.Empty,
        };

        return resourceKey;
    }
}
