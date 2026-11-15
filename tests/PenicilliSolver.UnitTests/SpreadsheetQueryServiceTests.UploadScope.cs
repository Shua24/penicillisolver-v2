using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;

using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// The upload-scoping half of <see cref="SpreadsheetQueryServiceTests"/>: a page
/// that has already read the current upload must be able to pin every later
/// query to that upload, so a replacement landing mid-page cannot mix two
/// files' data. Kept in a partial file so neither half exceeds the project's
/// 300-line cap.
/// </summary>
public sealed partial class SpreadsheetQueryServiceTests
{
    [Fact]
    public async Task GetOrganismNamesAsync_ReadsTheUploadItIsGivenRatherThanTheNewest()
    {
        SpreadsheetUpload firstUpload = await StoreRealSampleAsync();

        await StoreSyntheticUploadAsync();

        IReadOnlyList<string> firstUploadNames =
            await queryService.GetOrganismNamesAsync(firstUpload);

        Assert.Contains("Candida albicans", firstUploadNames);
        Assert.DoesNotContain("Synthetic organism", firstUploadNames);

        IReadOnlyList<string> newestNames = await queryService.GetOrganismNamesAsync();

        Assert.Contains("Synthetic organism", newestNames);
        Assert.DoesNotContain("Candida albicans", newestNames);
    }

    [Fact]
    public async Task GetTopSusceptibleWithinOrganismAsync_RanksTheUploadItIsGiven()
    {
        // The newest upload does not contain this organism at all, so a ranking
        // that came back non-empty proves the call read the upload it was given
        // rather than re-reading the newest row.
        SpreadsheetUpload firstUpload = await StoreRealSampleAsync();

        await StoreSyntheticUploadAsync();

        IReadOnlyList<AntibioticSusceptibility> ranking =
            await queryService.GetTopSusceptibleWithinOrganismAsync(
                "Candida albicans",
                3,
                firstUpload);

        Assert.NotEmpty(ranking);
    }

    /// <summary>
    /// Stores a second, deliberately different upload so a test can prove a
    /// query reads the upload it was given rather than the newest row.
    /// </summary>
    private async Task<SpreadsheetUpload> StoreSyntheticUploadAsync()
    {
        byte[] csvBytes = System.Text.Encoding.UTF8.GetBytes(
            "Organism,Synthetic organism\nNumber of isolates,1\nAmoxicillin,90\n");

        using MemoryStream content = new(csvBytes);

        SpreadsheetStorageResult storageResult = await storageService.SaveCurrentSpreadsheetAsync(
            content,
            SpreadsheetFileFormat.Csv);

        SpreadsheetImportResult importResult = CsvSpreadsheetReader.Read(
            storageResult.StoredFilePath,
            TestLocalizerFactory.Localizer);

        Assert.True(importResult.IsSuccess, importResult.ErrorMessage);

        SpreadsheetDocument document = importResult.Document!;

        SpreadsheetUpload uploadRow = new()
        {
            OriginalFileName = "synthetic.csv",
            StoredFilePath = storageResult.RelativeStoredFilePath,
            ContentHash = storageResult.ContentHash,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = "pathologist-user-id",
            FileFormat = SpreadsheetFileFormat.Csv,
            Orientation = document.Orientation,
            OrganismCount = document.OrganismNames.Count,
            AntibioticCount = document.AntibioticNames.Count,
        };

        database.SpreadsheetUploads.Add(uploadRow);

        await database.SaveChangesAsync();

        return uploadRow;
    }
}
