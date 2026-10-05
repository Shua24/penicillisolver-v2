namespace penicillisolver_v2.Services;

/// <summary>
/// The claim types the application adds on top of the claims issued by
/// ASP.NET Core Identity.
/// </summary>
public static class ApplicationClaimTypes
{
    /// <summary>
    /// Carries the account's <see cref="Domain.Enums.AccountStatus"/> name so
    /// that authorization can gate on activation without a database round trip
    /// on every request.
    /// </summary>
    public const string AccountStatus = "penicillisolver:account_status";

    /// <summary>
    /// Carries the role the registrant asked for. It is informational only and
    /// is never consulted by an authorization policy.
    /// </summary>
    public const string RequestedRole = "penicillisolver:requested_role";
}
