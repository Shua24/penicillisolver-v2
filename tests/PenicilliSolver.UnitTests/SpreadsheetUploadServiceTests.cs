using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly SqliteConnection connection;
    private readonly ApplicationDbContext database;
    private readonly SpreadsheetStorageService storageService;
    private readonly SpreadsheetUploadService uploadService;

    /// <summary>
    /// Creates a private content root, database and services for one test.
    /// </summary>
    /// <remarks>
    /// SQLite in memory, not the EF in-memory provider: the upload service wraps
    /// its metadata write in a transaction, and the in-memory provider refuses
    /// transactions outright ("Transactions are not supported by the in-memory
    /// store"). SQLite also enforces constraints, so the tests exercise something
    /// closer to the deployed database.
    /// </remarks>
    public SpreadsheetUploadServiceTests()
    {
        contentRootPath = Path.Combine(
            Path.GetTempPath(),
            $"penicillisolver-upload-{Guid.NewGuid():N}");

        Directory.CreateDirectory(contentRootPath);

        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

        database = new ApplicationDbContext(options);
        database.Database.EnsureCreated();

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
            localizerFactory,
            NullLogger<SpreadsheetUploadService>.Instance);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        database.Dispose();
        connection.Dispose();

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

        SpreadsheetUpload storedUpload = database.SpreadsheetUploads.Single();

        string storedFilePath = Path.Combine(
            storageService.StorageDirectoryPath,
            storedUpload.StoredFilePath);

        Assert.True(File.Exists(storedFilePath));
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

        SpreadsheetUpload storedUpload = database.SpreadsheetUploads.Single();

        string storedFilePath = Path.Combine(
            storageService.StorageDirectoryPath,
            storedUpload.StoredFilePath);

        byte[] storedBeforeFailure = await File.ReadAllBytesAsync(storedFilePath);

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
        Assert.True(File.Exists(storedFilePath));

        byte[] storedAfterFailure = await File.ReadAllBytesAsync(storedFilePath);

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
