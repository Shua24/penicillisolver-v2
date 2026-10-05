using System.Security.Claims;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Services;

/// <summary>
/// Adds the account status claim to every principal ASP.NET Core Identity
/// builds. This is what carries the pending gate: <see cref="Domain.Enums.AccountStatus.Pending"/>
/// accounts receive a claim that no authorization policy accepts, so they are
/// denied everywhere until a clinical pathologist activates them.
/// </summary>
public sealed class ApplicationUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    /// <inheritdoc />
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        ClaimsIdentity identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(
            new Claim(ApplicationClaimTypes.AccountStatus, user.AccountStatus.ToString()));

        if (!string.IsNullOrWhiteSpace(user.RequestedRole))
        {
            identity.AddClaim(new Claim(ApplicationClaimTypes.RequestedRole, user.RequestedRole));
        }

        return identity;
    }
}
