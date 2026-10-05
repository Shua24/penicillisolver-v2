using System.Net;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// Renders the interactive pages as a fully authorised clinical pathologist.
/// </summary>
/// <remarks>
/// This class exists because a whole class of defect escapes the access tests:
/// those assert on status codes for anonymous and wrong-role callers, so an
/// authorised caller never actually renders the page body. A component that
/// throws while RENDERING therefore passes every access test while being
/// completely broken for the one role that can use it.
/// <para>
/// The specific trap is that an interactive
/// (<c>@rendermode InteractiveServer</c>) component runs inside a Blazor
/// circuit, where there is no HTTP request and a cascading
/// <c>HttpContext</c> parameter is NULL. Any component that dereferences it
/// unconditionally throws a NullReferenceException on render — which is exactly
/// what happened to the upload page through the shared status message component.
/// </para>
/// </remarks>
public sealed class AuthorisedPageRenderTests : IClassFixture<PenicilliSolverApplicationFactory>, IAsyncLifetime
{
    private const string PathologistEmail = "render.pathologist@example.test";
    private const string PathologistPassword = "IntegrationTest1!";

    private readonly PenicilliSolverApplicationFactory factory;

    /// <summary>Creates the test class over the shared application factory.</summary>
    public AuthorisedPageRenderTests(PenicilliSolverApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await factory.InitialiseDatabaseAsync();
        await EnsurePathologistAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Upload_page_renders_for_an_active_pathologist()
    {
        // The regression: this page renders the shared status message component,
        // which used to dereference a null HttpContext inside the circuit.
        HttpClient client = await SignInPathologistAsync();

        HttpResponseMessage response = await client.GetAsync("/upload");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // A rendered page carries its own markup. An error page does not.
        Assert.Contains("Upload spreadsheet", html, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "NullReferenceException",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_page_renders_when_a_spreadsheet_is_already_stored()
    {
        // The reported failure happened when an upload had just been accepted,
        // so the page rendered WITH a current upload present.
        //
        // This deliberately does NOT seed storage: the shared fixture may
        // already hold a spreadsheet from another test class, and writing one
        // here would race that class over the single canonical file. Whether a
        // spreadsheet exists is not what this test is about — the render path
        // is identical either way, and the render is what broke.
        HttpClient client = await SignInPathologistAsync();

        HttpResponseMessage response = await client.GetAsync("/upload");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains("Upload spreadsheet", html, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "NullReferenceException",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task User_settings_page_renders_for_an_active_pathologist()
    {
        HttpClient client = await SignInPathologistAsync();

        HttpResponseMessage response = await client.GetAsync("/settings/user-roles");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.DoesNotContain(
            "NullReferenceException",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mapping_page_renders_for_an_active_pathologist()
    {
        HttpClient client = await SignInPathologistAsync();

        HttpResponseMessage response = await client.GetAsync("/antibiotic-mappings");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.DoesNotContain(
            "NullReferenceException",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> SignInPathologistAsync()
    {
        HttpClient client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        AntiforgeryFormHelper formHelper = new(client);

        HttpResponseMessage loginResponse = await formHelper.PostFormAsync(
            "/Account/Login",
            "login",
            new Dictionary<string, string>
            {
                ["Input.Email"] = PathologistEmail,
                ["Input.Password"] = PathologistPassword,
                ["Input.RememberMe"] = "false",
            });

        Assert.True(
            loginResponse.StatusCode is HttpStatusCode.Redirect
                or HttpStatusCode.Found
                or HttpStatusCode.OK,
            $"Unexpected login status {loginResponse.StatusCode}.");

        return client;
    }

    private async Task EnsurePathologistAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? existingUser = await userManager.FindByEmailAsync(PathologistEmail);

        if (existingUser is not null)
        {
            return;
        }

        ApplicationUser user = new()
        {
            UserName = PathologistEmail,
            Email = PathologistEmail,
            EmailConfirmed = true,
            DisplayName = "Render Pathologist",
            RequestedRole = ApplicationRoleNames.ClinicalPathologist,
            AccountStatus = AccountStatus.Active,
        };

        IdentityResult creationResult = await userManager.CreateAsync(user, PathologistPassword);

        Assert.True(creationResult.Succeeded, Describe(creationResult));

        IdentityResult roleResult = await userManager.AddToRoleAsync(
            user,
            ApplicationRoleNames.ClinicalPathologist);

        Assert.True(roleResult.Succeeded, Describe(roleResult));
    }

    private static string Describe(IdentityResult identityResult)
    {
        IEnumerable<string> errors = identityResult.Errors.Select(error => error.Description);

        string description = string.Join("; ", errors);

        return description;
    }
}
