namespace penicillisolver_v2.Services;

using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// Orchestrates the acceptance of one uploaded spreadsheet: format check, size
/// check, storage, parsing, and persistence of the single current upload row.
/// </summary>
/// <remarks>
/// <para>
/// <b>History vs. single row.</b> This implementation keeps exactly one
/// <see cref="SpreadsheetUpload"/> row. Requirement 5 says an accepted upload
/// replaces the current reference file, so the row is updated in place rather
/// than appended to: the table's job is "what is the current spreadsheet", not
/// "what has ever been uploaded". Keeping one row means the query service never
/// has to decide which of several rows is current, and it keeps the
/// <c>SpreadsheetUploads</c> table's meaning identical to the file on disk.
/// </para>
/// <para>
/// <b>Failed uploads must not destroy a working spreadsheet.</b> The incoming
/// file is written to a temporary name, parsed from there, and only moved onto
/// the canonical name once parsing has succeeded. A parse failure deletes the
/// temporary file and leaves the previously stored spreadsheet untouched. The
/// alternative — write the canonical name first and delete on failure — has a
/// window in which a crash leaves the application with no readable spreadsheet
/// at all.
/// </para>
/// </remarks>
public sealed class SpreadsheetUploadService(
    ApplicationDbContext database,
    SpreadsheetStorageService storageService,
    IConfiguration configuration)
{
    /// <summary>The configuration key holding the maximum accepted file size in bytes.</summary>
    public const string MaximumFileSizeConfigurationKey = "SpreadsheetStorage:MaximumFileSizeBytes";

    /// <summary>The default maximum accepted file size, in bytes (10 MB).</summary>
    public const long DefaultMaximumFileSizeBytes = 10L * 1024 * 1024;

    private const string TemporaryFileExtension = ".incoming";

    /// <summary>
    /// Validates, stores and parses an uploaded spreadsheet, then records it as
    /// the current upload.
    /// </summary>
    /// <param name="content">The uploaded file content. The caller owns the stream.</param>
    /// <param name="originalFileName">The file name exactly as the uploader supplied it.</param>
    /// <param name="uploadedByUserId">The identifier of the account performing the upload.</param>
    /// <returns>A result carrying the document and row, or a user facing failure message.</returns>
    public async Task<SpreadsheetUploadResult> AcceptUploadAsync(
        Stream content,
        string originalFileName,
        string uploadedByUserId)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedByUserId);

        SpreadsheetFileFormat? resolvedFormat = SpreadsheetFormatResolver.Resolve(originalFileName);

        if (resolvedFormat is null)
        {
            return SpreadsheetUploadResult.Failure(
                $"'{originalFileName}' is not a supported spreadsheet. " +
                "Upload a .csv or .xlsx file.");
        }

        SpreadsheetFileFormat fileFormat = resolvedFormat.Value;

        if (content.CanSeek)
        {
            long declaredByteCount = content.Length;

            if (declaredByteCount > ResolveMaximumFileSizeBytes())
            {
                return SpreadsheetUploadResult.Failure(BuildTooLargeMessage(declaredByteCount));
            }
        }

        // Write the upload to a staging name first. Nothing canonical is
        // touched until the file has been proven to parse.
        SpreadsheetStorageResult stagingResult;

        try
        {
            stagingResult = await storageService.SaveIncomingFileAsync(content, fileFormat);
        }
        catch (IOException exception)
        {
            return SpreadsheetUploadResult.Failure(
                $"The file '{originalFileName}' could not be saved: {exception.Message}");
        }

        SpreadsheetImportResult importResult = ReadStagedFile(stagingResult.StoredFilePath, fileFormat);

        if (!importResult.IsSuccess || importResult.Document is null)
        {
            storageService.DeleteStagedFile(stagingResult.StoredFilePath);

            return SpreadsheetUploadResult.Failure(importResult.ErrorMessage);
        }

        SpreadsheetDocument document = importResult.Document;

        // The file parsed, so promote the staged file onto its canonical name.
        // This is the point of no return: from here the previous spreadsheet is
        // gone and the new one is in its place.
        storageService.PromoteStagedFile(stagingResult.StoredFilePath, fileFormat);

        SpreadsheetUpload uploadRow = await UpsertCurrentUploadAsync(
            originalFileName,
            fileFormat,
            document,
            stagingResult,
            uploadedByUserId);

        string successMessage =
            $"Uploaded '{originalFileName}' ({document.OrganismNames.Count} organisms, " +
            $"{document.AntibioticNames.Count} antibiotics). It is now the current spreadsheet.";

        SpreadsheetUploadResult successResult = SpreadsheetUploadResult.Success(
            document,
            uploadRow,
            successMessage);

        return successResult;
    }

    /// <summary>
    /// Reads a staged file with the reader that matches its format.
    /// </summary>
    private static SpreadsheetImportResult ReadStagedFile(
        string stagedFilePath,
        SpreadsheetFileFormat fileFormat)
    {
        if (fileFormat == SpreadsheetFileFormat.Xlsx)
        {
            return XlsxSpreadsheetReader.Read(stagedFilePath);
        }

        return CsvSpreadsheetReader.Read(stagedFilePath);
    }

    /// <summary>
    /// Updates the single current upload row, or inserts one when none exists.
    /// </summary>
    private async Task<SpreadsheetUpload> UpsertCurrentUploadAsync(
        string originalFileName,
        SpreadsheetFileFormat fileFormat,
        SpreadsheetDocument document,
        SpreadsheetStorageResult stagingResult,
        string uploadedByUserId)
    {
        SpreadsheetUpload? existingUpload = await database.SpreadsheetUploads
            .OrderBy(upload => upload.Id)
            .FirstOrDefaultAsync();

        SpreadsheetUpload uploadRow;

        if (existingUpload is null)
        {
            uploadRow = new SpreadsheetUpload();
            database.SpreadsheetUploads.Add(uploadRow);
        }
        else
        {
            uploadRow = existingUpload;
        }

        uploadRow.OriginalFileName = originalFileName;
        uploadRow.StoredFilePath = stagingResult.RelativeStoredFilePath;
        uploadRow.ContentHash = stagingResult.ContentHash;
        uploadRow.UploadedAtUtc = DateTimeOffset.UtcNow;
        uploadRow.UploadedByUserId = uploadedByUserId;
        uploadRow.FileFormat = fileFormat;
        uploadRow.Orientation = document.Orientation;
        uploadRow.OrganismCount = document.OrganismNames.Count;
        uploadRow.AntibioticCount = document.AntibioticNames.Count;

        await database.SaveChangesAsync();

        return uploadRow;
    }

    /// <summary>
    /// Reads the configured maximum file size, falling back to the default.
    /// </summary>
    private long ResolveMaximumFileSizeBytes()
    {
        string? configuredValue = configuration[MaximumFileSizeConfigurationKey];

        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return DefaultMaximumFileSizeBytes;
        }

        bool parsed = long.TryParse(configuredValue, out long configuredByteCount);

        if (!parsed || configuredByteCount <= 0)
        {
            return DefaultMaximumFileSizeBytes;
        }

        return configuredByteCount;
    }

    /// <summary>
    /// Builds the rejection message naming the configured limit.
    /// </summary>
    private string BuildTooLargeMessage(long actualByteCount)
    {
        long maximumByteCount = ResolveMaximumFileSizeBytes();

        double maximumMegabytes = maximumByteCount / 1024d / 1024d;

        string message =
            $"The file is {actualByteCount / 1024d / 1024d:F1} MB, which exceeds the " +
            $"maximum upload size of {maximumMegabytes:F0} MB.";

        return message;
    }
}
