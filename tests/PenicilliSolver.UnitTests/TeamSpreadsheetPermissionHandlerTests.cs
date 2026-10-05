using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Checks the conditional update and delete permission held by the infectious
/// disease control team. The permission is not a role: it lives on the team
/// record and a clinical pathologist flips it at runtime, so these tests drive
/// the handler directly against a real database.
/// </summary>
public sealed class TeamSpreadsheetPermissionHandlerTests : IDisposable
{
    private readonly ApplicationDbContext database;

    /// <summary>Creates an isolated in-memory database for one test.</summary>
    public TeamSpreadsheetPermissionHandlerTests()
    {
        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"team-permission-{Guid.NewGuid():N}")
                .Options;

        database = new ApplicationDbContext(options);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        database.Dispose();
    }

    [Fact]
    public async Task TeamMember_IsAllowedToUpdate_WhenTheTeamRecordGrantsUpdate()
    {
        await SetTeamPermissionAsync(canUpdate: true, canDelete: false);

        bool isAllowed = await EvaluateAsync(
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
            AccountStatus.Active,
            TeamSpreadsheetPermissionKind.Update);

        Assert.True(isAllowed);
    }

    [Fact]
    public async Task TeamMember_IsDeniedUpdate_WhenTheTeamRecordWithholdsUpdate()
    {
        await SetTeamPermissionAsync(canUpdate: false, canDelete: false);

        bool isAllowed = await EvaluateAsync(
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
            AccountStatus.Active,
            TeamSpreadsheetPermissionKind.Update);

        Assert.False(isAllowed);
    }

    [Fact]
    public async Task TeamMember_IsDeniedDelete_WhenOnlyUpdateIsGranted()
    {
        // Update and delete are separate grants. Granting update must not leak
        // delete permission to the team.
        await SetTeamPermissionAsync(canUpdate: true, canDelete: false);

        bool isAllowed = await EvaluateAsync(
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
            AccountStatus.Active,
            TeamSpreadsheetPermissionKind.Delete);

        Assert.False(isAllowed);
    }

    [Fact]
    public async Task ClinicalPathologist_IsAllowed_WithoutAnyTeamGrant()
    {
        // The pathologist's permission is unconditional and cannot be changed.
        await SetTeamPermissionAsync(canUpdate: false, canDelete: false);

        bool isAllowed = await EvaluateAsync(
            ApplicationRoleNames.ClinicalPathologist,
            AccountStatus.Active,
            TeamSpreadsheetPermissionKind.Update);

        Assert.True(isAllowed);
    }

    [Fact]
    public async Task OtherDoctor_IsDenied_EvenWhenTheTeamRecordGrantsThePermission()
    {
        await SetTeamPermissionAsync(canUpdate: true, canDelete: true);

        bool isAllowed = await EvaluateAsync(
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active,
            TeamSpreadsheetPermissionKind.Update);

        Assert.False(isAllowed);
    }

    [Fact]
    public async Task PendingTeamMember_IsDenied_EvenWhenTheTeamRecordGrantsThePermission()
    {
        // The pending gate must hold even for a team member the team record
        // would otherwise permit.
        await SetTeamPermissionAsync(canUpdate: true, canDelete: true);

        bool isAllowed = await EvaluateAsync(
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
            AccountStatus.Pending,
            TeamSpreadsheetPermissionKind.Update);

        Assert.False(isAllowed);
    }

    [Fact]
    public async Task TeamMember_IsDenied_WhenTheTeamRecordDoesNotExist()
    {
        // No row means no grant. The handler must fail closed rather than throw.
        bool isAllowed = await EvaluateAsync(
            ApplicationRoleNames.InfectiousDiseaseControlTeam,
            AccountStatus.Active,
            TeamSpreadsheetPermissionKind.Delete);

        Assert.False(isAllowed);
    }

    private async Task SetTeamPermissionAsync(bool canUpdate, bool canDelete)
    {
        TeamPermission teamPermission = new()
        {
            TeamName = ApplicationRoleNames.InfectiousDiseaseControlTeam,
            CanUpdateSpreadsheet = canUpdate,
            CanDeleteSpreadsheet = canDelete,
            LastModifiedByUserId = "pathologist-user-id",
            LastModifiedAtUtc = DateTimeOffset.UtcNow,
        };

        database.TeamPermissions.Add(teamPermission);
        int writtenRowCount = await database.SaveChangesAsync();

        Assert.Equal(1, writtenRowCount);
    }

    private async Task<bool> EvaluateAsync(
        string roleName,
        AccountStatus accountStatus,
        TeamSpreadsheetPermissionKind permissionKind)
    {
        ClaimsPrincipal principal = BuildPrincipal(roleName, accountStatus);
        TeamSpreadsheetPermissionHandler handler = new(database);
        TeamSpreadsheetPermissionRequirement requirement = new(permissionKind);

        AuthorizationHandlerContext context = new([requirement], principal, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    private static ClaimsPrincipal BuildPrincipal(string roleName, AccountStatus accountStatus)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Role, roleName),
            new Claim(ApplicationClaimTypes.AccountStatus, accountStatus.ToString()),
        ];

        ClaimsIdentity identity = new(claims, authenticationType: "Test");
        ClaimsPrincipal principal = new(identity);

        return principal;
    }
}
