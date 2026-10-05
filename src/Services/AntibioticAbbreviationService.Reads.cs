using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.ValueObjects;

namespace penicillisolver_v2.Services;

/// <summary>
/// The read half of <see cref="AntibioticAbbreviationService"/>. Every query is
/// scoped to one upload: there is deliberately no lookup that resolves an
/// abbreviation without knowing which file it belongs to.
/// </summary>
public sealed partial class AntibioticAbbreviationService
{
    /// <summary>
    /// Lists every mapping that belongs to the supplied upload, ordered by
    /// abbreviation for a stable display.
    /// </summary>
    public async Task<IReadOnlyList<AntibioticAbbreviation>> GetMappingsAsync(int spreadsheetUploadId)
    {
        List<AntibioticAbbreviation> mappings = await database.AntibioticAbbreviations
            .AsNoTracking()
            .Where(mapping => mapping.SpreadsheetUploadId == spreadsheetUploadId)
            .OrderBy(mapping => mapping.Abbreviation)
            .ToListAsync();

        return mappings;
    }

    /// <summary>
    /// Returns the full name the abbreviation is mapped to for this upload, or
    /// null when the abbreviation is unmapped.
    /// </summary>
    /// <remarks>
    /// The match is an exact ordinal string comparison: the readers store the
    /// header verbatim, percent sign suffix and all, so the mapping must key on
    /// the identical string.
    /// </remarks>
    public async Task<string?> ResolveDisplayNameAsync(int spreadsheetUploadId, string abbreviation)
    {
        string trimmedAbbreviation = NormaliseOrDefault(abbreviation);

        if (trimmedAbbreviation.Length == 0)
        {
            return null;
        }

        AntibioticAbbreviation? mapping = await database.AntibioticAbbreviations
            .AsNoTracking()
            .FirstOrDefaultAsync(row =>
                row.SpreadsheetUploadId == spreadsheetUploadId
                && row.Abbreviation == trimmedAbbreviation);

        return mapping?.FullName;
    }

    /// <summary>
    /// Returns the antibiotic names present in the document that have no
    /// mapping for this upload, in source order and without duplicates.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUnmappedAbbreviationsAsync(
        int spreadsheetUploadId,
        SpreadsheetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<string> mappedAbbreviations = await database.AntibioticAbbreviations
            .AsNoTracking()
            .Where(mapping => mapping.SpreadsheetUploadId == spreadsheetUploadId)
            .Select(mapping => mapping.Abbreviation)
            .ToListAsync();

        HashSet<string> mappedSet = new(mappedAbbreviations, StringComparer.Ordinal);
        List<string> unmappedAbbreviations = [];

        foreach (string antibioticName in document.AntibioticNames)
        {
            string candidate = NormaliseOrDefault(antibioticName);

            if (candidate.Length == 0 || mappedSet.Contains(candidate))
            {
                continue;
            }

            if (!unmappedAbbreviations.Contains(candidate, StringComparer.Ordinal))
            {
                unmappedAbbreviations.Add(candidate);
            }
        }

        return unmappedAbbreviations;
    }

    /// <summary>Trims a value, treating null as the empty string.</summary>
    private static string NormaliseOrDefault(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
