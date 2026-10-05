using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Exercises <see cref="SpreadsheetStorageService"/> against a real temporary
/// directory. The canonical file name and the replacement behaviour are the
/// whole point of the service, so the tests check the actual file system rather
/// than a mock.
/// </summary>
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
    public async Task SaveCurrentSpreadsheetAsync_UsesTheStableCsvCanonicalName()
    {
        byte[] csvBytes = BuildCsvBytes();

        using MemoryStream content = new(csvBytes);

        SpreadsheetStorageResult result = await storageService.SaveCurrentSpreadsheetAsync(
            content,
            SpreadsheetFileFormat.Csv);

        Assert.Equal(SpreadsheetStorageService.CanonicalCsvFileName, result.RelativeStoredFilePath);
        Assert.True(File.Exists(result.StoredFilePath));
        Assert.Equal(
            Path.Combine(storageService.StorageDirectoryPath, "current-spreadsheet.csv"),
            result.StoredFilePath);
        Assert.Equal(csvBytes.Length, result.ByteCount);
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_UsesTheStableXlsxCanonicalName()
    {
        byte[] xlsxBytes = [1, 2, 3, 4, 5];

        using MemoryStream content = new(xlsxBytes);

        SpreadsheetStorageResult result = await storageService.SaveCurrentSpreadsheetAsync(
            content,
            SpreadsheetFileFormat.Xlsx);

        Assert.Equal(SpreadsheetStorageService.CanonicalXlsxFileName, result.RelativeStoredFilePath);
        Assert.Equal(
            Path.Combine(storageService.StorageDirectoryPath, "current-spreadsheet.xlsx"),
            result.StoredFilePath);
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_ReplacesAPriorFileOfTheOtherExtension()
    {
        // First store a csv, then store an xlsx. The csv must not survive the
        // second save, or a reader looking for the csv would resurrect it.
        using (MemoryStream csvContent = new(BuildCsvBytes()))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                csvContent,
                SpreadsheetFileFormat.Csv);
        }

        string csvPath = storageService.ResolveCanonicalFilePath(SpreadsheetFileFormat.Csv);

        Assert.True(File.Exists(csvPath));

        using (MemoryStream xlsxContent = new([9, 9, 9]))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                xlsxContent,
                SpreadsheetFileFormat.Xlsx);
        }

        string xlsxPath = storageService.ResolveCanonicalFilePath(SpreadsheetFileFormat.Xlsx);

        Assert.True(File.Exists(xlsxPath));
        Assert.False(File.Exists(csvPath));
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_OverwritesAPriorFileOfTheSameExtension()
    {
        using (MemoryStream firstContent = new(BuildCsvBytes("first")))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                firstContent,
                SpreadsheetFileFormat.Csv);
        }

        byte[] secondBytes = BuildCsvBytes("second");

        using (MemoryStream secondContent = new(secondBytes))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                secondContent,
                SpreadsheetFileFormat.Csv);
        }

        string csvPath = storageService.ResolveCanonicalFilePath(SpreadsheetFileFormat.Csv);

        byte[] storedBytes = await File.ReadAllBytesAsync(csvPath);

        Assert.Equal(secondBytes, storedBytes);

        // Only the one canonical file should remain; no staging leftovers.
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

        // The same bytes hashed a second time must give the same value.
        using MemoryStream repeatedContent = new(csvBytes);

        SpreadsheetStorageResult repeatedResult = await storageService.SaveCurrentSpreadsheetAsync(
            repeatedContent,
            SpreadsheetFileFormat.Csv);

        Assert.Equal(result.ContentHash, repeatedResult.ContentHash);
    }

    [Fact]
    public async Task SaveIncomingFileAsync_ThenDeleteStagedFile_LeavesAnyCanonicalFileIntact()
    {
        // This models the parse-failure path: a staged file that did not parse
        // is discarded, and a previously stored canonical file must be unharmed.
        using (MemoryStream existingContent = new(BuildCsvBytes("good")))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                existingContent,
                SpreadsheetFileFormat.Csv);
        }

        string csvPath = storageService.ResolveCanonicalFilePath(SpreadsheetFileFormat.Csv);

        byte[] goodBytes = await File.ReadAllBytesAsync(csvPath);

        SpreadsheetStorageResult stagingResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(BuildCsvBytes("bad")),
            SpreadsheetFileFormat.Csv);

        storageService.DeleteStagedFile(stagingResult.StoredFilePath);

        Assert.True(File.Exists(csvPath));

        byte[] survivingBytes = await File.ReadAllBytesAsync(csvPath);

        Assert.Equal(goodBytes, survivingBytes);
        Assert.False(File.Exists(stagingResult.StoredFilePath));
    }

    [Fact]
    public async Task PromoteStagedFile_MovesTheStagedFileOntoTheCanonicalName()
    {
        SpreadsheetStorageResult stagingResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(BuildCsvBytes()),
            SpreadsheetFileFormat.Csv);

        Assert.NotEqual(
            storageService.ResolveCanonicalFilePath(SpreadsheetFileFormat.Csv),
            stagingResult.StoredFilePath);

        storageService.PromoteStagedFile(stagingResult.StoredFilePath, SpreadsheetFileFormat.Csv);

        string csvPath = storageService.ResolveCanonicalFilePath(SpreadsheetFileFormat.Csv);

        Assert.True(File.Exists(csvPath));
        Assert.False(File.Exists(stagingResult.StoredFilePath));
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
