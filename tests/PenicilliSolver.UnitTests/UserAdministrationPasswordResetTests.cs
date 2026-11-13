using Microsoft.AspNetCore.Identity;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;

using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Checks the password-reset operation: who may call it, what it refuses, and
/// what it leaves behind on the account.
/// </summary>
public sealed class UserAdministrationPasswordResetTests : UserAdministrationTestFixture
{
    [Fact]
    public async Task Pathologist_can_reset_a_doctors_password()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "reset.pathologist@example.test",
            AccountStatus.Active);

        ApplicationUser doctor = await CreateUserAsync(
            "reset.doctor@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active);

        AdministrationResult result = await AdministrationService.ResetPasswordAsync(
            actingPathologist.Id,
            doctor.Id,
            "Temporary1!");

        Assert.True(result.IsSuccess, result.Message);

        ApplicationUser reloaded = (await UserManager.FindByIdAsync(doctor.Id))!;

        bool newPasswordWorks = await UserManager.CheckPasswordAsync(reloaded, "Temporary1!");

        Assert.True(newPasswordWorks);
    }

    [Fact]
    public async Task Reset_forces_a_change_at_next_sign_in()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "reset.pathologist@example.test",
            AccountStatus.Active);

        ApplicationUser doctor = await CreateUserAsync(
            "reset.doctor@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active);

        AdministrationResult result = await AdministrationService.ResetPasswordAsync(
            actingPathologist.Id,
            doctor.Id,
            "Temporary1!");

        Assert.True(result.IsSuccess, result.Message);

        ApplicationUser reloaded = (await UserManager.FindByIdAsync(doctor.Id))!;

        Assert.True(reloaded.MustChangePassword);
    }

    [Fact]
    public async Task Reset_records_who_performed_it_and_when()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "reset.pathologist@example.test",
            AccountStatus.Active);

        ApplicationUser doctor = await CreateUserAsync(
            "reset.doctor@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active);

        DateTimeOffset beforeReset = DateTimeOffset.UtcNow;

        AdministrationResult result = await AdministrationService.ResetPasswordAsync(
            actingPathologist.Id,
            doctor.Id,
            "Temporary1!");

        Assert.True(result.IsSuccess, result.Message);

        ApplicationUser reloaded = (await UserManager.FindByIdAsync(doctor.Id))!;

        Assert.Equal(actingPathologist.Id, reloaded.LastPasswordResetByUserId);
        Assert.NotNull(reloaded.LastPasswordResetAtUtc);
        Assert.True(reloaded.LastPasswordResetAtUtc >= beforeReset);
    }

    [Fact]
    public async Task Reset_rotates_the_security_stamp()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "reset.pathologist@example.test",
            AccountStatus.Active);

        ApplicationUser doctor = await CreateUserAsync(
            "reset.doctor@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active);

        string stampBefore = await UserManager.GetSecurityStampAsync(doctor);

        AdministrationResult result = await AdministrationService.ResetPasswordAsync(
            actingPathologist.Id,
            doctor.Id,
            "Temporary1!");

        Assert.True(result.IsSuccess, result.Message);

        ApplicationUser reloaded = (await UserManager.FindByIdAsync(doctor.Id))!;
        string stampAfter = await UserManager.GetSecurityStampAsync(reloaded);

        Assert.NotEqual(stampBefore, stampAfter);
    }

    [Fact]
    public async Task Doctor_cannot_reset_anyones_password()
    {
        ApplicationUser actingDoctor = await CreateUserAsync(
            "reset.actingdoctor@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active);

        ApplicationUser target = await CreateUserAsync(
            "reset.target@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active);

        AdministrationResult result = await AdministrationService.ResetPasswordAsync(
            actingDoctor.Id,
            target.Id,
            "Temporary1!");

        Assert.False(result.IsSuccess);

        ApplicationUser reloaded = (await UserManager.FindByIdAsync(target.Id))!;

        bool originalPasswordStillWorks = await UserManager.CheckPasswordAsync(
            reloaded,
            "Password1!");

        Assert.True(originalPasswordStillWorks);
    }

    [Fact]
    public async Task Reset_of_a_disabled_account_is_refused()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "reset.pathologist@example.test",
            AccountStatus.Active);

        ApplicationUser disabledDoctor = await CreateUserAsync(
            "reset.disabled@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Disabled);

        AdministrationResult result = await AdministrationService.ResetPasswordAsync(
            actingPathologist.Id,
            disabledDoctor.Id,
            "Temporary1!");

        Assert.False(result.IsSuccess);

        ApplicationUser reloaded = (await UserManager.FindByIdAsync(disabledDoctor.Id))!;

        // The refusal happens before any write: the old password still works and
        // the account is not marked for a change it could never complete.
        bool originalPasswordStillWorks = await UserManager.CheckPasswordAsync(
            reloaded,
            "Password1!");

        Assert.True(originalPasswordStillWorks);
        Assert.False(reloaded.MustChangePassword);
    }

    [Fact]
    public async Task Reset_of_an_unknown_account_is_refused()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "reset.pathologist@example.test",
            AccountStatus.Active);

        AdministrationResult result = await AdministrationService.ResetPasswordAsync(
            actingPathologist.Id,
            "no-such-account-id",
            "Temporary1!");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Reset_rejects_a_password_that_breaks_the_policy()
    {
        ApplicationUser actingPathologist = await CreatePathologistAsync(
            "reset.pathologist@example.test",
            AccountStatus.Active);

        ApplicationUser doctor = await CreateUserAsync(
            "reset.doctor@example.test",
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active);

        // The fixture relaxes the password policy, so this asserts the plumbing
        // rather than the policy: a reset goes through the same validators as
        // registration, and a blank password is rejected by the argument guard.
        AdministrationResult result = await AdministrationService.ResetPasswordAsync(
            actingPathologist.Id,
            doctor.Id,
            " ");

        Assert.False(result.IsSuccess);
    }
}
