using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Checks the accept-one-upload workflow against a real temporary storage
/// directory and an in-memory database. The behaviour that matters most is that
/// a rejected upload leaves whatever was already stored exactly as it was.
/// The replacement and copy-forward cases live in the partial in
/// <c>SpreadsheetUploadServiceTests.Replacement.cs</c>.
/// </summary>
public sealed partial class SpreadsheetUploadServiceTests : IDisposable
{
    private readonly string contentRootPath;
    private readonly ApplicationDbContext database;
    private readonly SpreadsheetStorageService storageService;
    private readonly SpreadsheetUploadService uploadService;

    /// <summary>Creates a private content root, database and services for one test.</summary>
    public SpreadsheetUploadServiceTests()
    {
        contentRootPath = Path.Combine(
            Path.GetTempPath(),
            $"penicillisolver-upload-{Guid.NewGuid():N}");

        Directory.CreateDirectory(contentRootPath);

        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"upload-{Guid.NewGuid():N}")
                .Options;

        database = new ApplicationDbContext(options);

        IWebHostEnvironment environment = new StubWebHostEnvironment(contentRootPath);

        IConfiguration configuration = new ConfigurationBuilder().Build();

        storageService = new SpreadsheetStorageService(environment, configuration);

        IStringLocalizerFactory localizerFactory = TestLocalizerFactory.Create();

        AntibioticAbbreviationService abbreviationService = new(
            database,
            new StubAuthorizationService(),
            localizerFactory);

        uploadService = new SpreadsheetUploadService(
            database,
            storageService,
            abbreviationService,
            configuration,
            localizerFactory);
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
    public async Task AcceptUploadAsync_StoresAValidCsvAndRecordsTheRow()
    {
        byte[] csvBytes = System.Text.Encoding.UTF8.GetBytes(
            "Organism,Organism one\nAmoxicillin,90\nCefixime,10\n");

        using MemoryStream content = new(csvBytes);

        SpreadsheetUploadResult result = await uploadService.AcceptUploadAsync(
            content,
            "amr.csv",
            "pathologist-user-id");

        Assert.True(result.IsSuccess, result.Message);

        string canonicalPath = storageService.ResolveCanonicalFilePath(SpreadsheetFileFormat.Csv);

        Assert.True(File.Exists(canonicalPath));
        Assert.Equal(1, database.SpreadsheetUploads.Count());
    }

    [Fact]
    public async Task AcceptUploadAsync_RejectsAnUnsupportedExtension()
    {
        using MemoryStream content = new([1, 2, 3]);

        SpreadsheetUploadResult result = await uploadService.AcceptUploadAsync(
            content,
            "notes.txt",
            "pathologist-user-id");

        Assert.False(result.IsSuccess);
        Assert.Contains(".csv", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, database.SpreadsheetUploads.Count());
    }

    [Fact]
    public async Task A_parseFailure_leaves_the_previousStoredFileIntact()
    {
        // Store a good file first.
        byte[] goodBytes = System.Text.Encoding.UTF8.GetBytes(
            "Organism,Organism one\nAmoxicillin,90\n");

        using (MemoryStream goodContent = new(goodBytes))
        {
            SpreadsheetUploadResult firstResult = await uploadService.AcceptUploadAsync(
                goodContent,
                "good.csv",
                "pathologist-user-id");

            Assert.True(firstResult.IsSuccess, firstResult.Message);
        }

        string canonicalPath = storageService.ResolveCanonicalFilePath(SpreadsheetFileFormat.Csv);

        byte[] storedBeforeFailure = await File.ReadAllBytesAsync(canonicalPath);

        // Now attempt an upload whose content cannot be parsed.
        byte[] badBytes = System.Text.Encoding.UTF8.GetBytes(
            "Organism,Organism one\nAmoxicillin,banana\n");

        SpreadsheetUploadResult failedResult;

        using (MemoryStream badContent = new(badBytes))
        {
            failedResult = await uploadService.AcceptUploadAsync(
                badContent,
                "bad.csv",
                "pathologist-user-id");
        }

        Assert.False(failedResult.IsSuccess);

        // The previously stored good file must be untouched byte for byte.
        Assert.True(File.Exists(canonicalPath));

        byte[] storedAfterFailure = await File.ReadAllBytesAsync(canonicalPath);

        Assert.Equal(storedBeforeFailure, storedAfterFailure);

        // No staging file may be left behind either.
        int storedFileCount = Directory.GetFiles(storageService.StorageDirectoryPath).Length;

        Assert.Equal(1, storedFileCount);
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
