using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using penicillisolver_v2.Domain.Services;

namespace penicillisolver_v2.Components.Pages;

/// <summary>
/// The behaviour of the searchable organism picker on the resistance ranking
/// page: what it offers, what is keyboard highlighted, and whether it is open.
/// </summary>
/// <remarks>
/// <para>
/// Split out of <see cref="SpreadsheetViewer"/> so both files stay small and so
/// the picker can be reasoned about on its own. Everything here is about
/// CHOOSING a name; the page decides what to do with the chosen name, which is
/// to resolve it and rank against it.
/// </para>
/// <para>
/// The list is rendered by the page rather than delegated to a native
/// <c>&lt;datalist&gt;</c>, which matches by prefix and is styled differently by
/// every browser. That made the file's qualified names — <c>Staphylococcus
/// aureus ss. aureus</c> — unfindable by the distinctive part a clinician
/// actually types. See <see cref="OrganismNameFilter"/>.
/// </para>
/// <para>
/// Typing only FILTERS; it never resolves. Resolution is the page's job and
/// happens when a name is selected, so the ranking on screen always belongs to
/// the name the reader picked.
/// </para>
/// </remarks>
public partial class SpreadsheetViewer
{
    private IReadOnlyList<string> suggestions = [];
    private bool isPickerOpen;
    private int highlightedSuggestionIndex = -1;

    /// <summary>True while the list of matching organisms is showing.</summary>
    private bool PickerIsOpen => isPickerOpen && organismNames.Count > 0;

    /// <summary>
    /// Recomputes the offered names for whatever has been typed, and resets the
    /// keyboard highlight to the first row.
    /// </summary>
    private void RefreshSuggestions()
    {
        suggestions = OrganismNameFilter.Filter(organismNames, typedOrganismName);

        highlightedSuggestionIndex = suggestions.Count > 0 ? 0 : -1;
    }

    private void OnTypedOrganismChanged(ChangeEventArgs eventArgs)
    {
        typedOrganismName = eventArgs.Value?.ToString();

        // Typing narrows the list. A previously resolved organism is only kept
        // when the box still spells that exact name, so the ranking can never
        // sit on screen under a name it does not belong to.
        bool typedNameStillMatchesResolution = resolvedOrganismName is not null
            && string.Equals(
                resolvedOrganismName,
                typedOrganismName?.Trim(),
                StringComparison.OrdinalIgnoreCase);

        if (!typedNameStillMatchesResolution)
        {
            resolvedOrganismName = null;
            topResistant = [];
        }

        lookupMessage = null;
        ambiguousCandidates = [];

        RefreshSuggestions();
        isPickerOpen = true;
    }

    private void OnPickerFocused()
    {
        RefreshSuggestions();
        isPickerOpen = true;
    }

    private async Task OnTogglePickerAsync()
    {
        if (isPickerOpen)
        {
            isPickerOpen = false;

            return;
        }

        RefreshSuggestions();
        isPickerOpen = true;

        await Task.CompletedTask;
    }

    private async Task OnPickerKeyDown(KeyboardEventArgs eventArgs)
    {
        if (eventArgs.Key == "Escape")
        {
            isPickerOpen = false;

            return;
        }

        if (eventArgs.Key == "ArrowDown")
        {
            isPickerOpen = true;

            int nextIndex = highlightedSuggestionIndex + 1;

            if (nextIndex < suggestions.Count)
            {
                highlightedSuggestionIndex = nextIndex;
            }

            return;
        }

        if (eventArgs.Key == "ArrowUp")
        {
            int previousIndex = highlightedSuggestionIndex - 1;

            if (previousIndex >= 0)
            {
                highlightedSuggestionIndex = previousIndex;
            }

            return;
        }

        if (eventArgs.Key == "Enter")
        {
            await AcceptHighlightedSuggestionAsync();

            return;
        }

        if (eventArgs.Key == "Tab" && highlightedSuggestionIndex >= 0)
        {
            await AcceptHighlightedSuggestionAsync();
        }
    }

    /// <summary>
    /// Takes the currently highlighted suggestion, if there is one.
    /// </summary>
    private async Task AcceptHighlightedSuggestionAsync()
    {
        bool highlightedSuggestionExists = highlightedSuggestionIndex >= 0
            && highlightedSuggestionIndex < suggestions.Count;

        if (!highlightedSuggestionExists)
        {
            return;
        }

        string chosenName = suggestions[highlightedSuggestionIndex];

        await OnSuggestionChosenAsync(chosenName);
    }

    /// <summary>
    /// Resolves the organism the reader picked and shows its ranking.
    /// </summary>
    private async Task OnSuggestionChosenAsync(string organismName)
    {
        typedOrganismName = organismName;
        isPickerOpen = false;

        await ResolveAndRankAsync(organismName);
    }

    /// <summary>
    /// Reports whether a suggestion is the one the keyboard is currently on.
    /// </summary>
    private bool IsHighlightedSuggestion(string organismName)
    {
        bool highlightedSuggestionExists = highlightedSuggestionIndex >= 0
            && highlightedSuggestionIndex < suggestions.Count;

        if (!highlightedSuggestionExists)
        {
            return false;
        }

        return string.Equals(
            suggestions[highlightedSuggestionIndex],
            organismName,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The CSS class marking the keyboard highlighted row.
    /// </summary>
    private string HighlightClass(string organismName)
    {
        return IsHighlightedSuggestion(organismName) ? "active" : string.Empty;
    }
}
