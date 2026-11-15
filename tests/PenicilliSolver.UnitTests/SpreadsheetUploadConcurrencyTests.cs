using System.Security.Cryptography;

using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Services;

using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Proves that two uploads accepted at the same moment cannot corrupt each
/// other's stored file.
/// </summary>
/// <remarks>
/// <para>
/// Two service instances are built, each with its own database, and both are
/// pointed at ONE storage directory. That is what two pathologists uploading
/// simultaneously looks like to the filesystem: separate circuits, separate
/// database contexts, shared storage.
/// </para>
/// <para>
/// This test failed on the previous design. The staging name was derived from a
/// fixed canonical name, so both uploads wrote to the same staging path; one
/// upload then promoted the other's bytes while recording a hash of its own. It
/// failed either with <c>FileNotFoundException</c> or, when both staged before
/// either promoted, with no error at all and a row that lied about its file.
/// </para>
/// </remarks>
public sealed class SpreadsheetUploadConcurrencyTests : IDisposable
{
    private readonly string contentRootPath;

    public SpreadsheetUploadConcurrencyTests()
    {
        contentRootPath = Path.Combine(
            Path.GetTempPath(),
            $"penicillisolver-concurrent-{Guid.NewGuid():N}");

        Directory.CreateDirectory(contentRootPath);
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
    public async Task TwoConcurrentUploads_EachKeepTheirOwnStoredFile()
    {
        BuiltService firstService = BuildService("first");
        BuiltService secondService = BuildService("second");

        try
        {
            byte[] firstBytes = System.Text.Encoding.UTF8.GetBytes(
                "Organism,Organism one\nAmoxicillin,90\nCefixime,10\n");

            byte[] secondBytes = System.Text.Encoding.UTF8.GetBytes(
                "Organism,Organism two\nVancomycin,80\nMeropenem,70\n");

            using MemoryStream firstContent = new(firstBytes);
            using MemoryStream secondContent = new(secondBytes);

            // Started before either is awaited, so both are in flight together.
            Task<SpreadsheetUploadResult> firstTask = firstService.Service.AcceptUploadAsync(
                firstContent,
                "first.csv",
                "pathologist-user-id");

            Task<SpreadsheetUploadResult> secondTask = secondService.Service.AcceptUploadAsync(
                secondContent,
                "second.csv",
                "pathologist-user-id");

            SpreadsheetUploadResult[] results = await Task.WhenAll(firstTask, secondTask);

            // 1. Neither upload reports a failure.
            Assert.All(results, result => Assert.True(result.IsSuccess, result.Message));

            // 2. Each recorded row's own file still holds that row's own bytes.
            //    This is the assertion that catches silent corruption: without it,
            //    two successful-looking uploads could have swapped content.
            int verifiedRowCount = 0;

            foreach (BuiltService builtService in new[] { firstService, secondService })
            {
                foreach (SpreadsheetUpload row in builtService.Database.SpreadsheetUploads)
                {
                    string storedPath = Path.Combine(
                        builtService.StorageDirectoryPath,
                        Path.GetFileName(row.StoredFilePath));

                    Assert.True(
                        File.Exists(storedPath),
                        $"row {row.Id} points at a file that does not exist");

                    byte[] storedBytes = await File.ReadAllBytesAsync(storedPath);

                    string actualHash = Convert.ToHexStringLower(
                        SHA256.HashData(storedBytes));

                    Assert.Equal(row.ContentHash, actualHash);

                    verifiedRowCount++;
                }
            }

            Assert.Equal(2, verifiedRowCount);

            // 3. No staging leftovers: two uploads, two stored files, nothing else.
            int storedFileCount = Directory
                .GetFiles(firstService.StorageDirectoryPath)
                .Length;

            Assert.Equal(2, storedFileCount);
        }
        finally
        {
            firstService.Database.Dispose();
            secondService.Database.Dispose();
        }
    }

    /// <summary>
    /// Builds one service with its own SQLite database, sharing the test's
    /// content root and therefore the storage directory.
    /// </summary>
    /// <param name="suffix">A unique suffix so each service gets its own database file.</param>
    private BuiltService BuildService(string suffix)
    {
        string databasePath = Path.Combine(contentRootPath, $"upload-{suffix}.db");

        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

        ApplicationDbContext database = new(options);
        database.Database.EnsureCreated();

        IWebHostEnvironment environment = new StubWebHostEnvironment(contentRootPath);
        IConfiguration configuration = new ConfigurationBuilder().Build();
        IStringLocalizerFactory localizerFactory = TestLocalizerFactory.Create();

        SpreadsheetStorageService storageService = new(environment, configuration);

        AntibioticAbbreviationService abbreviationService = new(
            database,
            new StubAuthorizationService(),
            localizerFactory);

        SpreadsheetUploadService service = new(
            database,
            storageService,
            abbreviationService,
            configuration,
            localizerFactory,
            NullLogger<SpreadsheetUploadService>.Instance);

        BuiltService builtService = new(
            service,
            database,
            storageService.StorageDirectoryPath);

        return builtService;
    }

    /// <summary>One service instance together with the database it owns.</summary>
    private sealed record BuiltService(
        SpreadsheetUploadService Service,
        ApplicationDbContext Database,
        string StorageDirectoryPath);

    /// <summary>
    /// A minimal <see cref="IWebHostEnvironment"/> that supplies only the
    /// content root the storage service reads.
    /// </summary>
    private sealed class StubWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        /// <inheritdoc />
        public string ApplicationName { get; set; } = "PenicilliSolver.UnitTests";

        /// <inheritdoc />
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();

        /// <inheritdoc />
        public string WebRootPath { get; set; } = contentRootPath;

        /// <inheritdoc />
        public string EnvironmentName { get; set; } = "Development";

        /// <inheritdoc />
        public string ContentRootPath { get; set; } = contentRootPath;

        /// <inheritdoc />
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRootPath);
    }
}