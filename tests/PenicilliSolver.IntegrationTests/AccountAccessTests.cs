using System.Net;

using Microsoft.Extensions.DependencyInjection;

using penicillisolver_v2.Services;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// End to end checks of the authentication and authorization gate: anonymous
/// visitors are sent to the login page, and an account that has been registered
/// but not yet activated is denied access.
/// </summary>
public sealed class AccountAccessTests : IClassFixture<PenicilliSolverApplicationFactory>, IAsyncLifetime
{
    private readonly PenicilliSolverApplicationFactory factory;
    private HttpClient client = null!;

    /// <summary>Creates the test class over the shared application factory.</summary>
    public AccountAccessTests(PenicilliSolverApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await factory.InitialiseDatabaseAsync();

        client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>An anonymous request to the spreadsheet page is redirected to login.</summary>
    [Fact]
    public async Task Anonymous_spreadsheet_request_redirects_to_login()
    {
        HttpResponseMessage response = await client.GetAsync("/spreadsheet");
        string? location = response.Headers.Location?.ToString();

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(location);
        Assert.Contains("/Account/Login", location, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An anonymous request to the protected spreadsheet page never returns 200.</summary>
    [Fact]
    public async Task Anonymous_spreadsheet_request_is_not_served()
    {
        HttpResponseMessage response = await client.GetAsync("/spreadsheet");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The public home page is served to anonymous visitors.</summary>
    [Fact]
    public async Task Anonymous_home_request_is_served()
    {
        HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A freshly registered account is <c>Pending</c> and must be denied by the
    /// policy gate even though it can sign in.
    /// </summary>
    [Fact]
    public async Task Pending_account_is_denied_on_spreadsheet_page()
    {
        AuthenticatedSession session = await RegisterAndSignInAsync("pending.user@example.test");

        HttpResponseMessage response = await session.Client.GetAsync("/spreadsheet");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        string? location = response.Headers.Location?.ToString();
        if (location is not null)
        {
            Assert.Contains(
                "/Account/AccessDenied",
                location,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<AuthenticatedSession> RegisterAndSignInAsync(string email)
    {
        HttpClient registrationClient = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        AntiforgeryFormHelper formHelper = new(registrationClient);

        HttpResponseMessage registerResponse = await formHelper.PostFormAsync(
            "/Account/Register",
            "register",
            new Dictionary<string, string>
            {
                ["Input.DisplayName"] = "Pending Test User",
                ["Input.Email"] = email,
                ["Input.RequestedRole"] = "OtherDoctor",
                ["Input.Password"] = "IntegrationTest1!",
                ["Input.ConfirmPassword"] = "IntegrationTest1!",
            });

        // A successful registration redirects away with a 302.
        Assert.True(
            registerResponse.StatusCode is HttpStatusCode.Redirect
                or HttpStatusCode.Found
                or HttpStatusCode.OK,
            $"Unexpected registration status {registerResponse.StatusCode}.");

        HttpResponseMessage loginResponse = await formHelper.PostFormAsync(
            "/Account/Login",
            "login",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "IntegrationTest1!",
                ["Input.RememberMe"] = "false",
            });

        Assert.True(
            loginResponse.StatusCode is HttpStatusCode.Redirect
                or HttpStatusCode.Found
                or HttpStatusCode.OK,
            $"Unexpected login status {loginResponse.StatusCode}.");

        return new AuthenticatedSession(registrationClient);
    }

    /// <summary>An HTTP client carrying the authentication cookies of one account.</summary>
    private sealed record AuthenticatedSession(HttpClient Client);
}
