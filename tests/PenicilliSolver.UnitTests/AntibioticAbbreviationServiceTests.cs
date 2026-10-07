using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Checks the upload-scoped antibiotic abbreviation mapping service. The
/// central property under test is that the SAME abbreviation string can hold
/// DIFFERENT meanings under different uploads, because a mapping belongs to a
/// file and not to the application as a whole.
/// </summary>
public sealed class AntibioticAbbreviationServiceTests : IDisposable
{
    private const string PathologistUserId = "pathologist-user-id";

    private readonly ApplicationDbContext database;
    private readonly AntibioticAbbreviationService service;
    private readonly ClaimsPrincipal pathologistPrincipal;

    /// <summary>Creates an isolated in-memory database and service for one test.</summary>
    public AntibioticAbbreviationServiceTests()
    {
        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"abbreviation-{Guid.NewGuid():N}")
                .Options;

        database = new ApplicationDbContext(options);
        service = new AntibioticAbbreviationService(
            database,
            new StubAuthorizationService(),
            TestLocalizerFactory.Create());
        pathologistPrincipal = StubAuthorizationService.BuildPrincipal(
            ApplicationRoleNames.ClinicalPathologist,
            AccountStatus.Active);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        database.Dispose();
    }

    [Fact]
    public async Task ResolveDisplayName_ReturnsNullForAnUnknownAbbreviation()
    {
        await SeedUploadAsync(uploadId: 1);

        string? resolvedName = await service.ResolveDisplayNameAsync(1, "Unknown");

        Assert.Null(resolvedName);
    }

    [Fact]
    public async Task ResolveDisplayName_ReturnsTheNameForAKnownAbbreviation()
    {
        await SeedUploadAsync(uploadId: 1);
        await SeedMappingAsync(uploadId: 1, abbreviation: "GAT %S", fullName: "Gatifloxacin");

        string? resolvedName = await service.ResolveDisplayNameAsync(1, "GAT %S");

        Assert.Equal("Gatifloxacin", resolvedName);
    }

    [Fact]
    public async Task SameAbbreviation_UnderTwoUploads_HoldsTwoDifferentMeanings()
    {
        // THE core of the per-upload design: the abbreviation string is not a
        // global key, so the two rows coexist with no conflict.
        await SeedUploadAsync(uploadId: 1);
        await SeedUploadAsync(uploadId: 2);

        WriteResult firstResult = await service.CreateMappingAsync(
            1,
            "AMR",
            "Amoxicillin",
            PathologistUserId,
            pathologistPrincipal,
            BuildDocument(["AMR"]));

        WriteResult secondResult = await service.CreateMappingAsync(
            2,
            "AMR",
            "Ampicillin resistance marker",
            PathologistUserId,
            pathologistPrincipal,
            BuildDocument(["AMR"]));

        Assert.True(firstResult.Succeeded);
        Assert.True(secondResult.Succeeded);

        string? firstMeaning = await service.ResolveDisplayNameAsync(1, "AMR");
        string? secondMeaning = await service.ResolveDisplayNameAsync(2, "AMR");

        Assert.Equal("Amoxicillin", firstMeaning);
        Assert.Equal("Ampicillin resistance marker", secondMeaning);

        int storedRowCount = await database.AntibioticAbbreviations.CountAsync();
        Assert.Equal(2, storedRowCount);
    }

    [Fact]
    public async Task CreateMapping_RejectsAnAbbreviationThatIsNotInTheDocument()
    {
        await SeedUploadAsync(uploadId: 1);

        WriteResult result = await service.CreateMappingAsync(
            1,
            "NOT-PRESENT",
            "Some drug",
            PathologistUserId,
            pathologistPrincipal,
            BuildDocument(["GAT %S"]));

        Assert.False(result.Succeeded);

        int storedRowCount = await database.AntibioticAbbreviations.CountAsync();
        Assert.Equal(0, storedRowCount);
    }

    [Fact]
    public async Task CreateMapping_RejectsADuplicateAbbreviationForTheSameUpload()
    {
        await SeedUploadAsync(uploadId: 1);
        await SeedMappingAsync(uploadId: 1, abbreviation: "GAT %S", fullName: "Gatifloxacin");

        WriteResult result = await service.CreateMappingAsync(
            1,
            "GAT %S",
            "A second meaning",
            PathologistUserId,
            pathologistPrincipal,
            BuildDocument(["GAT %S"]));

        Assert.False(result.Succeeded);

        int storedRowCount = await database.AntibioticAbbreviations.CountAsync();
        Assert.Equal(1, storedRowCount);
    }

    [Fact]
    public async Task CreateMapping_IsRejectedForANonPathologist()
    {
        await SeedUploadAsync(uploadId: 1);

        ClaimsPrincipal otherDoctorPrincipal = StubAuthorizationService.BuildPrincipal(
            ApplicationRoleNames.OtherDoctor,
            AccountStatus.Active);

        WriteResult result = await service.CreateMappingAsync(
            1,
            "GAT %S",
            "Gatifloxacin",
            "other-doctor-user-id",
            otherDoctorPrincipal,
            BuildDocument(["GAT %S"]));

        Assert.False(result.Succeeded);

        int storedRowCount = await database.AntibioticAbbreviations.CountAsync();
        Assert.Equal(0, storedRowCount);
    }

    [Fact]
    public async Task CopyMappingsForward_CopiesOntoTheNewUploadAndLeavesTheOldRows()
    {
        await SeedUploadAsync(uploadId: 1);
        await SeedUploadAsync(uploadId: 2);

        await SeedMappingAsync(uploadId: 1, abbreviation: "GAT %S", fullName: "Gatifloxacin");
        await SeedMappingAsync(uploadId: 1, abbreviation: "AMR", fullName: "Amoxicillin");

        await service.CopyMappingsForwardAsync(fromUploadId: 1, toUploadId: 2);

        IReadOnlyList<AntibioticAbbreviation> oldMappings = await service.GetMappingsAsync(1);
        IReadOnlyList<AntibioticAbbreviation> newMappings = await service.GetMappingsAsync(2);

        Assert.Equal(2, oldMappings.Count);
        Assert.Equal(2, newMappings.Count);

        string? copiedMeaning = await service.ResolveDisplayNameAsync(2, "GAT %S");
        Assert.Equal("Gatifloxacin", copiedMeaning);

        Assert.All(newMappings, mapping => Assert.Equal(2, mapping.SpreadsheetUploadId));
    }

    [Fact]
    public async Task CopyMappingsForward_IsIdempotent()
    {
        await SeedUploadAsync(uploadId: 1);
        await SeedUploadAsync(uploadId: 2);

        await SeedMappingAsync(uploadId: 1, abbreviation: "GAT %S", fullName: "Gatifloxacin");

        await service.CopyMappingsForwardAsync(fromUploadId: 1, toUploadId: 2);
        await service.CopyMappingsForwardAsync(fromUploadId: 1, toUploadId: 2);

        IReadOnlyList<AntibioticAbbreviation> newMappings = await service.GetMappingsAsync(2);

        AntibioticAbbreviation copiedMapping = Assert.Single(newMappings);
        Assert.Equal("GAT %S", copiedMapping.Abbreviation);
    }

    [Fact]
    public async Task CopyMappingsForward_DoesNotOverwriteAMeaningTheNewUploadAlreadyHas()
    {
        await SeedUploadAsync(uploadId: 1);
        await SeedUploadAsync(uploadId: 2);

        await SeedMappingAsync(uploadId: 1, abbreviation: "AMR", fullName: "Old meaning");
        await SeedMappingAsync(uploadId: 2, abbreviation: "AMR", fullName: "New file meaning");

        await service.CopyMappingsForwardAsync(fromUploadId: 1, toUploadId: 2);

        string? newMeaning = await service.ResolveDisplayNameAsync(2, "AMR");

        Assert.Equal("New file meaning", newMeaning);

        int newUploadRowCount = await database.AntibioticAbbreviations
            .CountAsync(mapping => mapping.SpreadsheetUploadId == 2);
        Assert.Equal(1, newUploadRowCount);
    }

    private async Task SeedUploadAsync(int uploadId)
    {
        SpreadsheetUpload upload = new()
        {
            Id = uploadId,
            OriginalFileName = $"upload-{uploadId}.csv",
            StoredFilePath = $"upload-{uploadId}.csv",
            ContentHash = $"hash-{uploadId}",
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = PathologistUserId,
            FileFormat = SpreadsheetFileFormat.Csv,
            Orientation = SpreadsheetOrientation.AntibioticsAsRows,
            OrganismCount = 1,
            AntibioticCount = 1,
        };

        database.SpreadsheetUploads.Add(upload);
        int writtenRowCount = await database.SaveChangesAsync();

        Assert.Equal(1, writtenRowCount);
    }

    private async Task SeedMappingAsync(int uploadId, string abbreviation, string fullName)
    {
        AntibioticAbbreviation mapping = new()
        {
            SpreadsheetUploadId = uploadId,
            Abbreviation = abbreviation,
            FullName = fullName,
            CreatedByUserId = PathologistUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastModifiedByUserId = PathologistUserId,
            LastModifiedAtUtc = DateTimeOffset.UtcNow,
        };

        database.AntibioticAbbreviations.Add(mapping);
        int writtenRowCount = await database.SaveChangesAsync();

        Assert.Equal(1, writtenRowCount);
    }

    private static SpreadsheetDocument BuildDocument(IReadOnlyList<string> antibioticNames)
    {
        SpreadsheetDocument document = new(
            originalFileName: "test.csv",
            fileFormat: SpreadsheetFileFormat.Csv,
            orientation: SpreadsheetOrientation.AntibioticsAsRows,
            organismNames: ["Escherichia coli"],
            antibioticNames: antibioticNames,
            measurements: []);

        return document;
    }
}
