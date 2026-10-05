using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// A stand-in for the authorization service that applies the same rule as the
/// real <c>CanManageAntibioticMappings</c> policy: the principal must be an
/// active clinical pathologist. It lets the mapping tests exercise the service's
/// own gate without standing up the whole policy pipeline, which the integration
/// tests cover separately.
/// </summary>
internal sealed class StubAuthorizationService : IAuthorizationService
{
    /// <summary>Builds a principal carrying the supplied role and account status.</summary>
    public static ClaimsPrincipal BuildPrincipal(string roleName, AccountStatus accountStatus)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, "acting-user-id"),
            new Claim(ClaimTypes.Role, roleName),
            new Claim(ApplicationClaimTypes.AccountStatus, accountStatus.ToString()),
        ];

        ClaimsIdentity identity = new(claims, authenticationType: "Test");
        ClaimsPrincipal principal = new(identity);

        return principal;
    }

    /// <inheritdoc />
    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        IEnumerable<IAuthorizationRequirement> requirementList = requirements.ToList();

        return Task.FromResult(Evaluate(user, requirementList));
    }

    /// <inheritdoc />
    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        string policyName)
    {
        return Task.FromResult(EvaluateForPolicy(user));
    }

    private static AuthorizationResult EvaluateForPolicy(ClaimsPrincipal user)
    {
        bool isPathologist = user.IsInRole(ApplicationRoleNames.ClinicalPathologist);

        Claim? accountStatusClaim = user.FindFirst(ApplicationClaimTypes.AccountStatus);
        bool isActive = accountStatusClaim is not null
            && string.Equals(
                accountStatusClaim.Value,
                nameof(AccountStatus.Active),
                StringComparison.Ordinal);

        if (isPathologist && isActive)
        {
            return AuthorizationResult.Success();
        }

        return AuthorizationResult.Failed();
    }

    private static AuthorizationResult Evaluate(
        ClaimsPrincipal user,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        return EvaluateForPolicy(user);
    }
}
