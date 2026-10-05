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
/// The end to end gate on the three new pages: the upload page, the
/// spreadsheet viewer and the user settings page, each reached anonymously and
/// by an active account that holds the wrong role.
/// </summary>
public sealed class Phase6PageAccessTests : IClassFixture<PenicilliSolverApplicationFactory>, IAsyncLifetime
{
    private const string OtherDoctorEmail = "phase6.otherdoctor@example.test";
    private const string OtherDoctorPassword = "IntegrationTest1!";

    private readonly PenicilliSolverApplicationFactory factory;

    /// <summary>Creates the test class over the shared application factory.</summary>
    public Phase6PageAccessTests(PenicilliSolverApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await factory.InitialiseDatabaseAsync();
        await EnsureOtherDoctorAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/upload")]
    [InlineData("/spreadsheet-viewer")]
    [InlineData("/spreadsheet")]
    [InlineData("/settings/user-roles")]
    public async Task Anonymous_request_to_a_new_page_is_not_served(string path)
    {
        HttpClient client = CreateClient(handleCookies: false);

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
    public async Task Viewer_page_renders_the_top_count_control_when_a_spreadsheet_exists()
    {
        await SeedStoredSpreadsheetAsync();

        HttpClient client = await SignInOtherDoctorAsync();

        HttpResponseMessage response = await client.GetAsync("/spreadsheet-viewer");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"top-count\"", html, StringComparison.Ordinal);
        Assert.Contains("How many of the most resistant antibiotics", html, StringComparison.Ordinal);
    }

    private async Task SeedStoredSpreadsheetAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        ApplicationDbContext database =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool alreadySeeded = await database.SpreadsheetUploads.AnyAsync();

        if (alreadySeeded)
        {
            return;
        }

        SpreadsheetStorageService storageService =
            scope.ServiceProvider.GetRequiredService<SpreadsheetStorageService>();

        string csvText =
            "Organism,Organism one,Organism two\n" +
            "Cefetamet,0,0\n" +
            "Cefixime,1,1\n" +
            "Ceftibuten,2,2\n" +
            "Amoxicillin,90,90\n";

        byte[] csvBytes = System.Text.Encoding.UTF8.GetBytes(csvText);

        using MemoryStream content = new(csvBytes);

        SpreadsheetStorageResult storageResult = await storageService.SaveCurrentSpreadsheetAsync(
            content,
            SpreadsheetFileFormat.Csv);

        SpreadsheetUpload upload = new()
        {
            OriginalFileName = "phase6.csv",
            StoredFilePath = storageResult.RelativeStoredFilePath,
            ContentHash = storageResult.ContentHash,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = "seed",
            FileFormat = SpreadsheetFileFormat.Csv,
            Orientation = SpreadsheetOrientation.AntibioticsAsRows,
            OrganismCount = 2,
            AntibioticCount = 4,
        };

        database.SpreadsheetUploads.Add(upload);

        await database.SaveChangesAsync();
    }

    private HttpClient CreateClient(bool handleCookies)
    {
        HttpClient client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies,
        });

        return client;
    }

    private async Task<HttpClient> SignInOtherDoctorAsync()
    {
        HttpClient client = CreateClient(handleCookies: true);

        AntiforgeryFormHelper formHelper = new(client);

        HttpResponseMessage loginResponse = await formHelper.PostFormAsync(
            "/Account/Login",
            "login",
            new Dictionary<string, string>
            {
                ["Input.Email"] = OtherDoctorEmail,
                ["Input.Password"] = OtherDoctorPassword,
                ["Input.RememberMe"] = "false",
            });

        Assert.True(
            loginResponse.StatusCode is HttpStatusCode.Redirect
                or HttpStatusCode.Found
                or HttpStatusCode.OK,
            $"Unexpected login status {loginResponse.StatusCode}.");

        return client;
    }

    private async Task EnsureOtherDoctorAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? existingUser = await userManager.FindByEmailAsync(OtherDoctorEmail);

        if (existingUser is not null)
        {
            return;
        }

        ApplicationUser user = new()
        {
            UserName = OtherDoctorEmail,
            Email = OtherDoctorEmail,
            EmailConfirmed = true,
            DisplayName = "Phase 6 Other Doctor",
            RequestedRole = ApplicationRoleNames.OtherDoctor,
            AccountStatus = AccountStatus.Active,
        };

        IdentityResult creationResult = await userManager.CreateAsync(user, OtherDoctorPassword);

        Assert.True(creationResult.Succeeded, Describe(creationResult));

        IdentityResult roleResult = await userManager.AddToRoleAsync(
            user,
            ApplicationRoleNames.OtherDoctor);

        Assert.True(roleResult.Succeeded, Describe(roleResult));
    }

    private static string Describe(IdentityResult identityResult)
    {
        IEnumerable<string> errors = identityResult.Errors.Select(error => error.Description);

        string description = string.Join("; ", errors);

        return description;
    }
}
