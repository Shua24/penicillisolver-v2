using Microsoft.AspNetCore.Authorization;

using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Services;

/// <summary>
/// Requires the signed in account to be <see cref="AccountStatus.Active"/>.
/// This is the pending gate: a freshly registered account is pending and every
/// policy that includes this requirement denies it until a clinical pathologist
/// activates the account.
/// </summary>
public sealed class ActiveAccountRequirement : IAuthorizationRequirement
{
}
