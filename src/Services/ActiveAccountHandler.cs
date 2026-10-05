using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Services;

/// <summary>
/// Succeeds only when the principal carries an
/// <see cref="ApplicationClaimTypes.AccountStatus"/> claim whose value is
/// <see cref="AccountStatus.Active"/>. A principal with no such claim fails:
/// that is what keeps every account that has never been activated out of the
/// application.
/// </summary>
public sealed class ActiveAccountHandler : AuthorizationHandler<ActiveAccountRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveAccountRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        Claim? accountStatusClaim = context.User.FindFirst(ApplicationClaimTypes.AccountStatus);
        bool isActiveAccount = accountStatusClaim is not null
            && string.Equals(
                accountStatusClaim.Value,
                nameof(AccountStatus.Active),
                StringComparison.Ordinal);

        if (isActiveAccount)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
