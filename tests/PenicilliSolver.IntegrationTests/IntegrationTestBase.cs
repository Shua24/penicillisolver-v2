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
/// Shared plumbing for integration tests over the running application: a known
/// signed-in doctor, and one uploaded spreadsheet in the current storage.
/// </summary>
/// <remarks>
/// The seeding goes through <see cref="SpreadsheetStorageService"/> rather than
/// writing files by hand, so the tests exercise the same path the upload page
/// does. The factory has already pointed that service at a private directory —
/// see the remarks on <see cref="PenicilliSolverApplicationFactory"/> for why
/// writing to the application's own storage would be data loss.
/// </remarks>
public abstract class IntegrationTestBase : IClassFixture<PenicilliSolverApplicationFactory>, IAsyncLifetime
{
    /// <summary>The doctor every test signs in as.</summary>
    protected const string OtherDoctorEmail = "phase6.otherdoctor@example.test";

    /// <summary>The doctor's password.</summary>
    protected const string OtherDoctorPassword = "IntegrationTest1!";

    /// <summary>The factory the derived test class is constructed with.</summary>
    protected PenicilliSolverApplicationFactory Factory { get; }

    /// <summary>Creates the base over the shared application factory.</summary>
    protected IntegrationTestBase(PenicilliSolverApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        Factory = factory;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await Factory.InitialiseDatabaseAsync();
        await EnsureOtherDoctorAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Signs in as the seeded other-doctor account, returning the client that
    /// now carries its authentication cookie.
    /// </summary>
    protected async Task<HttpClient> SignInOtherDoctorAsync(bool handleCookies = true)
    {
        HttpClient client = Factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies,
        });

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

    /// <summary>
    /// Stores one small spreadsheet as the current upload, once per test run.
    /// </summary>
    /// <remarks>
    /// The file names two organisms, "Organism one" and "Organism two", and four
    /// antibiotics. Two of those antibiotics are measured at 0 and two at
    /// higher values, which is enough to assert both the resistance order and
    /// the measured-versus-untested rule.
    /// </remarks>
    protected async Task SeedStoredSpreadsheetAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();

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

    /// <summary>Resolves a service from the running application's container.</summary>
    protected TService ResolveService<TService>()
        where TService : notnull
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        TService service = scope.ServiceProvider.GetRequiredService<TService>();

        return service;
    }

    private async Task EnsureOtherDoctorAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();

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

        return string.Join("; ", errors);
    }
}
