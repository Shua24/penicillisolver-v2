using Microsoft.AspNetCore.Components;

using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;

namespace penicillisolver_v2.Components.Pages;

/// <summary>
/// The behaviour behind the resistance ranking page.
/// </summary>
/// <remarks>
/// <para>
/// The ranking is scoped to ONE organism (requirements 6 and 7): the page asks
/// which organism is being treated, resolves that against the file, and then
/// ranks the antibiotics for it. Nothing is ever ranked for an organism the
/// user did not name, so the page has explicit states for "several matches,
/// choose one" and "no such organism" instead of falling back to a guess.
/// </para>
/// <para>
/// The data loading, resolution and ranking live here. The searchable picker
/// that chooses the organism lives in
/// <c>SpreadsheetViewer.Picker.cs</c>; this half decides what to do with the
/// name it hands over.
/// </para>
/// </remarks>
public partial class SpreadsheetViewer
{
    private const int DefaultTopCount = 3;

    private SpreadsheetUpload? currentUpload;
    private IReadOnlyList<string> organismNames = [];
    private IReadOnlyList<AntibioticResistance> topResistant = [];
    private IReadOnlyList<string> ambiguousCandidates = [];
    private string? typedOrganismName;
    private string? resolvedOrganismName;
    private string? lookupMessage;
    private int topCount = DefaultTopCount;
    private int effectiveTopCount = DefaultTopCount;
    private int maximumTopCount = 1;
    private string? topCountError;
    private bool isLoading = true;

    private bool hasResolvedOrganism => resolvedOrganismName is not null;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadCurrentSpreadsheetAsync();
    }

    private async Task LoadCurrentSpreadsheetAsync()
    {
        isLoading = true;

        currentUpload = await QueryService.GetCurrentUploadAsync();

        if (currentUpload is null)
        {
            maximumTopCount = 1;
            organismNames = [];
            suggestions = [];
            topResistant = [];
            isLoading = false;
            return;
        }

        organismNames = await QueryService.GetOrganismNamesAsync();

        RefreshSuggestions();

        maximumTopCount = Math.Max(currentUpload.AntibioticCount, 1);

        effectiveTopCount = ClampTopCount(topCount);

        if (hasResolvedOrganism)
        {
            topResistant = await QueryService.GetTopResistantWithinOrganismAsync(
                resolvedOrganismName!,
                effectiveTopCount);
        }

        isLoading = false;
    }

    private async Task OnShowRankingClickedAsync()
    {
        // The box holds an exact name once a suggestion has been picked, so the
        // button keeps working for someone who types a full name and never opens
        // the list. A partial name resolves to "ambiguous" or "not found", and
        // the page says so rather than guessing.
        isPickerOpen = false;

        await ResolveAndRankAsync(typedOrganismName);
    }

    private async Task OnCandidateChosenAsync(string candidate)
    {
        typedOrganismName = candidate;

        await OnSuggestionChosenAsync(candidate);
    }

    private async Task ResolveAndRankAsync(string? candidateName)
    {
        lookupMessage = null;
        ambiguousCandidates = [];
        resolvedOrganismName = null;
        topResistant = [];

        OrganismLookupResult lookupResult =
            await QueryService.ResolveOrganismAsync(candidateName);

        if (lookupResult.Outcome == OrganismLookupOutcome.Resolved)
        {
            resolvedOrganismName = lookupResult.OrganismName;
            typedOrganismName = lookupResult.OrganismName;

            isLoading = true;
            topResistant = await QueryService.GetTopResistantWithinOrganismAsync(
                resolvedOrganismName!,
                effectiveTopCount);
            isLoading = false;
            return;
        }

        if (lookupResult.Outcome == OrganismLookupOutcome.Ambiguous)
        {
            ambiguousCandidates = lookupResult.Candidates;
            return;
        }

        lookupMessage = BuildNotFoundMessage(candidateName, lookupResult.Candidates);
    }

    private static string BuildNotFoundMessage(
        string? candidateName,
        IReadOnlyList<string> suggestions)
    {
        string trimmedName = (candidateName ?? string.Empty).Trim();

        if (trimmedName.Length == 0)
        {
            return "Type an organism name, then choose Show ranking.";
        }

        if (suggestions.Count == 0)
        {
            return $"Species not found: '{trimmedName}' is not in this spreadsheet.";
        }

        string joinedSuggestions = string.Join(", ", suggestions);

        return $"Species not found: '{trimmedName}' is not in this spreadsheet. "
            + $"This file contains: {joinedSuggestions}.";
    }

    private async Task OnTopCountChangedAsync(ChangeEventArgs eventArgs)
    {
        topCountError = null;

        string? rawValue = eventArgs.Value?.ToString();

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            topCountError = "Enter a whole number.";
            return;
        }

        bool isNumber = int.TryParse(
            rawValue,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out int requestedCount);

        if (!isNumber)
        {
            topCountError = "Enter a whole number, for example 3.";
            return;
        }

        topCount = ClampTopCount(requestedCount);

        await LoadCurrentSpreadsheetAsync();
    }

    private int ClampTopCount(int requestedCount)
    {
        if (requestedCount < 1)
        {
            return 1;
        }

        if (requestedCount > maximumTopCount)
        {
            return maximumTopCount;
        }

        return requestedCount;
    }
}
