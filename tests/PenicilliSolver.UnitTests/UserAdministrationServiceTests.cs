using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Checks the last-active-pathologist guard and the other administration
/// guards on <see cref="UserAdministrationService"/>. The guard is the only
/// thing standing between a mis-click and a permanently locked-out
/// application, so it is exercised against a real
/// <see cref="Microsoft.AspNetCore.Identity.UserManager{TUser}"/>.
/// </summary>
public sealed class UserAdministrationServiceTests : UserAdministrationTestFixture
{
    [Fact]
    public async Task Cannot_demote_the_last_active_pathologist()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "sole.pathologist@example.test",
            AccountStatus.Active);

        AdministrationResult result = await AdministrationService.SetRoleAsync(
            actingPathologist.Id,
            actingPathologist.Id,
            ApplicationRoleNames.OtherDoctor);

        Assert.False(result.IsSuccess);

        // The role must be unchanged: the refusal happens before any write.
        bool stillPathologist = await UserManager.IsInRoleAsync(
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

        AdministrationResult result = await AdministrationService.SetAccountStatusAsync(
            actingPathologist.Id,
            actingPathologist.Id,
            AccountStatus.Disabled);

        Assert.False(result.IsSuccess);

        ApplicationUser survivor = await UserManager.FindByIdAsync(actingPathologist.Id)
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

        AdministrationResult result = await AdministrationService.SetRoleAsync(
            actingPathologist.Id,
            secondPathologist.Id,
            ApplicationRoleNames.OtherDoctor);

        Assert.True(result.IsSuccess);

        bool stillPathologist = await UserManager.IsInRoleAsync(
            secondPathologist,
            ApplicationRoleNames.ClinicalPathologist);

        Assert.False(stillPathologist);

        bool nowOtherDoctor = await UserManager.IsInRoleAsync(
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

        AdministrationResult result = await AdministrationService.SetAccountStatusAsync(
            actingPathologist.Id,
            secondPathologist.Id,
            AccountStatus.Disabled);

        Assert.True(result.IsSuccess);

        ApplicationUser disabledSurvivor =
            await UserManager.FindByIdAsync(secondPathologist.Id)
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

        AdministrationResult result = await AdministrationService.SetAccountStatusAsync(
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

        AdministrationResult result = await AdministrationService.SetAccountStatusAsync(
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

        AdministrationResult result = await AdministrationService.SetAccountStatusAsync(
            actingPathologist.Id,
            pendingUser.Id,
            AccountStatus.Active);

        Assert.True(result.IsSuccess);

        ApplicationUser activatedUser = await UserManager.FindByIdAsync(pendingUser.Id)
            ?? throw new InvalidOperationException("The account disappeared.");

        Assert.Equal(AccountStatus.Active, activatedUser.AccountStatus);
    }

    [Fact]
    public async Task Setting_team_permissions_persists_both_flags()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "acting.pathologist@example.test",
            AccountStatus.Active);

        AdministrationResult result = await AdministrationService.SetTeamPermissionsAsync(
            actingPathologist.Id,
            canUpdateSpreadsheet: true,
            canDeleteSpreadsheet: false);

        Assert.True(result.IsSuccess);

        TeamPermissionSummary summary = await AdministrationService.GetTeamPermissionsAsync();

        Assert.True(summary.CanUpdateSpreadsheet);
        Assert.False(summary.CanDeleteSpreadsheet);
        Assert.Equal(
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
            summary.TeamName);
    }
}
