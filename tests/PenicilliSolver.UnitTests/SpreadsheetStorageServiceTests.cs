using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Exercises <see cref="SpreadsheetStorageService"/> against a real temporary
/// directory. The stored-file naming IS the service's contract, so these tests
/// check the actual filesystem rather than a mock.
/// </summary>
/// <remarks>
/// The contract these tests defend: a file's name is derived from its own
/// content, so two uploads can never share a path. That is what lets an earlier
/// upload row keep the file its mappings describe, and what makes a concurrent
/// upload harmless instead of destructive.
/// </remarks>
public sealed class SpreadsheetStorageServiceTests : IDisposable
{
    private readonly string contentRootPath;
    private readonly SpreadsheetStorageService storageService;

    /// <summary>Creates a private content root and a storage service over it.</summary>
    public SpreadsheetStorageServiceTests()
    {
        contentRootPath = Path.Combine(
            Path.GetTempPath(),
            $"penicillisolver-storage-{Guid.NewGuid():N}");

        Directory.CreateDirectory(contentRootPath);

        storageService = BuildStorageService(contentRootPath);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(contentRootPath))
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_NamesTheFileByItsContentHash()
    {
        byte[] csvBytes = BuildCsvBytes();

        using MemoryStream content = new(csvBytes);

        SpreadsheetStorageResult result = await storageService.SaveCurrentSpreadsheetAsync(
            content,
            SpreadsheetFileFormat.Csv);

        Assert.Equal($"{result.ContentHash}.csv", result.RelativeStoredFilePath);
        Assert.Equal(
            Path.Combine(storageService.StorageDirectoryPath, $"{result.ContentHash}.csv"),
            result.StoredFilePath);
        Assert.True(File.Exists(result.StoredFilePath));
        Assert.Equal(csvBytes.Length, result.ByteCount);
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_UsesTheFormatExtensionInTheName()
    {
        using MemoryStream content = new([1, 2, 3, 4, 5]);

        SpreadsheetStorageResult result = await storageService.SaveCurrentSpreadsheetAsync(
            content,
            SpreadsheetFileFormat.Xlsx);

        Assert.Equal($"{result.ContentHash}.xlsx", result.RelativeStoredFilePath);
        Assert.True(File.Exists(result.StoredFilePath));
    }

    [Fact]
    public async Task SaveIncomingFileAsync_GivesEveryUploadItsOwnStagingFile()
    {
        // The old design derived the staging name from a fixed canonical name, so
        // two uploads in flight shared one staging path and the second destroyed
        // the first's bytes. Unique names are the fix.
        SpreadsheetStorageResult firstResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(BuildCsvBytes("first")),
            SpreadsheetFileFormat.Csv);

        SpreadsheetStorageResult secondResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(BuildCsvBytes("second")),
            SpreadsheetFileFormat.Csv);

