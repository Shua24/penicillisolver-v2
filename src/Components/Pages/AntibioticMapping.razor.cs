using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;

namespace penicillisolver_v2.Components.Pages;

/// <summary>
/// The behaviour behind the <c>/antibiotic-mappings</c> page. Every action is
/// scoped to the CURRENT upload. The page lays out one editable row for each
/// antibiotic abbreviation the file itself detected, so a pathologist only
/// ever types a meaning next to a name that is actually in the file; there is
/// no free-text way to map a name that is not present.
/// </summary>
public partial class AntibioticMapping
{
    private SpreadsheetUpload? currentUpload;
    private SpreadsheetDocument? currentDocument;
    private IReadOnlyList<AntibioticMappingRow> mappingRows = [];
    private ClaimsPrincipal? actingPrincipal;

    private bool isLoading = true;
    private bool canManageMappings;
    private string? statusMessage;
    private bool statusSucceeded;

    private AntibioticMappingRow? pendingDelete;

    /// <summary>
    /// True when the current file uses complete antibiotic names, so there is
    /// nothing for a pathologist to interpret.
    /// </summary>
    /// <remarks>
    /// The csv sample is the motivating case: it lists "Amoxicillin" and
    /// "Amoxicillin/Clavulanic acid" outright. Offering to map those would ask
    /// the user to re-enter a name the file already spells out, so the whole
    /// worksheet is withheld and the page explains why instead.
    /// </remarks>
    private bool mappingIsNotApplicable =>
        currentDocument is not null
        && AntibioticNameClassifier.NeedsNoMapping(currentDocument.AntibioticNames);

    /// <summary>How many detected abbreviations already have a stored meaning.</summary>
    private int mappedRowCount =>
        mappingRows.Count(row => row.Mapping is not null);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        AuthenticationState authenticationState =
            await AuthenticationStateProvider.GetAuthenticationStateAsync();
        actingPrincipal = authenticationState.User;

        AuthorizationResult authorizationResult = await AuthorizationService.AuthorizeAsync(
            actingPrincipal,
            resource: null,
            policyName: AuthorizationPolicyNames.CanManageAntibioticMappings);
        canManageMappings = authorizationResult.Succeeded;

        currentUpload = await QueryService.GetCurrentUploadAsync();

        if (currentUpload is not null)
        {
            currentDocument = await QueryService.GetCurrentDocumentAsync();
            await ReloadMappingRowsAsync();
        }

