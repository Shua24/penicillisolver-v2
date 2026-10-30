using System.Net;
using System.Net.Http;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;

namespace PenicilliSolver.IntegrationTests;

/// <summary>
/// Guards the seeding contract: a configured seed account becomes an active
/// account holding its role, and a host that ends up with no active clinical
/// pathologist refuses to continue instead of starting in a locked-out state.
/// </summary>
public class IdentitySeedAccountTests
{
    /// <summary>Asserts the behaviour of a host with a configured seed.</summary>
    public sealed class WithSeedConfiguration : IClassFixture<PenicilliSolverApplicationFactory>
    {
        private readonly PenicilliSolverApplicationFactory factory;

        public WithSeedConfiguration(PenicilliSolverApplicationFactory factory)
        {
            ArgumentNullException.ThrowIfNull(factory);

            this.factory = factory;
        }

        [Fact]
        public async Task The_configured_seed_account_is_created_active_with_its_role()
        {
            await factory.InitialiseDatabaseAsync();

            ApplicationUser? pathologist = await FindUserByEmailAsync(
                factory,
                PenicilliSolverApplicationFactory.SeedPathologistEmail);

            Assert.NotNull(pathologist);
            Assert.Equal(AccountStatus.Active, pathologist!.AccountStatus);

            bool isPathologistRole = await IsInRoleAsync(
                factory,
                pathologist,
                ApplicationRoleNames.ClinicalPathologist);

            Assert.True(isPathologistRole);
        }

        [Fact]
        public async Task A_second_seeding_pass_does_not_add_another_pathologist()
        {
            await factory.InitialiseDatabaseAsync();

            int pathologistCountBefore = await CountPathologistsAsync(factory);

            await RunSeederAsync(factory, factory.Services.GetRequiredService<IConfiguration>());

            int pathologistCountAfter = await CountPathologistsAsync(factory);

            Assert.Equal(pathologistCountBefore, pathologistCountAfter);
        }

        [Fact]
        public async Task The_registration_form_offers_the_clinical_pathologist_role()
        {
            await factory.InitialiseDatabaseAsync();

            HttpResponseMessage registerResponse = await factory.CreateClient(new()
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
            }).GetAsync("/Account/Register");

            string registerPage = await registerResponse.Content.ReadAsStringAsync();

            Assert.Contains("Clinical Pathologist", registerPage);
        }

        [Fact]
        public async Task A_pending_clinical_pathologist_registration_is_denied_the_settings_page()
        {
            await factory.InitialiseDatabaseAsync();

            HttpClient registerClient = factory.CreateClient(new()
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
            });

            AntiforgeryFormHelper formHelper = new(registerClient);

            string registrationEmail = $"pending.pathologist.{Guid.NewGuid():N}@example.test";

            HttpResponseMessage registerResponse = await formHelper.PostFormAsync(
                "/Account/Register",
                "register",
                new Dictionary<string, string>
                {
                    ["Input.DisplayName"] = "Pending Pathologist Registrant",
                    ["Input.Email"] = registrationEmail,
                    ["Input.RequestedRole"] = ApplicationRoleNames.ClinicalPathologist,
                    ["Input.Password"] = "IntegrationTest1!",
                    ["Input.ConfirmPassword"] = "IntegrationTest1!",
                });

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
                    ["Input.Email"] = registrationEmail,
                    ["Input.Password"] = "IntegrationTest1!",
                    ["Input.RememberMe"] = "false",
                });

            Assert.True(
                loginResponse.StatusCode is HttpStatusCode.Redirect
                    or HttpStatusCode.Found
                    or HttpStatusCode.OK,
                $"Unexpected login status {loginResponse.StatusCode}.");

            HttpResponseMessage settingsResponse =
                await registerClient.GetAsync("/settings/user-roles");

            Assert.NotEqual(HttpStatusCode.OK, settingsResponse.StatusCode);

            ApplicationUser? registrant = await FindUserByEmailAsync(factory, registrationEmail);

            Assert.Equal(AccountStatus.Pending, registrant!.AccountStatus);
            Assert.Equal(
                ApplicationRoleNames.ClinicalPathologist,
                registrant.RequestedRole);
        }
    }

    /// <summary>Asserts the startup refusal when no active pathologist survives.</summary>
    public sealed class WithoutConfiguredSeed : IClassFixture<PenicilliSolverApplicationFactory>
    {
        private readonly PenicilliSolverApplicationFactory factory;

        public WithoutConfiguredSeed(PenicilliSolverApplicationFactory factory)
        {
            ArgumentNullException.ThrowIfNull(factory);

            this.factory = factory;
        }

        [Fact]
        public async Task Seeding_throws_when_no_active_clinical_pathologist_exists()
        {
            await factory.InitialiseDatabaseAsync();

            await DisableEveryPathologistAsync(factory);

            IConfiguration configurationWithoutSeeds = new ConfigurationBuilder().Build();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RunSeederAsync(factory, configurationWithoutSeeds));
        }

        private static async Task DisableEveryPathologistAsync(
            PenicilliSolverApplicationFactory factory)
        {
            using IServiceScope scope = factory.Services.CreateScope();

            UserManager<ApplicationUser> userManager =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            IList<ApplicationUser> pathologists =
                await userManager.GetUsersInRoleAsync(ApplicationRoleNames.ClinicalPathologist);

            Assert.NotEmpty(pathologists);

            foreach (ApplicationUser pathologist in pathologists)
            {
                pathologist.AccountStatus = AccountStatus.Disabled;

                IdentityResult disableResult = await userManager.UpdateAsync(pathologist);

                Assert.True(disableResult.Succeeded);
            }
        }
    }

    /// <summary>Runs one full seeding pass over the host's services.</summary>
    private static async Task RunSeederAsync(
        PenicilliSolverApplicationFactory factory,
        IConfiguration configuration)
    {
        ILogger seederLogger =
            factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test");

        await IdentitySeeder.SeedAsync(factory.Services, configuration, seederLogger);
    }

    private static async Task<int> CountPathologistsAsync(
        PenicilliSolverApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        IList<ApplicationUser> pathologists =
            await userManager.GetUsersInRoleAsync(ApplicationRoleNames.ClinicalPathologist);

        int pathologistCount = pathologists.Count;

        return pathologistCount;
    }

    private static async Task<ApplicationUser?> FindUserByEmailAsync(
        PenicilliSolverApplicationFactory factory,
        string email)
    {
        using IServiceScope scope = factory.Services.CreateScope();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByEmailAsync(email);

        return user;
    }

    private static async Task<bool> IsInRoleAsync(
        PenicilliSolverApplicationFactory factory,
        ApplicationUser user,
        string roleName)
    {
        using IServiceScope scope = factory.Services.CreateScope();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        bool isInRole = await userManager.IsInRoleAsync(user, roleName);

        return isInRole;
    }
}
