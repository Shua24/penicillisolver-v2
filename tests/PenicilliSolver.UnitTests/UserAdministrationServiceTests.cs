using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Checks the last-active-pathologist guard on
/// <see cref="UserAdministrationService"/>. The guard is the only thing standing
/// between a mis-click and a permanently locked-out application, so it is
/// exercised against a real <see cref="UserManager{TUser}"/> over an in-memory
/// database rather than a stub.
/// </summary>
public sealed class UserAdministrationServiceTests : IAsyncLifetime
{
    private ServiceProvider serviceProvider = null!;
    private ApplicationDbContext database = null!;
    private UserManager<ApplicationUser> userManager = null!;
    private UserAdministrationService administrationService = null!;

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

        serviceProvider = services.BuildServiceProvider();

        database = serviceProvider.GetRequiredService<ApplicationDbContext>();

        userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        administrationService = new UserAdministrationService(database, userManager);

        await SeedRolesAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Cannot_demote_the_last_active_pathologist()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "sole.pathologist@example.test",
            AccountStatus.Active);

        AdministrationResult result = await administrationService.SetRoleAsync(
            actingPathologist.Id,
            actingPathologist.Id,
            ApplicationRoleNames.OtherDoctor);

        Assert.False(result.IsSuccess);

        // The role must be unchanged: the refusal happens before any write.
        bool stillPathologist = await userManager.IsInRoleAsync(
            actingPathologist,
            ApplicationRoleNames.ClinicalPathologist);

        Assert.True(stillPathologist);
    }

    [Fact]
    public async Task Cannot_disable_the_last_active_pathologist()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "sole.pathologist@example.test",
            AccountStatus.Active);

        AdministrationResult result = await administrationService.SetAccountStatusAsync(
            actingPathologist.Id,
            actingPathologist.Id,
            AccountStatus.Disabled);

        Assert.False(result.IsSuccess);

        ApplicationUser survivor = await userManager.FindByIdAsync(actingPathologist.Id)
            ?? throw new InvalidOperationException("The account disappeared.");

        Assert.Equal(AccountStatus.Active, survivor.AccountStatus);
    }

    [Fact]
    public async Task Can_demote_a_pathologist_when_another_active_pathologist_exists()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "acting.pathologist@example.test",
            AccountStatus.Active);

        ApplicationUser secondPathologist = await CreatePathologistAsync(
            "second.pathologist@example.test",
            AccountStatus.Active);

        AdministrationResult result = await administrationService.SetRoleAsync(
            actingPathologist.Id,
            secondPathologist.Id,
            ApplicationRoleNames.OtherDoctor);

        Assert.True(result.IsSuccess);

        bool stillPathologist = await userManager.IsInRoleAsync(
            secondPathologist,
            ApplicationRoleNames.ClinicalPathologist);

        Assert.False(stillPathologist);

        bool nowOtherDoctor = await userManager.IsInRoleAsync(
            secondPathologist,
            ApplicationRoleNames.OtherDoctor);

        Assert.True(nowOtherDoctor);
    }

    [Fact]
    public async Task Can_disable_a_pathologist_when_another_active_pathologist_exists()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "acting.pathologist@example.test",
            AccountStatus.Active);

        ApplicationUser secondPathologist = await CreatePathologistAsync(
            "second.pathologist@example.test",
            AccountStatus.Active);

        AdministrationResult result = await administrationService.SetAccountStatusAsync(
            actingPathologist.Id,
            secondPathologist.Id,
            AccountStatus.Disabled);

        Assert.True(result.IsSuccess);

        ApplicationUser disabledSurvivor =
            await userManager.FindByIdAsync(secondPathologist.Id)
                ?? throw new InvalidOperationException("The account disappeared.");

        Assert.Equal(AccountStatus.Disabled, disabledSurvivor.AccountStatus);
    }

    [Fact]
    public async Task A_disabled_pathologist_does_not_count_as_an_active_survivor()
    {
        // Two pathologists exist, but one is already disabled, so the active one
        // is effectively the last. The guard must refuse.
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "acting.pathologist@example.test",
            AccountStatus.Active);

        await CreatePathologistAsync(
            "dormant.pathologist@example.test",
            AccountStatus.Disabled);

        AdministrationResult result = await administrationService.SetAccountStatusAsync(
            actingPathologist.Id,
            actingPathologist.Id,
            AccountStatus.Disabled);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task A_non_pathologist_caller_cannot_change_anything()
    {
        ApplicationUser otherDoctor = await CreateUserAsync(
            "other.doctor@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active);

        ApplicationUser target = await CreateUserAsync(
            "target.user@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Pending);

        AdministrationResult result = await administrationService.SetAccountStatusAsync(
            otherDoctor.Id,
            target.Id,
            AccountStatus.Active);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Activating_a_pending_account_persists_the_status()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "acting.pathologist@example.test",
            AccountStatus.Active);

        ApplicationUser pendingUser = await CreateUserAsync(
            "pending.user@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Pending);

        AdministrationResult result = await administrationService.SetAccountStatusAsync(
            actingPathologist.Id,
            pendingUser.Id,
            AccountStatus.Active);

        Assert.True(result.IsSuccess);

        ApplicationUser activatedUser = await userManager.FindByIdAsync(pendingUser.Id)
            ?? throw new InvalidOperationException("The account disappeared.");

        Assert.Equal(AccountStatus.Active, activatedUser.AccountStatus);
    }

    [Fact]
    public async Task Setting_team_permissions_persists_both_flags()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "acting.pathologist@example.test",
            AccountStatus.Active);

        AdministrationResult result = await administrationService.SetTeamPermissionsAsync(
            actingPathologist.Id,
            canUpdateSpreadsheet: true,
            canDeleteSpreadsheet: false);

        Assert.True(result.IsSuccess);

        TeamPermissionSummary summary = await administrationService.GetTeamPermissionsAsync();

        Assert.True(summary.CanUpdateSpreadsheet);
        Assert.False(summary.CanDeleteSpreadsheet);
        Assert.Equal(
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
            summary.TeamName);
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

    private async Task<ApplicationUser> CreatePathologistAsync(
        string email,
        AccountStatus accountStatus)
    {
        ApplicationUser user = await CreateUserAsync(
            email,
            ApplicationRoleNames.ClinicalPathologist,
            accountStatus);

        return user;
    }

    private async Task<ApplicationUser> CreateUserAsync(
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

        IdentityResult creationResult = await userManager.CreateAsync(user, "Password1!");

        Assert.True(creationResult.Succeeded, Describe(creationResult));

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, roleName);

        Assert.True(roleResult.Succeeded, Describe(roleResult));

        return user;
    }

    private static string Describe(IdentityResult identityResult)
    {
        IEnumerable<string> errors = identityResult.Errors.Select(error => error.Description);

        string description = string.Join("; ", errors);

        return description;
    }
}
