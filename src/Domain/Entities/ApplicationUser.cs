using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Domain.Entities;

/// <summary>
/// An application account. A freshly registered account always starts with
/// <see cref="AccountStatus.Pending"/> and stores the role that the registrant
/// asked for in <see cref="RequestedRole"/>. A clinical pathologist later
/// activates the account and assigns the real role.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>The human readable name shown throughout the application.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The role the registrant selected on the registration form. This is a
    /// request only: it grants no permissions until a clinical pathologist
    /// activates the account.
    /// </summary>
    public string RequestedRole { get; set; } = string.Empty;

    /// <summary>
    /// The account state. Every authorization policy requires
    /// <see cref="AccountStatus.Active"/>, so a pending account has no access
    /// anywhere in the application.
    /// </summary>
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Pending;
}
