using System.Globalization;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Constants;
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
    private readonly CapturingLogger<SpreadsheetUploadService> logger = new();

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
            logger);
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
    /// Verifies that a file the reader cannot parse is refused with a sentence
    /// that carries no framework text, and that the exception still reaches the
    /// log.
    /// </summary>
    /// <remarks>
    /// This is the pairing that makes dropping the technical tail safe. The
    /// reader puts the message and the exception in separate places on purpose,
    /// and the upload service is the caller that has a logger, so it is the one
    /// that records it. Asserting only the message would let someone delete the
    /// logging call and lose the diagnosis while every test stayed green.
    /// </remarks>
    [Fact]
    public async Task A_file_the_reader_cannot_parse_is_refused_and_the_cause_is_logged()
    {
        IReadOnlyDictionary<string, string> resources =
            TestResourceFiles.LoadValues(TestResourceFiles.NeutralFileName);

        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(SupportedLanguages.English);

            // An unterminated quoted field, which TextFieldParser refuses. The
            // csv reader does not care about the staging file's extension, so
            // this is the failure the real upload path can reach.
            byte[] malformedBytes = System.Text.Encoding.UTF8.GetBytes(
                "Organism,Organism one\n\"unterminated,1\n");

            SpreadsheetUploadResult failedResult;

            using (MemoryStream badContent = new(malformedBytes))
            {
                failedResult = await uploadService.AcceptUploadAsync(
                    badContent,
                    "malformed.csv",
                    "pathologist-user-id");
            }

            Assert.False(failedResult.IsSuccess);

            LogEntry? warning = logger.Entries.FirstOrDefault(
                entry => entry.Level == LogLevel.Warning && entry.Exception is not null);

            Assert.NotNull(warning);

            string failureCauseMessage = warning.Exception!.Message;

            Assert.NotEmpty(failureCauseMessage);

            // The exception text is recorded but never reaches the banner: that
            // is the whole point of splitting the two.
            Assert.DoesNotContain(failureCauseMessage, failedResult.Message, StringComparison.Ordinal);

            // The sentence itself still ends where the resource sentence ends,
            // so nothing has been appended to it.
            string resourceSentence = resources["Service_CsvMalformedLine"];

            string sentenceSuffix = resourceSentence[
                (resourceSentence.IndexOf("{0}", StringComparison.Ordinal) + 3)..];

            Assert.EndsWith(sentenceSuffix, failedResult.Message, StringComparison.Ordinal);

            // Behaviour unchanged: nothing recorded and nothing left staged.
            Assert.Equal(0, database.SpreadsheetUploads.Count());
            Assert.Empty(Directory.GetFiles(storageService.StorageDirectoryPath));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>
    /// Verifies that a real xlsx workbook is accepted through the upload
    /// pipeline.
    /// </summary>
    /// <remarks>
    /// This is the regression test for the staging-name bug. The pipeline stages
    /// every upload as <c>{guid}.incoming</c>, and the reader used to hand that
    /// path to <c>XLWorkbook(string)</c>, which infers the format from the
    /// extension and threw <c>ArgumentException</c> before reading a byte. Every
    /// xlsx upload failed, so this test is the one that proves the feature works
    /// at all.
    /// </remarks>
    [Fact]
    public async Task A_valid_xlsx_upload_is_accepted()
    {
        string samplePath = SampleDataLocator.XlsxSamplePath();

        await using FileStream content = File.OpenRead(samplePath);

        SpreadsheetUploadResult result = await uploadService.AcceptUploadAsync(
            content,
            "amr.xlsx",
            "pathologist-user-id");

        Assert.True(result.IsSuccess, result.Message);

        SpreadsheetUpload storedUpload = database.SpreadsheetUploads.Single();

        Assert.EndsWith(".xlsx", storedUpload.StoredFilePath, StringComparison.Ordinal);

        string storedFilePath = Path.Combine(
            storageService.StorageDirectoryPath,
            storedUpload.StoredFilePath);

        Assert.True(File.Exists(storedFilePath));

        // A successful upload promotes its staging file, so none is left.
        Assert.Empty(Directory.GetFiles(storageService.StorageDirectoryPath, "*.incoming"));
    }

    /// <summary>
    /// Verifies that a rejected xlsx upload leaves no staging file behind.
    /// </summary>
    /// <remarks>
    /// Nothing points at an orphaned staging file and nothing sweeps the folder,
    /// so one would sit there for the life of the deployment. The upload service
    /// deletes it on the failure path and, as a backstop, when the reader throws
    /// instead of returning a failure.
    /// </remarks>
    [Fact]
    public async Task A_rejected_xlsx_upload_leaves_no_staging_file()
    {
        byte[] garbageBytes = System.Text.Encoding.UTF8.GetBytes(
            "this is not a zip archive, let alone a workbook");

        using MemoryStream content = new(garbageBytes);

        SpreadsheetUploadResult result = await uploadService.AcceptUploadAsync(
            content,
            "not-a-workbook.xlsx",
            "pathologist-user-id");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, database.SpreadsheetUploads.Count());

        Assert.Empty(Directory.GetFiles(storageService.StorageDirectoryPath));
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
