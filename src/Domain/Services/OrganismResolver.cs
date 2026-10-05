using penicillisolver_v2.Domain.ValueObjects;

namespace penicillisolver_v2.Domain.Services;

/// <summary>
/// Resolves a typed organism name against the organisms present in a document.
/// </summary>
/// <remarks>
/// This exists so the ranked view never guesses which organism was meant. The
/// caller types or picks a name; this type decides only whether that input
/// identifies exactly one organism, several, or none, and the page renders
/// accordingly.
/// <para>
/// ONLY AN EXACT, CASE-INSENSITIVE MATCH RESOLVES (Q16). Partial input does not
/// decide; it narrows the candidate list for the picker. The reason is that
/// substring matching cannot tell a legitimate prefix from a typo: "baumann" is
/// a useful prefix, but so is "baumanni", which is the correct name with a
/// letter missing. Treating either as a match would silently rank the wrong
/// organism, and a ranking presented for a species the user did not name is
/// worse than being asked to choose. Partial input therefore has exactly two
/// outcomes: several candidates to choose from, or not found.
/// </para>
/// </remarks>
public static class OrganismResolver
{
    private const int MaximumSuggestionCount = 5;

    /// <summary>
    /// Resolves a typed organism name against the supplied organism names.
    /// </summary>
    /// <param name="organismNames">Every organism present in the document, in source order.</param>
    /// <param name="typedName">What the user typed or selected.</param>
    /// <returns>The outcome, carrying either the single match, the candidates, or suggestions.</returns>
    public static OrganismLookupResult Resolve(
        IReadOnlyList<string> organismNames,
        string? typedName)
    {
        ArgumentNullException.ThrowIfNull(organismNames);

        string trimmedName = (typedName ?? string.Empty).Trim();

        if (trimmedName.Length == 0)
        {
            return OrganismLookupResult.NotFound(Suggestions(organismNames, string.Empty));
        }

        List<string> exactMatches = organismNames
            .Where(name => string.Equals(name, trimmedName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exactMatches.Count == 1)
        {
            return OrganismLookupResult.Resolved(exactMatches[0]);
        }

        List<string> partialMatches = organismNames
            .Where(name => name.Contains(trimmedName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (partialMatches.Count > 0)
        {
            // Notably this covers the single-candidate case as well: a lone
            // partial match is still presented for confirmation rather than
            // silently resolved, so a typo that happens to be a substring of one
            // real name is never ranked without the user picking it.
            return OrganismLookupResult.Ambiguous(partialMatches);
        }

        return OrganismLookupResult.NotFound(Suggestions(organismNames, trimmedName));
    }

    /// <summary>
    /// Offers up to five names to show alongside a not-found message.
    /// </summary>
    /// <remarks>
    /// When nothing matched by substring there is no meaningful "closest" name
    /// to rank, so the first few organisms in source order are offered instead.
    /// They are there so the reader can see the shape of the names in the file
    /// and retype, not to imply a recommendation.
    /// </remarks>
    private static IReadOnlyList<string> Suggestions(
        IReadOnlyList<string> organismNames,
        string trimmedName)
    {
        List<string> substringSuggestions = organismNames
            .Where(name => name.Contains(trimmedName, StringComparison.OrdinalIgnoreCase))
            .Take(MaximumSuggestionCount)
            .ToList();

        if (substringSuggestions.Count > 0)
        {
            return substringSuggestions;
        }

        List<string> leadingNames = organismNames
            .Take(MaximumSuggestionCount)
            .ToList();

        return leadingNames;
    }
}
