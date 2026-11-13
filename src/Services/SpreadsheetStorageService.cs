namespace penicillisolver_v2.Services;

using Microsoft.AspNetCore.Hosting;

using penicillisolver_v2.Domain.Enums;

/// <summary>
/// Persists the single current shared spreadsheet to disk.
/// </summary>
/// <remarks>
/// <para>
/// There is exactly one current spreadsheet in the application, so the file on
/// disk has a fixed canonical name derived from its format rather than the name
/// the uploader supplied: <c>current-spreadsheet.csv</c> or
/// <c>current-spreadsheet.xlsx</c>. Writing to a canonical name is what makes
/// an accepted upload replace the previous one, and it is why the opposite
/// extension is deleted when a file is promoted: a stale
/// <c>current-spreadsheet.csv</c> beside a fresh <c>current-spreadsheet.xlsx</c>
/// would be read by whichever code path looks for the other format and would
/// silently resurrect the old data.
/// </para>
/// <para>
/// Writing is split into two steps so a failed upload cannot destroy a working
/// spreadsheet. <see cref="SaveIncomingFileAsync"/> writes the uploaded bytes
/// to a staging name and returns a handle describing it;
/// <see cref="PromoteStagedFile"/> atomically moves that staging file onto the
/// canonical name once the caller has proven the file parses; and
/// <see cref="DeleteStagedFile"/> discards a staging file that failed to parse.
/// </para>
/// </remarks>
public sealed partial class SpreadsheetStorageService
{
    private const int StreamCopyBufferSize = 81920;

    private const string StagingFileNameSuffix = ".incoming";

    private readonly string storageDirectoryPath;

    /// <summary>
    /// Creates the storage service rooted at the configured directory.
    /// </summary>
    /// <param name="environment">The hosting environment, used to resolve the content root.</param>
    /// <param name="configuration">The application configuration.</param>
    public SpreadsheetStorageService(
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        string? configuredDirectory = configuration[StorageDirectoryConfigurationKey];

        string directoryToUse;

        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            directoryToUse = DefaultStorageDirectoryRelativePath;
        }
        else
        {
            directoryToUse = configuredDirectory;
        }

        // Resolve to an absolute path anchored on the content root so the
        // storage location does not depend on the process working directory.
        string combinedPath = Path.Combine(environment.ContentRootPath, directoryToUse);

