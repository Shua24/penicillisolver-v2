namespace penicillisolver_v2.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

using penicillisolver_v2.Data;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Resources;

/// <summary>
/// Serves the current spreadsheet to the pages: the persisted metadata row, the
/// parsed document, and the susceptibility ranking derived from it.
/// </summary>
/// <remarks>
/// <para>
/// Parsing a spreadsheet is the expensive part of every viewer page load, so
/// the parsed document is cached in memory and keyed on the upload row's
/// content hash. The hash changes whenever the file content changes, so a new
/// upload invalidates the cache without any explicit eviction call, and two
/// uploads with identical bytes share a cache entry. The cache deliberately
/// holds a single entry, because there is only ever one current spreadsheet.
/// </para>
/// <para>
/// This service knows nothing about antibiotic abbreviation mappings. Those are
/// a separate feature layered on top of the names this service returns.
/// </para>
/// </remarks>
public sealed class SpreadsheetQueryService(
    ApplicationDbContext database,
    SpreadsheetStorageService storageService,
    SpreadsheetDocumentCache cache,
    IStringLocalizerFactory localizerFactory)
{
    /// <summary>
    /// The localizer the readers need to build their failure messages. Built
    /// from the factory because this service is not a Razor component.
    /// </summary>
    private readonly IStringLocalizer localizer =
        localizerFactory.Create(typeof(SharedResource));

    /// <summary>
    /// Returns the current upload's metadata row, or null when nothing has been
    /// uploaded.
    /// </summary>
    /// <remarks>
    /// Replacement appends a row rather than updating one in place, so the
    /// current spreadsheet is the NEWEST row. Mapping rows hang off a specific
    /// upload, and reusing an identifier across a replacement would carry the
    /// previous file's mappings onto a different file.
    /// </remarks>
    /// <returns>The newest upload row, or null.</returns>
    public async Task<SpreadsheetUpload?> GetCurrentUploadAsync()
    {
        SpreadsheetUpload? currentUpload = await database.SpreadsheetUploads
            .AsNoTracking()
            .OrderByDescending(upload => upload.Id)
            .FirstOrDefaultAsync();

        return currentUpload;
    }

    /// <summary>
    /// Returns the parsed current spreadsheet, or null when nothing has been
    /// uploaded or the stored file can no longer be read.
    /// </summary>
    /// <returns>The parsed document, or null.</returns>
    public async Task<SpreadsheetDocument?> GetCurrentDocumentAsync()
    {
        SpreadsheetUpload? currentUpload = await GetCurrentUploadAsync();

        if (currentUpload is null)
        {
            return null;
        }

        SpreadsheetDocument? cachedDocument = cache.Get(currentUpload.ContentHash);

        if (cachedDocument is not null)
        {
            return cachedDocument;
        }

        string absoluteFilePath = ResolveStoredFilePath(currentUpload);

        if (!File.Exists(absoluteFilePath))
        {
            return null;
        }

        SpreadsheetImportResult importResult = ReadStoredFile(absoluteFilePath, currentUpload.FileFormat);

        if (!importResult.IsSuccess || importResult.Document is null)
        {
            return null;
        }

        SpreadsheetDocument document = importResult.Document;

        cache.Set(currentUpload.ContentHash, document);

        return document;
    }

    /// <summary>
    /// Returns the most susceptible antibiotics for ONE organism in the current
    /// spreadsheet.
    /// </summary>
    /// <param name="organismName">The organism to rank against.</param>
    /// <param name="requestedCount">How many antibiotics to return.</param>
    /// <returns>The ranking, or an empty list when nothing has been uploaded or the organism is absent.</returns>
    public async Task<IReadOnlyList<AntibioticSusceptibility>> GetTopSusceptibleWithinOrganismAsync(
        string organismName,
        int requestedCount)
    {
        SpreadsheetDocument? document = await GetCurrentDocumentAsync();

        if (document is null)
        {
            return [];
        }

        IReadOnlyList<AntibioticSusceptibility> ranking =
            AntibioticRankingService.GetMostSusceptibleWithinOrganism(
                document,
                organismName,
                requestedCount);

        return ranking;
    }

    /// <summary>
    /// Returns the full ranking of every antibiotic for ONE organism in the
    /// current spreadsheet.
    /// </summary>
    /// <param name="organismName">The organism to rank against.</param>
    /// <returns>The ranking, or an empty list when nothing has been uploaded or the organism is absent.</returns>
    public async Task<IReadOnlyList<AntibioticSusceptibility>> GetFullRankingWithinOrganismAsync(
        string organismName)
    {
        SpreadsheetDocument? document = await GetCurrentDocumentAsync();

        if (document is null)
        {
            return [];
        }

        IReadOnlyList<AntibioticSusceptibility> ranking =
            AntibioticRankingService.RankWithinOrganism(document, organismName);

        return ranking;
    }

    /// <summary>
    /// Resolves a typed organism name against the current spreadsheet.
    /// </summary>
    /// <param name="typedName">What the user typed or selected.</param>
    /// <returns>
    /// The resolution outcome. A not-found result is returned when nothing has
    /// been uploaded, so a page can always render a single not-found path.
    /// </returns>
    public async Task<OrganismLookupResult> ResolveOrganismAsync(string? typedName)
    {
        SpreadsheetDocument? document = await GetCurrentDocumentAsync();

        if (document is null)
        {
            return OrganismLookupResult.NotFound([]);
        }

        OrganismLookupResult lookupResult = OrganismResolver.Resolve(
            document.OrganismNames,
            typedName);

        return lookupResult;
    }

    /// <summary>
    /// Returns every organism in the current spreadsheet, for a picker.
    /// </summary>
    /// <returns>The organism names, or an empty list when nothing has been uploaded.</returns>
    public async Task<IReadOnlyList<string>> GetOrganismNamesAsync()
    {
        SpreadsheetDocument? document = await GetCurrentDocumentAsync();

        if (document is null)
        {
            return [];
        }

        return document.OrganismNames;
    }

    /// <summary>
    /// Reads a stored file with the reader that matches its recorded format.
    /// </summary>
    private SpreadsheetImportResult ReadStoredFile(
        string absoluteFilePath,
        Domain.Enums.SpreadsheetFileFormat fileFormat)
    {
        if (fileFormat == Domain.Enums.SpreadsheetFileFormat.Xlsx)
        {
            return XlsxSpreadsheetReader.Read(absoluteFilePath, localizer);
        }

        return CsvSpreadsheetReader.Read(absoluteFilePath, localizer);
    }

    /// <summary>
    /// Resolves the absolute path of the stored file from the upload row.
    /// </summary>
    private string ResolveStoredFilePath(SpreadsheetUpload upload)
    {
        // Older rows may hold a path that is already absolute; a relative path
        // is resolved against the storage directory.
        if (Path.IsPathRooted(upload.StoredFilePath))
        {
            return upload.StoredFilePath;
        }

        string fileName = Path.GetFileName(upload.StoredFilePath);

        string absoluteFilePath = Path.Combine(storageService.StorageDirectoryPath, fileName);

        return absoluteFilePath;
    }
}