        isLoading = false;
    }

    /// <summary>
    /// Builds one editable row for every abbreviation detected in the current
    /// file, pre-filling the rows that already hold a mapping for this upload.
    /// </summary>
    private async Task ReloadMappingRowsAsync()
    {
        if (currentUpload is null || currentDocument is null)
        {
            mappingRows = [];
            return;
        }

        IReadOnlyList<AntibioticAbbreviation> storedMappings =
            await AbbreviationService.GetMappingsAsync(currentUpload.Id);

        Dictionary<string, AntibioticAbbreviation> mappingByAbbreviation =
            storedMappings
                .ToDictionary(
                    mapping => NormalisedAbbreviation(mapping.Abbreviation),
                    mapping => mapping,
                    StringComparer.Ordinal);

        List<AntibioticMappingRow> rows = [];
        HashSet<string> seenAbbreviations = [];

        foreach (string antibioticName in currentDocument.AntibioticNames)
        {
            string normalisedAbbreviation = NormalisedAbbreviation(antibioticName);

            if (normalisedAbbreviation.Length == 0
                || seenAbbreviations.Contains(normalisedAbbreviation))
            {
                continue;
            }

            // Only abbreviated names need a meaning. A file that spells its
            // antibiotics out in full is withheld from this worksheet entirely
            // (mappingIsNotApplicable), so here every listed row is one the file
            // detected as needing interpretation.
            bool isAbbreviation =
                AntibioticNameClassifier.IsAbbreviation(antibioticName);

            if (!isAbbreviation)
            {
                continue;
            }

            seenAbbreviations.Add(normalisedAbbreviation);

            mappingByAbbreviation.TryGetValue(
                normalisedAbbreviation,
                out AntibioticAbbreviation? mapping);

            AntibioticMappingRow row = new()
            {
                Abbreviation = normalisedAbbreviation,
                Mapping = mapping,
                StoredFullName = mapping?.FullName ?? string.Empty,
                WorkingFullName = mapping?.FullName ?? string.Empty,
            };

            rows.Add(row);
        }

        mappingRows = rows;
        statusMessage = null;
    }

    /// <summary>
    /// Persists every row at once: a filled box on an unmapped row creates a
    /// mapping, and a changed box on a mapped row updates it. Rows that are
    /// unchanged, or blanked, are left for the row's Delete button.
    /// </summary>
    private async Task SaveMappingsAsync()
    {
        if (currentUpload is null || currentDocument is null || actingPrincipal is null)
        {
            return;
        }

        string actingUserId = ResolveActingUserId();

        List<WriteResult> writeResults = [];

        foreach (AntibioticMappingRow row in mappingRows)
        {
            string requestedFullName = row.WorkingFullName.Trim();
            string storedFullName = row.StoredFullName.Trim();

            // A blank or unchanged box writes nothing; removing a meaning is
            // the row's Delete button, not the save button.
            if (requestedFullName.Length == 0 || requestedFullName == storedFullName)
            {
                continue;
            }

            WriteResult result;

            if (row.Mapping is null)
            {
                result = await AbbreviationService.CreateMappingAsync(
                    currentUpload.Id,
                    row.Abbreviation,
                    requestedFullName,
                    actingUserId,
                    actingPrincipal,
                    currentDocument);
            }
            else
            {
                result = await AbbreviationService.UpdateMappingAsync(
                    row.Mapping.Id,
                    requestedFullName,
                    actingUserId,
                    actingPrincipal);
            }

            writeResults.Add(result);
        }

        ApplyResults(writeResults);

        bool everythingSucceeded = writeResults.TrueForAll(result => result.Succeeded);

        if (everythingSucceeded)
        {
            await ReloadMappingRowsAsync();
        }
    }

    /// <summary>Summarises a batch of writes into the one status banner.</summary>
    private void ApplyResults(IReadOnlyList<WriteResult> writeResults)
    {
        int succeededCount = writeResults.Count(result => result.Succeeded);
        int failedCount = writeResults.Count(result => !result.Succeeded);

        if (writeResults.Count == 0)
        {
            statusSucceeded = false;
            statusMessage = "Nothing has changed since the last save.";
            return;
        }

        if (failedCount == 0)
        {
            statusSucceeded = true;
            statusMessage = $"{succeededCount} meaning{(succeededCount == 1 ? "" : "s")} saved.";
            return;
        }

        statusSucceeded = false;
        string failureDetail = string.Join(" ", writeResults
            .Where(result => !result.Succeeded)
            .Select(result => result.Message));

        statusMessage = $"{succeededCount} saved, {failedCount} could not be saved. {failureDetail}";
    }

    private void RequestDelete(AntibioticMappingRow row)
    {
        pendingDelete = row;
        statusMessage = null;
    }

    private void CancelDelete()
    {
        pendingDelete = null;
    }

    private async Task ConfirmDeleteAsync()
    {
        if (pendingDelete is null || actingPrincipal is null)
        {
            return;
        }

        AntibioticMappingRow row = pendingDelete;
        pendingDelete = null;

        if (row.Mapping is null)
        {
            statusSucceeded = false;
            statusMessage = "That abbreviation has no meaning to remove.";
            return;
        }

        string actingUserId = ResolveActingUserId();

        WriteResult result = await AbbreviationService.DeleteMappingAsync(
            row.Mapping.Id,
            actingUserId,
            actingPrincipal);

        statusSucceeded = result.Succeeded;
        statusMessage = result.Message;

        if (result.Succeeded)
        {
            await ReloadMappingRowsAsync();
        }
    }

    private string ResolveActingUserId()
    {
        string? userId = actingPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return userId ?? string.Empty;
    }

    /// <summary>
    /// Trims an abbreviation to the form the service stores and keys on.
    /// </summary>
    private static string NormalisedAbbreviation(string abbreviation)
    {
        string normalised = abbreviation.Trim();

        return normalised;
    }
}
