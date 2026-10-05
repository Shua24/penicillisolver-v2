using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Checks <see cref="SpreadsheetQueryService"/> against the real <c>amr.csv</c>
/// sample: that it ranks the stored file, honours a requested count, and serves
/// the parsed document from cache on a second call without re-reading the file.
/// </summary>
public sealed class SpreadsheetQueryServiceTests : IDisposable
{
    private readonly string contentRootPath;
    private readonly ApplicationDbContext database;
    private readonly SpreadsheetStorageService storageService;
    private readonly SpreadsheetQueryService queryService;

    /// <summary>Creates a private content root, database and query service.</summary>
    public SpreadsheetQueryServiceTests()
    {
        contentRootPath = Path.Combine(
            Path.GetTempPath(),
            $"penicillisolver-query-{Guid.NewGuid():N}");

        Directory.CreateDirectory(contentRootPath);

        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"query-{Guid.NewGuid():N}")
                .Options;

        database = new ApplicationDbContext(options);

        IWebHostEnvironment environment = new StubWebHostEnvironment(contentRootPath);

        IConfiguration configuration = new ConfigurationBuilder().Build();

        storageService = new SpreadsheetStorageService(environment, configuration);

        SpreadsheetDocumentCache cache = new();

        queryService = new SpreadsheetQueryService(database, storageService, cache);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        database.Dispose();

        if (Directory.Exists(contentRootPath))
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task GetCurrentDocumentAsync_ReturnsNullWhenNothingHasBeenUploaded()
    {
        SpreadsheetDocument? document = await queryService.GetCurrentDocumentAsync();

        Assert.Null(document);
    }

    [Fact]
    public async Task GetTopResistantAsync_HonoursTheRequestedCount()
    {
        await StoreRealSampleAsync();

        IReadOnlyList<AntibioticResistance> five = await queryService.GetTopResistantAsync(5);
        IReadOnlyList<AntibioticResistance> two = await queryService.GetTopResistantAsync(2);

        Assert.Equal(5, five.Count);
        Assert.Equal(2, two.Count);

        // The first entries of the shorter list must match the longer one, so
        // the ranking is stable regardless of how many are requested.
        List<string> fiveNames = five.Select(resistance => resistance.AntibioticName).ToList();
        List<string> twoNames = two.Select(resistance => resistance.AntibioticName).ToList();

        Assert.Equal(fiveNames.Take(2), twoNames);
    }

    [Fact]
    public async Task GetTopResistantAsync_DefaultsToThreeWhenThePageAsksForThree()
    {
        await StoreRealSampleAsync();

        // The viewer page's default is three, so asking for three must yield
        // exactly three rows from the real sample.
        IReadOnlyList<AntibioticResistance> three = await queryService.GetTopResistantAsync(3);

        Assert.Equal(3, three.Count);

        List<string> names = three.Select(resistance => resistance.AntibioticName).ToList();

        Assert.Equal(["Cefetamet", "Cefixime", "Ceftibuten"], names);
    }

    [Fact]
    public async Task GetCurrentDocumentAsync_ServesRepeatedCallsFromCache()
    {
        await StoreRealSampleAsync();

        SpreadsheetDocument? firstDocument = await queryService.GetCurrentDocumentAsync();

        Assert.NotNull(firstDocument);

        // Delete the file on disk. A cache hit must still return the document,
        // proving the second call did not re-read the file.
        string storedFilePath = storageService.ResolveCanonicalFilePath(SpreadsheetFileFormat.Csv);

        File.Delete(storedFilePath);

        SpreadsheetDocument? secondDocument = await queryService.GetCurrentDocumentAsync();

        Assert.NotNull(secondDocument);
    }

    private async Task StoreRealSampleAsync()
    {
        string samplePath = SampleDataLocator.CsvSamplePath();

        await using FileStream sampleStream = File.OpenRead(samplePath);

        SpreadsheetStorageResult storageResult = await storageService.SaveCurrentSpreadsheetAsync(
            sampleStream,
            SpreadsheetFileFormat.Csv);

        SpreadsheetImportResult importResult = CsvSpreadsheetReader.Read(storageResult.StoredFilePath);

        Assert.True(importResult.IsSuccess, importResult.ErrorMessage);

        SpreadsheetDocument document = importResult.Document!;

        database.SpreadsheetUploads.Add(new penicillisolver_v2.Domain.Entities.SpreadsheetUpload
        {
            OriginalFileName = "amr.csv",
            StoredFilePath = storageResult.RelativeStoredFilePath,
            ContentHash = storageResult.ContentHash,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = "pathologist-user-id",
            FileFormat = SpreadsheetFileFormat.Csv,
            Orientation = document.Orientation,
            OrganismCount = document.OrganismNames.Count,
            AntibioticCount = document.AntibioticNames.Count,
        });

        await database.SaveChangesAsync();
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
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

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
