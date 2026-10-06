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
/// Guards the invariant the seeder enforces on every startup: at least one
/// active clinical pathologist always exists, whichever seed configuration
/// the host started with.
/// </summary>
public class ClinicalPathologistBootstrapTests
{
    private const string BootstrapPathologistEmail = "bootstrap.clinicalpathologist@localhost.invalid";

    /// <summary>Asserts the invariant on the development test host.</summary>
    public sealed class WithSeedConfiguration : IClassFixture<PenicilliSolverApplicationFactory>
    {
        private readonly PenicilliSolverApplicationFactory factory;

        public WithSeedConfiguration(PenicilliSolverApplicationFactory factory)
        {
            ArgumentNullException.ThrowIfNull(factory);

            this.factory = factory;
        }

        [Fact]
        public async Task After_startup_an_active_clinical_pathologist_exists()
        {
            await factory.InitialiseDatabaseAsync();

            int activePathologistCount = await CountActivePathologistsAsync(factory);

            Assert.True(activePathologistCount >= 1);
        }

        [Fact]
        public async Task A_second_seeding_pass_does_not_add_another_bootstrap_account()
        {
            await factory.InitialiseDatabaseAsync();

            int activePathologistCountBefore = await CountActivePathologistsAsync(factory);

            await RunSeederAsync(factory);

            int activePathologistCountAfter = await CountActivePathologistsAsync(factory);

            Assert.Equal(activePathologistCountBefore, activePathologistCountAfter);
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

    /// <summary>Asserts the pure bootstrap branch on a host with no seed config.</summary>
    public sealed class WithoutSeedConfiguration : IClassFixture<NoSeedAccountsApplicationFactory>
    {
        private readonly NoSeedAccountsApplicationFactory factory;

        public WithoutSeedConfiguration(NoSeedAccountsApplicationFactory factory)
        {
            ArgumentNullException.ThrowIfNull(factory);

            this.factory = factory;
        }

        [Fact]
        public async Task Startup_creates_the_bootstrap_clinical_pathologist()
        {
            await factory.InitialiseDatabaseAsync();

            ApplicationUser? pathologist =
                await FindUserByEmailAsync(factory, BootstrapPathologistEmail);

            Assert.NotNull(pathologist);
            Assert.Equal(AccountStatus.Active, pathologist!.AccountStatus);

            bool isPathologistRole = await IsInRoleAsync(
                factory,
                pathologist,
                ApplicationRoleNames.ClinicalPathologist);

            Assert.True(isPathologistRole);
        }

        [Fact]
        public async Task A_second_seeding_pass_does_not_stack_a_duplicate_bootstrap_account()
        {
            await factory.InitialiseDatabaseAsync();

            int bootstrapUserCountBefore = await CountBootstrapUsersAsync(factory);

            await RunSeederAsync(factory);

            int bootstrapUserCountAfter = await CountBootstrapUsersAsync(factory);

            Assert.Equal(bootstrapUserCountBefore, bootstrapUserCountAfter);
        }

        [Fact]
        public async Task Disabling_every_pathologist_is_repaired_on_the_next_seeding_pass()
        {
            await factory.InitialiseDatabaseAsync();

            using IServiceScope disableScope = factory.Services.CreateScope();

            UserManager<ApplicationUser> userManager =
                disableScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            IList<ApplicationUser> pathologists =
                await userManager.GetUsersInRoleAsync(ApplicationRoleNames.ClinicalPathologist);

            Assert.NotEmpty(pathologists);

            foreach (ApplicationUser pathologist in pathologists)
            {
                pathologist.AccountStatus = AccountStatus.Disabled;

                IdentityResult disableResult = await userManager.UpdateAsync(pathologist);

                Assert.True(disableResult.Succeeded);
            }

            await RunSeederAsync(factory);

            int activePathologistCount = await CountActivePathologistsAsync(factory);

            Assert.True(activePathologistCount >= 1);
        }
    }

    /// <summary>Runs one full seeding pass over the host's services.</summary>
    private static async Task RunSeederAsync(PenicilliSolverApplicationFactory factory)
    {
        IConfiguration configuration =
            factory.Services.GetRequiredService<IConfiguration>();
        ILogger seederLogger =
            factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test");

        await IdentitySeeder.SeedAsync(factory.Services, configuration, seederLogger);
    }

    private static async Task<int> CountActivePathologistsAsync(PenicilliSolverApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        IList<ApplicationUser> pathologists =
            await userManager.GetUsersInRoleAsync(ApplicationRoleNames.ClinicalPathologist);

        int activePathologistCount = pathologists.Count(user =>
            user.AccountStatus == AccountStatus.Active);

        return activePathologistCount;
    }

    private static async Task<int> CountBootstrapUsersAsync(NoSeedAccountsApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        IList<ApplicationUser> pathologists =
            await userManager.GetUsersInRoleAsync(ApplicationRoleNames.ClinicalPathologist);

        int bootstrapUserCount = pathologists.Count(user =>
            user.Email == BootstrapPathologistEmail);

        return bootstrapUserCount;
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
