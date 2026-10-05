using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Components;
using penicillisolver_v2.Components.Account;
using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

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
