namespace penicillisolver_v2.Services;

using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Resources;

/// <summary>
/// Orchestrates the acceptance of one uploaded spreadsheet: format check, size
/// check, storage, parsing, and persistence of the single current upload row.
/// </summary>
/// <remarks>
/// <para>
/// <b>History, not a single overwritten row.</b> Requirement 5 says an
/// accepted upload replaces the current reference file. That replacement is
/// recorded as a NEW row, and the previous row is left in place as history.
/// There are two reasons the row is appended rather than updated in place.
/// </para>
/// <para>
/// First, an abbreviation mapping belongs to a specific upload (see
/// <see cref="Domain.Entities.AntibioticAbbreviation"/>), because the same
/// abbreviation can mean different drugs in different files. If a
/// replacement reused the previous row's identifier, the previous file's
/// mappings would silently carry over onto a new file and render the wrong
/// drug names. Appending gives the new file its own mapping set, seeded by
/// an explicit copy forward, while the old rows stay attached to the file
/// they actually describe.
/// </para>
/// <para>
/// Second, the file on disk is still a single canonical file: replacement
/// overwrites it, exactly as requirement 5 asks. Only the metadata is
/// append-only, and "the current spreadsheet" is the newest row.
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
    AntibioticAbbreviationService abbreviationService,
    IConfiguration configuration,
    IStringLocalizerFactory localizerFactory)
{
    /// <summary>The configuration key holding the maximum accepted file size in bytes.</summary>
    public const string MaximumFileSizeConfigurationKey = "SpreadsheetStorage:MaximumFileSizeBytes";

    /// <summary>
    /// The localizer for this service's own messages. Built from the factory
    /// because the service is not a Razor component.
    /// </summary>
    private readonly IStringLocalizer localizer =
        localizerFactory.Create(typeof(SharedResource));

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
                localizer["Service_UnsupportedSpreadsheet", originalFileName]);
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
                localizer["Service_FileCouldNotBeSaved", originalFileName, exception.Message]);
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

        SpreadsheetUpload uploadRow = await CreateCurrentUploadAsync(
            originalFileName,
            fileFormat,
            document,
            stagingResult,
            uploadedByUserId);

        string successMessage = localizer["Service_UploadSucceeded",
            originalFileName,
            document.OrganismNames.Count,
            document.AntibioticNames.Count];

        SpreadsheetUploadResult successResult = SpreadsheetUploadResult.Success(
            document,
            uploadRow,
            successMessage);

        return successResult;
    }

    /// <summary>
    /// Reads a staged file with the reader that matches its format.
    /// </summary>
    private SpreadsheetImportResult ReadStagedFile(
        string stagedFilePath,
        SpreadsheetFileFormat fileFormat)
    {
        if (fileFormat == SpreadsheetFileFormat.Xlsx)
        {
            return XlsxSpreadsheetReader.Read(stagedFilePath, localizer);
        }

        return CsvSpreadsheetReader.Read(stagedFilePath, localizer);
    }

    /// <summary>
    /// Appends a new current upload row, carrying the previous file's
    /// abbreviation mappings forward onto it.
    /// </summary>
    /// <remarks>
    /// The previous row is left untouched so its mappings keep describing the
    /// file they were written for. The copy forward seeds the new row with those
    /// mappings by exact abbreviation match, so an abbreviation that appeared in
    /// both files keeps its meaning and only genuinely new abbreviations arrive
    /// unmapped.
    /// </remarks>
    private async Task<SpreadsheetUpload> CreateCurrentUploadAsync(
        string originalFileName,
        SpreadsheetFileFormat fileFormat,
        SpreadsheetDocument document,
        SpreadsheetStorageResult stagingResult,
        string uploadedByUserId)
    {
        SpreadsheetUpload? previousUpload = await FindCurrentUploadAsync();

        SpreadsheetUpload uploadRow = new()
        {
            OriginalFileName = originalFileName,
            StoredFilePath = stagingResult.RelativeStoredFilePath,
            ContentHash = stagingResult.ContentHash,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = uploadedByUserId,
            FileFormat = fileFormat,
            Orientation = document.Orientation,
            OrganismCount = document.OrganismNames.Count,
            AntibioticCount = document.AntibioticNames.Count,
        };

        database.SpreadsheetUploads.Add(uploadRow);
        await database.SaveChangesAsync();

        if (previousUpload is not null)
        {
            await abbreviationService.CarryMappingsToNewUploadAsync(
                previousUpload.Id,
                uploadRow.Id);
        }

        return uploadRow;
    }

    /// <summary>
    /// Reads the newest upload row, which is the current spreadsheet.
    /// </summary>
    private async Task<SpreadsheetUpload?> FindCurrentUploadAsync()
    {
        SpreadsheetUpload? currentUpload = await database.SpreadsheetUploads
            .OrderByDescending(upload => upload.Id)
            .FirstOrDefaultAsync();

        return currentUpload;
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

        // The numeric figures are formatted invariantly so "12.5 MB" stays
        // readable in every locale; only the surrounding sentence is localised.
        string actualMegabytes = (actualByteCount / 1024d / 1024d)
            .ToString("F1", CultureInfo.InvariantCulture);
        string maximumMegabytesText = maximumMegabytes
            .ToString("F0", CultureInfo.InvariantCulture);

        string message = localizer[
            "Service_FileTooLarge",
            actualMegabytes,
            maximumMegabytesText];

        return message;
    }
}
