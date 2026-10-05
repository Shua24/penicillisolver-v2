using Microsoft.EntityFrameworkCore;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Services;

/// <summary>
/// The copy-forward half of <see cref="AntibioticAbbreviationService"/>: bringing
/// the previous upload's mappings over to a newly uploaded file.
/// </summary>
public sealed partial class AntibioticAbbreviationService
{
    /// <summary>
    /// Copies every mapping from one upload onto another, preserving the
    /// abbreviation and its full name exactly, and stamping the copies with the
    /// destination upload identifier. The source rows are left untouched against
    /// their own upload.
    /// </summary>
    /// <remarks>
    /// Copying is by EXACT abbreviation string match, so an abbreviation that
    /// also existed in the previous file keeps the meaning the pathologists
    /// already gave it and only genuinely new abbreviations start unmapped.
    /// <para>
    /// The operation is idempotent: copying the same source onto the same
    /// destination twice adds no duplicate rows and does not throw, because an
    /// abbreviation already present on the destination is skipped.
    /// </para>
    /// </remarks>
    public async Task CopyMappingsForwardAsync(int fromUploadId, int toUploadId)
    {
        if (fromUploadId == toUploadId)
        {
            return;
        }

        List<AntibioticAbbreviation> sourceMappings = await database.AntibioticAbbreviations
            .AsNoTracking()
            .Where(mapping => mapping.SpreadsheetUploadId == fromUploadId)
            .ToListAsync();

        if (sourceMappings.Count == 0)
        {
            return;
        }

        List<string> destinationAbbreviations = await database.AntibioticAbbreviations
            .AsNoTracking()
            .Where(mapping => mapping.SpreadsheetUploadId == toUploadId)
            .Select(mapping => mapping.Abbreviation)
            .ToListAsync();

        HashSet<string> destinationSet = new(destinationAbbreviations, StringComparer.Ordinal);
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        foreach (AntibioticAbbreviation sourceMapping in sourceMappings)
        {
            if (destinationSet.Contains(sourceMapping.Abbreviation))
            {
                continue;
            }

            AntibioticAbbreviation copiedMapping = new()
            {
                SpreadsheetUploadId = toUploadId,
                Abbreviation = sourceMapping.Abbreviation,
                FullName = sourceMapping.FullName,
                CreatedByUserId = sourceMapping.CreatedByUserId,
                CreatedAtUtc = timestamp,
                LastModifiedByUserId = sourceMapping.LastModifiedByUserId,
                LastModifiedAtUtc = timestamp,
            };

            database.AntibioticAbbreviations.Add(copiedMapping);
            destinationSet.Add(copiedMapping.Abbreviation);
        }

        await database.SaveChangesAsync();
    }

    /// <summary>
    /// The single call the upload flow makes once a replacement upload row has
    /// been persisted: it brings the previous upload's mappings forward as the
    /// new upload's starting point.
    /// </summary>
    /// <param name="previousUploadId">
    /// The identifier of the upload being replaced, or null when this is the
    /// first upload and there is nothing to copy.
    /// </param>
    /// <param name="newUploadId">The identifier of the newly persisted upload.</param>
    /// <example>
    /// <c>await abbreviationService.CarryMappingsToNewUploadAsync(previousUpload.Id, newUpload.Id);</c>
    /// </example>
    public async Task CarryMappingsToNewUploadAsync(int? previousUploadId, int newUploadId)
    {
        if (previousUploadId is null)
        {
            return;
        }

        await CopyMappingsForwardAsync(previousUploadId.Value, newUploadId);
    }
}
