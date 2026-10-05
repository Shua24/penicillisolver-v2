using penicillisolver_v2.Domain.ValueObjects;

namespace penicillisolver_v2.Domain.Services;

/// <summary>
/// Filters the organism names of a document down to the ones a reader is
/// probably looking for, for a searchable picker.
/// </summary>
/// <remarks>
/// <para>
/// This is a PICKER aid, not a resolver. It decides which names to offer; it
/// never decides which organism was meant. That decision stays with
/// <see cref="OrganismResolver"/>, which only accepts an exact match (Q16), and
/// keeping the two apart is deliberate: a filter that guessed would invite the
/// user to rank a species they never named.
/// </para>
/// <para>
/// Matching is case-insensitive and UNANCHORED, because the file's names are
/// long qualified ones such as <c>Staphylococcus aureus ss. aureus</c> and a
/// clinician usually types the distinctive tail — "aureus", not "Staph". An
/// anchored prefix filter would miss nearly everything they type.
/// </para>
/// <para>
/// Results are ordered so the most likely choice is first: names that START
/// with the query, then names that contain it as a whole word, then the rest,
/// and alphabetical within each group. The order is stable, so the list does
/// not reshuffle between keystrokes.
/// </para>
/// </remarks>
public static class OrganismNameFilter
{
    /// <summary>How many names the picker offers at once.</summary>
    public const int MaximumFilteredCount = 50;

    /// <summary>
    /// Filters organism names against what the reader has typed.
    /// </summary>
    /// <param name="organismNames">Every organism present in the document, in source order.</param>
    /// <param name="typedText">What the reader has typed so far. Blank offers everything, capped.</param>
    /// <returns>The matching names, most likely first, at most <see cref="MaximumFilteredCount"/> of them.</returns>
    public static IReadOnlyList<string> Filter(
        IReadOnlyList<string> organismNames,
        string? typedText)
    {
        ArgumentNullException.ThrowIfNull(organismNames);

        string trimmedText = (typedText ?? string.Empty).Trim();

        if (trimmedText.Length == 0)
        {
            List<string> firstNames = organismNames
                .Take(MaximumFilteredCount)
                .ToList();

            return firstNames;
        }

        List<string> matches = organismNames
            .Where(name => name.Contains(trimmedText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        List<string> orderedMatches = matches
            .OrderBy(name => RankMatch(name, trimmedText))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumFilteredCount)
            .ToList();

        return orderedMatches;
    }

    /// <summary>
    /// Scores how directly a name answers the typed text, lower being better.
    /// </summary>
    /// <remarks>
    /// A name that begins with the query is what the reader most likely means,
    /// then a name where the query starts a whole word, then anything else that
    /// contains it. The whole-word test is what lifts
    /// <c>Staphylococcus aureus ss. aureus</c> above a name that merely happens
    /// to contain the same letters mid-word.
    /// </remarks>
    private static int RankMatch(string organismName, string typedText)
    {
        bool startsWithQuery = organismName.StartsWith(
            typedText,
            StringComparison.OrdinalIgnoreCase);

        if (startsWithQuery)
        {
            return 0;
        }

        bool containsWholeWord = ContainsWholeWord(organismName, typedText);

        if (containsWholeWord)
        {
            return 1;
        }

        return 2;
    }

    /// <summary>
    /// Reports whether the typed text begins a word inside the name, where a
    /// word starts at the beginning or after a separator.
    /// </summary>
    private static bool ContainsWholeWord(string organismName, string typedText)
    {
        int searchStartIndex = 0;

        while (searchStartIndex < organismName.Length)
        {
            int foundIndex = organismName.IndexOf(
                typedText,
                searchStartIndex,
                StringComparison.OrdinalIgnoreCase);

            if (foundIndex < 0)
            {
                return false;
            }

            bool atStart = foundIndex == 0;

            bool afterSeparator = !atStart
                && !char.IsLetterOrDigit(organismName[foundIndex - 1]);

            if (atStart || afterSeparator)
            {
                return true;
            }

            searchStartIndex = foundIndex + 1;
        }

        return false;
    }
}
