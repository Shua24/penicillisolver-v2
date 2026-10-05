using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers what happens when a second spreadsheet replaces the current one. A
/// replacement is recorded as a NEW upload row, so the previous file's
/// abbreviation mappings keep describing the file they were actually written
/// for, and the new row starts from an explicit copy of them.
/// </summary>
public sealed partial class SpreadsheetUploadServiceTests
{
    [Fact]
    public async Task AcceptingASecondUpload_AppendsANewCurrentRow()
    {
        byte[] firstBytes = System.Text.Encoding.UTF8.GetBytes(
            "Organism,Organism one\nAmoxicillin,90\n");

        using (MemoryStream firstContent = new(firstBytes))
        {
            await uploadService.AcceptUploadAsync(firstContent, "first.csv", "pathologist-user-id");
        }

        byte[] secondBytes = System.Text.Encoding.UTF8.GetBytes(
            "Organism,Organism one\nCefixime,10\n");

        using (MemoryStream secondContent = new(secondBytes))
        {
            SpreadsheetUploadResult secondResult = await uploadService.AcceptUploadAsync(
                secondContent,
                "second.csv",
                "pathologist-user-id");

            Assert.True(secondResult.IsSuccess, secondResult.Message);
        }

        // Replacement is recorded as a new row so the previous file's
        // abbreviation mappings keep describing the file they were written for.
        // The previous row is retained as history.
        int uploadRowCount = database.SpreadsheetUploads.Count();

        Assert.Equal(2, uploadRowCount);

        // The current spreadsheet is the NEWEST row, not the oldest.
        SpreadsheetUpload? currentUpload = database.SpreadsheetUploads
            .OrderByDescending(upload => upload.Id)
            .First();

        Assert.Equal("second.csv", currentUpload.OriginalFileName);

        // The row describing the first file survives untouched.
        SpreadsheetUpload previousUpload = database.SpreadsheetUploads
            .OrderBy(upload => upload.Id)
            .First();

        Assert.Equal("first.csv", previousUpload.OriginalFileName);
    }

    [Fact]
    public async Task AcceptingAReplacementUpload_CarriesTheMappingsForwardOntoTheNewRow()
    {
        byte[] firstBytes = System.Text.Encoding.UTF8.GetBytes(
            "Organism,Organism one\nAmoxicillin,90\nVancomycin,20\n");

        using (MemoryStream firstContent = new(firstBytes))
        {
            await uploadService.AcceptUploadAsync(firstContent, "first.csv", "pathologist-user-id");
        }

        SpreadsheetUpload firstUpload = database.SpreadsheetUploads
            .OrderBy(upload => upload.Id)
            .First();

        // A pathologist maps one abbreviation against the first file.
        database.AntibioticAbbreviations.Add(new AntibioticAbbreviation
        {
            SpreadsheetUploadId = firstUpload.Id,
            Abbreviation = "Amoxicillin",
            FullName = "Amoxicillin trihydrate",
            CreatedByUserId = "pathologist-user-id",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastModifiedByUserId = "pathologist-user-id",
            LastModifiedAtUtc = DateTimeOffset.UtcNow,
        });

        await database.SaveChangesAsync();

        byte[] secondBytes = System.Text.Encoding.UTF8.GetBytes(
            "Organism,Organism one\nAmoxicillin,40\nCefixime,10\n");

        using (MemoryStream secondContent = new(secondBytes))
        {
            SpreadsheetUploadResult secondResult = await uploadService.AcceptUploadAsync(
                secondContent,
                "second.csv",
                "pathologist-user-id");

            Assert.True(secondResult.IsSuccess, secondResult.Message);
        }

        SpreadsheetUpload secondUpload = database.SpreadsheetUploads
            .OrderByDescending(upload => upload.Id)
            .First();

        // The abbreviation that appeared in both files keeps its meaning on the
        // new upload, so the pathologist does not retype it.
        AntibioticAbbreviation? carriedMapping = database.AntibioticAbbreviations
            .FirstOrDefault(row =>
                row.SpreadsheetUploadId == secondUpload.Id
                && row.Abbreviation == "Amoxicillin");

        Assert.NotNull(carriedMapping);
        Assert.Equal("Amoxicillin trihydrate", carriedMapping.FullName);

        // The original mapping still belongs to the first upload.
        bool originalStillAttached = database.AntibioticAbbreviations
            .Any(row =>
                row.SpreadsheetUploadId == firstUpload.Id
                && row.Abbreviation == "Amoxicillin");

        Assert.True(originalStillAttached);

        // An abbreviation that was never mapped arrives unmapped rather than
        // inheriting a meaning from somewhere else.
        bool cefiximeIsMapped = database.AntibioticAbbreviations
            .Any(row =>
                row.SpreadsheetUploadId == secondUpload.Id
                && row.Abbreviation == "Cefixime");

        Assert.False(cefiximeIsMapped);
    }
}
