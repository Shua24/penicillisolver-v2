namespace penicillisolver_v2.Domain.Constants;

/// <summary>
/// The names of the three roles the application recognises. These strings are
/// chosen by registrants on the registration form and are seeded into the
/// identity role table on startup. A registration is always pending: a
/// clinical pathologist activates it before the requested role takes effect.
/// </summary>
public static class ApplicationRoleNames
{
    /// <summary>
    /// A clinical pathologist. This is the only role that may manage the shared
    /// spreadsheet, update or delete it, and manage other users' roles.
    /// </summary>
    public const string ClinicalPathologist = "ClinicalPathologist";

    /// <summary>A doctor other than a clinical pathologist. May read the spreadsheet.</summary>
    public const string OtherDoctor = "OtherDoctor";

    /// <summary>
    /// A member of the infectious disease control team. May read the spreadsheet.
    /// </summary>
    public const string InfectiousDiseaseControlTeam = "InfectiousDiseaseControlTeam";
}
