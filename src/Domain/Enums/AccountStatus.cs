namespace penicillisolver_v2.Domain.Enums;

/// <summary>
/// The state of a registered account. A newly registered account is always
/// <see cref="Pending"/> and holds no permissions until a clinical pathologist
/// assigns it a role from the administration settings page.
/// </summary>
public enum AccountStatus
{
    /// <summary>The account exists and can sign in, but every authorization policy denies it.</summary>
    Pending = 0,

    /// <summary>The account has been assigned a role and may use that role's permissions.</summary>
    Active = 1,

    /// <summary>The account is blocked. It can no longer sign in.</summary>
    Disabled = 2,
}
