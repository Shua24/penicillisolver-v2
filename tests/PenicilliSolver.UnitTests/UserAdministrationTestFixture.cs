using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Boots a real <see cref="UserManager{TUser}"/> over an in-memory database and
/// exposes the small helpers the administration tests share. Keeping the wiring
/// here lets the test class itself read as a list of behaviours.
/// </summary>
public abstract class UserAdministrationTestFixture : IAsyncLifetime
{
    private ServiceProvider serviceProvider = null!;

    /// <summary>The database the service under test writes to.</summary>
    protected ApplicationDbContext Database { get; private set; } = null!;

    /// <summary>The identity user manager the service under test uses.</summary>
    protected UserManager<ApplicationUser> UserManager { get; private set; } = null!;

    /// <summary>The service under test.</summary>
    protected UserAdministrationService AdministrationService { get; private set; } = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        string databaseName = $"user-administration-{Guid.NewGuid():N}";

        ServiceCollection services = new();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 1;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        // The service under test now resolves its user facing messages through
        // a localizer, so the container has to provide the localization
        // services even though no message is asserted here.
        services.AddLocalization();

        serviceProvider = services.BuildServiceProvider();

        Database = serviceProvider.GetRequiredService<ApplicationDbContext>();

        UserManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        IStringLocalizerFactory localizerFactory =
            serviceProvider.GetRequiredService<IStringLocalizerFactory>();

        AdministrationService = new UserAdministrationService(
            Database,
            UserManager,
            localizerFactory);

        await SeedRolesAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await serviceProvider.DisposeAsync();
    }

    /// <summary>Creates an account holding the clinical pathologist role.</summary>
    protected async Task<ApplicationUser> CreatePathologistAsync(
        string email,
        AccountStatus accountStatus)
    {
        ApplicationUser user = await CreateUserAsync(
            email,
            ApplicationRoleNames.ClinicalPathologist,
            accountStatus);

        return user;
    }

    /// <summary>Creates an account holding the supplied role and status.</summary>
    protected async Task<ApplicationUser> CreateUserAsync(
        string email,
        string roleName,
        AccountStatus accountStatus)
    {
        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = $"Test account {email}",
            RequestedRole = roleName,
            AccountStatus = accountStatus,
        };

        IdentityResult creationResult = await UserManager.CreateAsync(user, "Password1!");

        Assert.True(creationResult.Succeeded, Describe(creationResult));

        IdentityResult roleResult = await UserManager.AddToRoleAsync(user, roleName);

        Assert.True(roleResult.Succeeded, Describe(roleResult));

        return user;
    }

    private async Task SeedRolesAsync()
    {
        RoleManager<IdentityRole> roleManager =
            serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roleNames =
        [
            ApplicationRoleNames.ClinicalPathologist,
            ApplicationRoleNames.OtherDoctor,
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
        ];

        foreach (string roleName in roleNames)
        {
            bool roleExists = await roleManager.RoleExistsAsync(roleName);

            if (!roleExists)
            {
                IdentityResult creationResult = await roleManager.CreateAsync(new IdentityRole(roleName));

                Assert.True(creationResult.Succeeded, Describe(creationResult));
            }
        }
    }

    private static string Describe(IdentityResult identityResult)
    {
        IEnumerable<string> errors = identityResult.Errors.Select(error => error.Description);

        string description = string.Join("; ", errors);

        return description;
    }
}
