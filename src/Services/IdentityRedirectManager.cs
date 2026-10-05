using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Services;

/// <summary>
/// Centralises the redirects and status messages used by the account pages.
/// The project sets <c>BlazorDisableThrowNavigationException</c>, so the
/// navigation calls below never throw; each one simply returns to the caller
/// and the redirect is applied when the response is written.
/// </summary>
public sealed class IdentityRedirectManager
{
    /// <summary>The name of the short lived cookie used to surface a status message.</summary>
    public const string StatusCookieName = "Identity.StatusMessage";

    private static readonly CookieBuilder StatusCookieBuilder = new()
    {
        SameSite = SameSiteMode.Strict,
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromSeconds(5),
    };

    private readonly NavigationManager navigationManager;

    /// <summary>Creates the manager over the circuit's navigation manager.</summary>
    public IdentityRedirectManager(NavigationManager navigationManager)
    {
        ArgumentNullException.ThrowIfNull(navigationManager);
        this.navigationManager = navigationManager;
    }

    private string CurrentPath =>
        navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

    /// <summary>Navigates to the supplied uri, rejecting absolute external targets.</summary>
    public void RedirectTo(string? uri)
    {
        uri ??= string.Empty;

        // Prevent open redirects.
        if (!Uri.IsWellFormedUriString(uri, UriKind.Relative)
            || uri.StartsWith("//", StringComparison.Ordinal))
        {
            uri = navigationManager.ToBaseRelativePath(uri);
        }

        navigationManager.NavigateTo(uri);
    }

    /// <summary>Navigates to the supplied uri with the supplied query parameters.</summary>
    public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
    {
        string uriWithoutQuery =
            navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
        string newUri = navigationManager.GetUriWithQueryParameters(
            uriWithoutQuery,
            queryParameters);

        RedirectTo(newUri);
    }

    /// <summary>Writes a status message cookie then navigates to the supplied uri.</summary>
    public void RedirectToWithStatus(string uri, string message, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Cookies.Append(
            StatusCookieName,
            message,
            StatusCookieBuilder.Build(context));

        RedirectTo(uri);
    }

    /// <summary>Navigates back to the page the request is currently on.</summary>
    public void RedirectToCurrentPage() => RedirectTo(CurrentPath);

    /// <summary>Writes a status message cookie then reloads the current page.</summary>
    public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
        => RedirectToWithStatus(CurrentPath, message, context);

    /// <summary>Reports that the signed in principal has no matching account row.</summary>
    public void RedirectToInvalidUser(UserManager<ApplicationUser> userManager, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(userManager);

        RedirectToWithStatus(
            "Account/InvalidUser",
            $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.",
            context);
    }
}
