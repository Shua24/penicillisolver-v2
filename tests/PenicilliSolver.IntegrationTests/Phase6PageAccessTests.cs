using System.Net;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// The end to end gate on the three new pages: the upload page, the
/// spreadsheet viewer and the user settings page, each reached anonymously and
/// by an active account that holds the wrong role.
/// </summary>
/// <remarks>
/// This file is about ACCESS only. What the viewer page renders once it is
/// reached lives in <see cref="SpreadsheetViewerPageTests"/>.
/// </remarks>
public sealed class Phase6PageAccessTests : IntegrationTestBase
{
    /// <summary>Creates the test class over the shared application factory.</summary>
    public Phase6PageAccessTests(PenicilliSolverApplicationFactory factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData("/upload")]
    [InlineData("/spreadsheet-viewer")]
    [InlineData("/spreadsheet")]
    [InlineData("/settings/user-roles")]
    public async Task Anonymous_request_to_a_new_page_is_not_served(string path)
    {
        HttpClient client = CreateAnonymousClient();

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Active_otherDoctor_cannot_reach_the_upload_page()
    {
        HttpClient client = await SignInOtherDoctorAsync();

        HttpResponseMessage response = await client.GetAsync("/upload");

        // The doctor may read, but the upload page needs the manage policy.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Active_otherDoctor_can_reach_the_viewer_page()
    {
        HttpClient client = await SignInOtherDoctorAsync();

        HttpResponseMessage response = await client.GetAsync("/spreadsheet-viewer");

        // Reading is permitted for every active role, so this page opens.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Active_otherDoctor_cannot_reach_the_user_settings_page()
    {
        HttpClient client = await SignInOtherDoctorAsync();

        HttpResponseMessage response = await client.GetAsync("/settings/user-roles");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_page_renders_the_organism_picker_when_a_spreadsheet_exists()
    {
        await SeedStoredSpreadsheetAsync();

        HttpClient client = await SignInOtherDoctorAsync();

        HttpResponseMessage response = await client.GetAsync("/spreadsheet-viewer");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"organism-name\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a client that carries no authentication cookie.
    /// </summary>
    private HttpClient CreateAnonymousClient()
    {
        HttpClient client = Factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });

        return client;
    }
}
