using System.Security.Claims;

using penicillisolver_v2.Services;

namespace penicillisolver_v2.Components.Account;

/// <summary>
/// Sends an account that must change its password to the change page, whatever
/// page it asked for.
/// </summary>
/// <remarks>
/// <para>
/// This is the server-side half of the gate. The layout redirect covers an
/// interactive circuit, and this covers a plain HTTP request that never reaches
/// a component. Both are needed: a circuit can be opened on a page and then sit
/// there, and a request for a static file never renders a component at all.
/// </para>
/// <para>
/// <b>The exemption list is not optional.</b> The change page is a normal page:
/// it needs its stylesheet, the Blazor framework script and the circuit
/// endpoint. Redirecting those leaves the user on an unstyled page whose
/// buttons do nothing, which looks like a broken application rather than a
/// gate. Verified the hard way: without the exemptions, <c>/app.css</c> returned
/// the change page's HTML and <c>/_framework/blazor.web.js</c> returned 500.
/// </para>
/// </remarks>
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    /// <summary>The path the gate lets through.</summary>
    private const string ChangePasswordPath = "/Account/ChangePassword";

    /// <summary>The sign-out path, so a stuck user can always leave.</summary>
    private const string LogoutPath = "/Account/Logout";

    /// <summary>
    /// Path prefixes the gate must never touch: the Blazor framework and circuit
    /// endpoints, the static asset roots, and the culture switcher.
    /// </summary>
    private static readonly string[] ExemptPathPrefixes =
    [
        "/_framework",
        "/_blazor",
        "/_content",
        "/lib",
        "/css",
        "/js",
        "/culture",
        "/favicon",
    ];

    /// <summary>Runs the gate, then the rest of the pipeline.</summary>
    /// <param name="context">The request being handled.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        if (isAuthenticated && MustChangePassword(context.User))
        {
            PathString path = context.Request.Path;

            bool isAllowedPath = path.StartsWithSegments(
                    ChangePasswordPath,
                    StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments(LogoutPath, StringComparison.OrdinalIgnoreCase)
                || IsExemptPath(path);

            if (!isAllowedPath)
            {
                context.Response.Redirect(ChangePasswordPath);
                return;
            }
        }

        await next(context);
    }

    /// <summary>
    /// Reports whether the principal carries the must-change claim.
    /// </summary>
    /// <param name="user">The principal to inspect.</param>
    /// <returns>True when the account must choose a new password.</returns>
    private static bool MustChangePassword(ClaimsPrincipal user)
    {
        Claim? mustChangeClaim = user.FindFirst(ApplicationClaimTypes.MustChangePassword);

        bool mustChange = mustChangeClaim is not null
            && string.Equals(mustChangeClaim.Value, "true", StringComparison.Ordinal);

        return mustChange;
    }

    /// <summary>
    /// Reports whether a path is a static asset or framework path the gate must
    /// let through untouched.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>True when the path must not be redirected.</returns>
    private static bool IsExemptPath(PathString path)
    {
        foreach (string prefix in ExemptPathPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Anything with a file extension is a static asset: app.css, favicon.png,
        // the generated styles bundle, and so on.
        bool hasFileExtension = Path.HasExtension(path.Value ?? string.Empty);

        return hasFileExtension;
    }
}
