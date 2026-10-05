using System.Security.Claims;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Components.Account;

/// <summary>
/// The form based HTTP endpoints the account pages depend on. Sign in and sign
/// out must happen on an HTTP request because the authentication cookie can
/// only be written to a real response, never from inside an interactive Blazor
/// circuit where <c>HttpContext</c> is null.
/// </summary>
public static class IdentityComponentsEndpointRouteBuilderExtensions
{
    /// <summary>Maps the additional endpoints required by the account pages.</summary>
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder accountGroup = endpoints.MapGroup("/Account");

        accountGroup.MapPost("/Logout", async (
            ClaimsPrincipal user,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect($"~/{returnUrl}");
        });

        return accountGroup;
    }
}
