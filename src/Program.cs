using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

using System.Globalization;

using penicillisolver_v2.Components;
using penicillisolver_v2.Components.Account;
using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Resources;
using penicillisolver_v2.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Localisation. The marker type SharedResource ties the localizer to the two
// resource files under Resources/. ResourcesPath is left unset on purpose: the
// files already compile to the base name <RootNamespace>.Resources.SharedResource,
// and setting the path would make the localizer probe for a second "Resources"
// segment that does not exist, so every lookup would fall back to the bare key.
builder.Services.AddLocalization();

// Indonesian is the site default: an unconfigured browser reaches an Indonesian
// interface whatever language it asks for. The browser's Accept-Language header
// is deliberately NOT consulted — the audience is Indonesian laboratory staff,
// and a tool that opened in English because the browser was installed in
// English would greet most of them in the wrong language. English is reached
// only through the switcher, whose choice is stored in a cookie.
CultureInfo indonesianCulture = new(SupportedLanguages.Indonesian);
CultureInfo englishCulture = new(SupportedLanguages.English);

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(SupportedLanguages.Default);
    options.SupportedCultures = [indonesianCulture, englishCulture];
    options.SupportedUICultures = [indonesianCulture, englishCulture];

    // Only the cookie is consulted. The order matters when more than one
    // provider is present; with a single provider it decides on its own.
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider
        {
            CookieName = LanguagePreference.CookieName,
        },
    ];
});

builder.Services.AddCascadingAuthenticationState();

// The culture switcher reads the culture resolved for the current request from
// IRequestCultureFeature, which needs the accessor registered.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

bool isDevelopment = builder.Environment.IsDevelopment();
Action<DbContextOptionsBuilder> databaseProviderOptions =
    DatabaseProviderSelector.Create(builder.Configuration, isDevelopment);

builder.Services.AddDbContext<ApplicationDbContext>(databaseProviderOptions);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Adds the account status claim to every issued principal. This is what makes
// the pending gate work: a pending account carries a status claim that no
// authorization policy accepts.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddAuthorizationPolicies();

// The spreadsheet feature services. The storage service is scoped because it
// is cheap to construct and simply resolves a directory; the parsed document
// cache is a singleton so the parsed spreadsheet survives across requests and
// circuits and is only re-parsed when the upload's content hash changes.
builder.Services.AddSingleton<SpreadsheetDocumentCache>();
builder.Services.AddScoped<SpreadsheetStorageService>();
builder.Services.AddScoped<SpreadsheetUploadService>();
builder.Services.AddScoped<SpreadsheetQueryService>();
builder.Services.AddScoped<UserAdministrationService>();

// The antibiotic abbreviation mapping service. Scoped because it reads and
// writes through the scoped database context. It is the single, upload-scoped
// API for mappings and enforces the clinical pathologist gate server-side.
builder.Services.AddScoped<AntibioticAbbreviationService>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (isDevelopment)
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Applied before the endpoints so every request, including the SSR pass that
// produces a Blazor component's first HTML, resolves its culture from the
// cookie (or the browser header) rather than the host default.
app.UseRequestLocalization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Persists the language chosen in the navigation switcher. A dedicated minimal
// endpoint is used instead of a Blazor form so the cookie can be written with a
// real HTTP response and the page then hard-reloads under the new culture; an
// interactive circuit cannot change its own culture mid render.
app.MapPost("/culture/set", async (HttpContext httpContext) =>
{
    IFormCollection form = await httpContext.Request.ReadFormAsync();
    string? requestedCulture = form[LanguagePreference.FormFieldName];

    bool isSupported = requestedCulture is not null
        && SupportedLanguages.All.Contains(requestedCulture, StringComparer.Ordinal);

    string cultureToApply = isSupported ? requestedCulture! : SupportedLanguages.Default;

    httpContext.Response.Cookies.Append(
        LanguagePreference.CookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureToApply)),
        new CookieOptions
        {
            Path = "/",
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(365),
        });

    Uri requestOrigin = new($"{httpContext.Request.Scheme}://{httpContext.Request.Host}");

    string returnPath = httpContext.Request.Headers.Referer.ToString() switch
    {
        { Length: > 0 } referer when Uri.TryCreate(referer, UriKind.Absolute, out Uri? refererUri)
            && string.Equals(refererUri.Scheme, requestOrigin.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(refererUri.Host, requestOrigin.Host, StringComparison.OrdinalIgnoreCase)
            && refererUri.Port == requestOrigin.Port
            && RedirectHttpResult.IsLocalUrl(refererUri.PathAndQuery) =>
            refererUri.PathAndQuery,
        _ => "/",
    };

    return Results.LocalRedirect(returnPath);
});

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Create the schema and seed roles and seed accounts before serving traffic.
using (IServiceScope startupScope = app.Services.CreateScope())
{
    ApplicationDbContext database = startupScope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await database.Database.MigrateAsync();

    ILogger startupLogger = startupScope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    await IdentitySeeder.SeedAsync(app.Services, app.Configuration, startupLogger);
}

app.Run();

/// <summary>
/// Exposed so the integration test host (<c>WebApplicationFactory&lt;Program&gt;</c>)
/// can bind to the application entry point.
/// </summary>
public partial class Program
{
}