        Assert.NotEqual(firstResult.StoredFilePath, secondResult.StoredFilePath);
        Assert.True(File.Exists(firstResult.StoredFilePath));
        Assert.True(File.Exists(secondResult.StoredFilePath));
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_KeepsTheFileOfAnEarlierUpload()
    {
        byte[] firstBytes = BuildCsvBytes("first");

        SpreadsheetStorageResult firstResult;

        using (MemoryStream firstContent = new(firstBytes))
        {
            firstResult = await storageService.SaveCurrentSpreadsheetAsync(
                firstContent,
                SpreadsheetFileFormat.Csv);
        }

        using (MemoryStream secondContent = new(BuildCsvBytes("second")))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                secondContent,
                SpreadsheetFileFormat.Csv);
        }

        // The earlier upload's file must still hold its own bytes, because the
        // row written for it still points at it.
        Assert.True(File.Exists(firstResult.StoredFilePath));

        byte[] survivingBytes = await File.ReadAllBytesAsync(firstResult.StoredFilePath);

        Assert.Equal(firstBytes, survivingBytes);
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_KeepsTheFileOfTheOtherExtension()
    {
        // A csv and an xlsx are different uploads with different rows. Neither
        // may delete the other.
        SpreadsheetStorageResult csvResult;

        using (MemoryStream csvContent = new(BuildCsvBytes()))
        {
            csvResult = await storageService.SaveCurrentSpreadsheetAsync(
                csvContent,
                SpreadsheetFileFormat.Csv);
        }

        SpreadsheetStorageResult xlsxResult;

        using (MemoryStream xlsxContent = new([9, 9, 9]))
        {
            xlsxResult = await storageService.SaveCurrentSpreadsheetAsync(
                xlsxContent,
                SpreadsheetFileFormat.Xlsx);
        }

        Assert.NotEqual(csvResult.StoredFilePath, xlsxResult.StoredFilePath);
        Assert.True(File.Exists(csvResult.StoredFilePath));
        Assert.True(File.Exists(xlsxResult.StoredFilePath));
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_StoringTheSameContentTwiceLeavesOneFile()
    {
        byte[] csvBytes = BuildCsvBytes();

        using (MemoryStream firstContent = new(csvBytes))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                firstContent,
                SpreadsheetFileFormat.Csv);
        }

        using (MemoryStream secondContent = new(csvBytes))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                secondContent,
                SpreadsheetFileFormat.Csv);
        }

        int fileCount = Directory.GetFiles(storageService.StorageDirectoryPath).Length;

        Assert.Equal(1, fileCount);
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_ComputesAStableLowercaseSha256Hash()
    {
        byte[] csvBytes = BuildCsvBytes();

        using MemoryStream content = new(csvBytes);

        SpreadsheetStorageResult result = await storageService.SaveCurrentSpreadsheetAsync(
            content,
            SpreadsheetFileFormat.Csv);

        Assert.Equal(64, result.ContentHash.Length);
        Assert.Equal(result.ContentHash.ToLowerInvariant(), result.ContentHash);

        using MemoryStream repeatedContent = new(csvBytes);

        SpreadsheetStorageResult repeatedResult = await storageService.SaveCurrentSpreadsheetAsync(
            repeatedContent,
            SpreadsheetFileFormat.Csv);

        Assert.Equal(result.ContentHash, repeatedResult.ContentHash);
    }

    [Fact]
    public async Task DeleteStagedFile_LeavesAnEarlierUploadsFileIntact()
    {
        // Models the parse-failure path: the staged file is discarded, and a file
        // an earlier upload row points at must be unharmed.
        SpreadsheetStorageResult existingResult;

        using (MemoryStream existingContent = new(BuildCsvBytes("good")))
        {
            existingResult = await storageService.SaveCurrentSpreadsheetAsync(
                existingContent,
                SpreadsheetFileFormat.Csv);
        }

        byte[] goodBytes = await File.ReadAllBytesAsync(existingResult.StoredFilePath);

        SpreadsheetStorageResult stagingResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(BuildCsvBytes("bad")),
            SpreadsheetFileFormat.Csv);

        storageService.DeleteStagedFile(stagingResult.StoredFilePath);

        byte[] survivingBytes = await File.ReadAllBytesAsync(existingResult.StoredFilePath);

        Assert.Equal(goodBytes, survivingBytes);
        Assert.False(File.Exists(stagingResult.StoredFilePath));
    }

    [Fact]
    public async Task PromoteStagedFile_MovesTheStagedFileOntoItsContentAddressedName()
    {
        SpreadsheetStorageResult stagingResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(BuildCsvBytes()),
            SpreadsheetFileFormat.Csv);

        string canonicalPath = Path.Combine(
            storageService.StorageDirectoryPath,
            stagingResult.RelativeStoredFilePath);

        Assert.NotEqual(canonicalPath, stagingResult.StoredFilePath);

        storageService.PromoteStagedFile(
            stagingResult.StoredFilePath,
            stagingResult.RelativeStoredFilePath);

        Assert.True(File.Exists(canonicalPath));
        Assert.False(File.Exists(stagingResult.StoredFilePath));
    }

    [Fact]
    public async Task PromoteStagedFile_IsIdempotentForIdenticalContent()
    {
        byte[] csvBytes = BuildCsvBytes();

        using (MemoryStream firstContent = new(csvBytes))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                firstContent,
                SpreadsheetFileFormat.Csv);
        }

        SpreadsheetStorageResult stagingResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(csvBytes),
            SpreadsheetFileFormat.Csv);

        // Promoting content that is already stored must not throw and must not
        // destroy or duplicate the file already in place.
        storageService.PromoteStagedFile(
            stagingResult.StoredFilePath,
            stagingResult.RelativeStoredFilePath);

        string canonicalPath = Path.Combine(
            storageService.StorageDirectoryPath,
            stagingResult.RelativeStoredFilePath);

        Assert.True(File.Exists(canonicalPath));
        Assert.False(File.Exists(stagingResult.StoredFilePath));

        int fileCount = Directory.GetFiles(storageService.StorageDirectoryPath).Length;

        Assert.Equal(1, fileCount);
    }

    private static SpreadsheetStorageService BuildStorageService(string contentRootPath)
    {
        IWebHostEnvironment environment = new StubWebHostEnvironment(contentRootPath);

        IConfiguration configuration = new ConfigurationBuilder().Build();

        SpreadsheetStorageService service = new(environment, configuration);

        return service;
    }

    private static byte[] BuildCsvBytes(string marker = "synthetic spreadsheet")
    {
        string csvText =
            $"Organism,Organism one\nAmoxicillin,{marker}\n";

        byte[] csvBytes = System.Text.Encoding.UTF8.GetBytes(csvText);

        return csvBytes;
    }

    /// <summary>
    /// A minimal <see cref="IWebHostEnvironment"/> that supplies only the
    /// content root the storage service reads.
    /// </summary>
    private sealed class StubWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        /// <inheritdoc />
        public string ApplicationName { get; set; } = "PenicilliSolver.UnitTests";

        /// <inheritdoc />
        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();

        /// <inheritdoc />
        public string WebRootPath { get; set; } = contentRootPath;

        /// <inheritdoc />
        public string EnvironmentName { get; set; } = "Development";

        /// <inheritdoc />
        public string ContentRootPath { get; set; } = contentRootPath;

        /// <inheritdoc />
        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(contentRootPath);
    }
}