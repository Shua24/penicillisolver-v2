using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Services;

/// <summary>
/// Revalidates the security stamp of the connected user every thirty minutes
/// while an interactive circuit is open, so that a disabled or re-roled account
/// loses its circuit promptly.
/// </summary>
public sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    /// <inheritdoc />
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(RevalidationIntervalMinutes);

    /// <inheritdoc />
    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        // Take the user manager from a new scope so it fetches fresh data.
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        bool isValidSecurityStamp = await ValidateSecurityStampAsync(
            userManager,
            authenticationState.User,
            cancellationToken);

        return isValidSecurityStamp;
    }

    /// <summary>
    /// Checks whether the principal's security stamp claim still matches the
    /// user's current security stamp in the store.
    /// </summary>
    /// <param name="userManager">The user manager to fetch the current stamp from.</param>
    /// <param name="principal">The connected user's claims principal.</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns>True when the user exists and the stamps match or stamps are unsupported.</returns>
    private async Task<bool> ValidateSecurityStampAsync(
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.GetUserAsync(principal);

        if (user is null)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        string? principalStamp =
            principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        string userStamp = await userManager.GetSecurityStampAsync(user);

        return principalStamp == userStamp;
    }

    private const int RevalidationIntervalMinutes = 30;
}
