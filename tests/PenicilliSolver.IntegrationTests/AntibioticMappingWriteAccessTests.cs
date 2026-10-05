using System.Net;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.Services;

using Xunit;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// End to end checks of the write gate on antibiotic abbreviation mappings over
/// real HTTP. The test-only endpoint calls the real service with the request's
/// authenticated principal, so this exercises the cookie authentication, the
/// <c>CanManageAntibioticMappings</c> policy, and the service's own server-side
/// check together: a non-pathologist is refused and a pathologist is allowed.
/// </summary>
public sealed class AntibioticMappingWriteAccessTests
    : IClassFixture<AntibioticMappingApplicationFactory>, IAsyncLifetime
{
    private const string Password = "IntegrationTest1!";

    private readonly AntibioticMappingApplicationFactory factory;

    /// <summary>Creates the test class over the shared application factory.</summary>
    public AntibioticMappingWriteAccessTests(AntibioticMappingApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await factory.InitialiseDatabaseAsync();

        using IServiceScope scope = factory.Services.CreateScope();

        await SeedRolesAsync(scope);
        await SeedActiveUserAsync(scope, "pathologist@example.test", ApplicationRoleNames.ClinicalPathologist);
        await SeedActiveUserAsync(scope, "otherdoctor@example.test", ApplicationRoleNames.OtherDoctor);
        await SeedUploadAsync(scope);
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OtherDoctor_PostingAMappingCreate_IsRefused()
    {
        HttpClient client = await SignInAsync("otherdoctor@example.test");

        HttpResponseMessage response = await client.PostAsync(
            "/test/antibiotic-mappings",
            content: null);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClinicalPathologist_PostingAMappingCreate_Succeeds()
    {
        HttpClient client = await SignInAsync("pathologist@example.test");

        HttpResponseMessage response = await client.PostAsync(
            "/test/antibiotic-mappings",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        HttpClient client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        AntiforgeryFormHelper formHelper = new(client);

        HttpResponseMessage response = await formHelper.PostFormAsync(
            "/Account/Login",
            "login",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = Password,
                ["Input.RememberMe"] = "false",
            });

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect
                or HttpStatusCode.Found
                or HttpStatusCode.OK,
            $"Unexpected login status {response.StatusCode}.");

        return client;
    }

    private static async Task SeedRolesAsync(IServiceScope scope)
    {
        RoleManager<IdentityRole> roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roleNames =
        [
            ApplicationRoleNames.ClinicalPathologist,
            ApplicationRoleNames.OtherDoctor,
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
        ];

        foreach (string roleName in roleNames)
        {
            bool exists = await roleManager.RoleExistsAsync(roleName);

            if (!exists)
            {
                IdentityRole role = new(roleName);
                await roleManager.CreateAsync(role);
            }
        }
    }

    private static async Task SeedActiveUserAsync(IServiceScope scope, string email, string roleName)
    {
        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return;
        }

        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = email,
            RequestedRole = roleName,
            AccountStatus = AccountStatus.Active,
        };

        IdentityResult creationResult = await userManager.CreateAsync(user, Password);
        Assert.True(creationResult.Succeeded);

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, roleName);
        Assert.True(roleResult.Succeeded);
    }

    private async Task SeedUploadAsync(IServiceScope scope)
    {
        ApplicationDbContext database =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool alreadySeeded = await database.SpreadsheetUploads.AnyAsync();

        if (alreadySeeded)
        {
            return;
        }

        SpreadsheetUpload upload = new()
        {
            OriginalFileName = "integration.csv",
            StoredFilePath = "integration.csv",
            ContentHash = $"hash-{Guid.NewGuid():N}",
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = "seed",
            FileFormat = SpreadsheetFileFormat.Csv,
            Orientation = SpreadsheetOrientation.AntibioticsAsRows,
            OrganismCount = 1,
            AntibioticCount = 1,
        };

        database.SpreadsheetUploads.Add(upload);

        int writtenRowCount = await database.SaveChangesAsync();
        Assert.Equal(1, writtenRowCount);
    }
}