        storageDirectoryPath = Path.GetFullPath(combinedPath);
    }

    /// <summary>The configuration key holding the storage directory.</summary>
    public const string StorageDirectoryConfigurationKey = "SpreadsheetStorage:Directory";

    /// <summary>The default storage directory, relative to the content root.</summary>
    public const string DefaultStorageDirectoryRelativePath = "App_Data/spreadsheets";

    /// <summary>The absolute directory the stored spreadsheet lives in.</summary>
    public string StorageDirectoryPath => storageDirectoryPath;

    /// <summary>
    /// Writes an uploaded stream to a uniquely named staging file inside the
    /// storage directory.
    /// </summary>
    /// <remarks>
    /// Nothing canonical is touched here. The caller promotes the staging file
    /// with <see cref="PromoteStagedFile"/> once the content has parsed, and
    /// discards it with <see cref="DeleteStagedFile"/> when it has not. The
    /// staging file is deliberately a sibling of the canonical file so the
    /// later move is a same-volume rename rather than a copy.
    /// <para>
    /// The staging name is unique per call. It must never be shared: two uploads
    /// in flight at once would otherwise write to the same path, and the second
    /// would overwrite the first's bytes before either had been read.
    /// </para>
    /// </remarks>
    /// <param name="content">The uploaded file content.</param>
    /// <param name="fileFormat">The format the content will be stored under.</param>
    /// <returns>A description of the staging file that was written.</returns>
    public async Task<SpreadsheetStorageResult> SaveIncomingFileAsync(
        Stream content,
        SpreadsheetFileFormat fileFormat)
    {
        ArgumentNullException.ThrowIfNull(content);

        Directory.CreateDirectory(storageDirectoryPath);

        string stagingFileName = $"{Guid.NewGuid():N}{StagingFileNameSuffix}";

        string stagingFilePath = Path.Combine(storageDirectoryPath, stagingFileName);

        long writtenByteCount = await WriteStreamToFileAsync(content, stagingFilePath);

        string contentHash = await ComputeContentHashAsync(stagingFilePath);

        // The canonical name is derived from the content, so identical content
        // always resolves to the same file and different content never collides.
        // A fixed name would make every upload share one path, and a second
        // upload would destroy the file an earlier row still points at.
        string canonicalFileName = $"{contentHash}{ResolveExtension(fileFormat)}";

        SpreadsheetStorageResult stagingResult = new(
            StoredFilePath: stagingFilePath,
            RelativeStoredFilePath: canonicalFileName,
            ContentHash: contentHash,
            FileFormat: fileFormat,
            ByteCount: writtenByteCount);

        return stagingResult;
    }

    /// <summary>
    /// Moves a staging file onto its content-addressed canonical name.
    /// </summary>
    /// <remarks>
    /// The move is idempotent. Because the name is derived from the content, an
    /// existing target holds identical bytes, so the staged copy is redundant and
    /// is discarded. The move deliberately does not overwrite: an existing target
    /// is the same content by construction, and overwriting would destroy a file
    /// that an earlier upload row still points at.
    /// </remarks>
    /// <param name="stagedFilePath">The staging file returned by <see cref="SaveIncomingFileAsync"/>.</param>
    /// <param name="canonicalRelativeFileName">
    /// The content-addressed name, relative to the storage directory, carried by
    /// the same <see cref="SpreadsheetStorageResult"/>.
    /// </param>
    public void PromoteStagedFile(
        string stagedFilePath,
        string canonicalRelativeFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalRelativeFileName);

        string canonicalFilePath = Path.Combine(
            storageDirectoryPath,
            canonicalRelativeFileName);

        if (File.Exists(canonicalFilePath))
        {
            DeleteStagedFile(stagedFilePath);

            return;
        }

        File.Move(stagedFilePath, canonicalFilePath);
    }

    /// <summary>
    /// Deletes a staging file that failed to parse, leaving any canonical file
    /// untouched.
    /// </summary>
    /// <param name="stagedFilePath">The staging file returned by <see cref="SaveIncomingFileAsync"/>.</param>
    public void DeleteStagedFile(string stagedFilePath)
    {
        if (string.IsNullOrWhiteSpace(stagedFilePath))
        {
            return;
        }

        if (File.Exists(stagedFilePath))
        {
            File.Delete(stagedFilePath);
        }
    }

    /// <summary>
    /// Saves an uploaded stream as the current spreadsheet in one call and
    /// returns a description of the canonical file.
    /// </summary>
    /// <remarks>
    /// This is the convenience entry point for callers that have already
    /// validated the content, such as the storage tests. The upload pipeline
    /// uses the staged pair so a parse failure can be rolled back.
    /// </remarks>
    /// <param name="content">The uploaded file content.</param>
    /// <param name="fileFormat">The format to store the file under.</param>
    /// <returns>Where the file was written, its hash, its format and its size.</returns>
    public async Task<SpreadsheetStorageResult> SaveCurrentSpreadsheetAsync(
        Stream content,
        SpreadsheetFileFormat fileFormat)
    {
        ArgumentNullException.ThrowIfNull(content);

        SpreadsheetStorageResult stagingResult = await SaveIncomingFileAsync(content, fileFormat);

        PromoteStagedFile(
            stagingResult.StoredFilePath,
            stagingResult.RelativeStoredFilePath);

        string canonicalFilePath = Path.Combine(
            storageDirectoryPath,
            stagingResult.RelativeStoredFilePath);

        SpreadsheetStorageResult canonicalResult = stagingResult with
        {
            StoredFilePath = canonicalFilePath,
        };

        return canonicalResult;
    }

    /// <summary>
    /// Removes every stored spreadsheet file, canonical or staged.
    /// </summary>
    /// <returns>The number of files that were removed.</returns>
    public int DeleteAll()
    {
        if (!Directory.Exists(storageDirectoryPath))
        {
            return 0;
        }

        int deletedFileCount = 0;

        foreach (string filePath in Directory.EnumerateFiles(storageDirectoryPath))
        {
            File.Delete(filePath);
            deletedFileCount++;
        }

        return deletedFileCount;
    }

    /// <summary>
    /// Resolves the file extension a stored spreadsheet of the supplied format
    /// carries.
    /// </summary>
    /// <param name="fileFormat">The format to resolve the extension for.</param>
    /// <returns>The extension, including its leading dot.</returns>
    private static string ResolveExtension(SpreadsheetFileFormat fileFormat)
    {
        if (fileFormat == SpreadsheetFileFormat.Xlsx)
        {
            return ".xlsx";
        }

        return ".csv";
    }
}
