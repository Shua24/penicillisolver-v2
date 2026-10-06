using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

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
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    private SpreadsheetUpload? currentUpload;
    private SpreadsheetDocument? currentDocument;
    private IReadOnlyList<AntibioticMappingRow> mappingRows = [];
    private ClaimsPrincipal? actingPrincipal;

    private bool isLoading = true;
    private bool canManageMappings;
    private string? statusMessage;
    private bool statusSucceeded;

    /// <summary>
    /// True when the current file uses complete antibiotic names, so there is
    /// nothing for a pathologist to interpret.
    /// </summary>
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

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Register the .NET reference so the scoped module can relay a
            // native close (Esc / backdrop) back into the component.
            DotNetObjectReference<AntibioticMapping> reference =
                DotNetObjectReference.Create(this);
            return JSRuntime.InvokeVoidAsync(
                "antibioticMappingDeleteDialog.init", reference).AsTask();
        }

        // Open only AFTER the render has flushed the pending row into the
        // dialog's text; opening from the click handler would raise the box
        // still showing the previous pending abbreviation.
        if (deleteDialogOpen)
        {
            return JSRuntime.InvokeVoidAsync("antibioticMappingDeleteDialog.open").AsTask();
        }

        return Task.CompletedTask;
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
            // (mappingIsNotApplicable), so every listed row here is one the
            // file detected as needing interpretation.
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
    /// unchanged, or blanked, are left for the row's Remove button.
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
            // the row's Remove button, not the save button.
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

    /// <summary>
    /// Reads the current user's id claim from the acting principal.
    /// </summary>
    /// <returns>The user id, or an empty string when there is no acting principal.</returns>
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
