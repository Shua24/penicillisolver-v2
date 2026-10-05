namespace penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// Decides whether an antibiotic name in a spreadsheet is an abbreviation that
/// needs a human-supplied meaning.
/// </summary>
/// <remarks>
/// Not every file uses abbreviations. The csv sample lists its antibiotics as
/// complete names ("Amoxicillin", "Amoxicillin/Clavulanic acid"), so there is
/// nothing for a pathologist to decide and requirement 8 does not apply to it.
/// The xlsx sample lists them as short codes with a susceptibility marker
/// ("AMK %S"), which is exactly what needs interpreting.
/// <para>
/// The test is the SUSCEPTIBILITY MARKER rather than the file format. A csv that
/// arrives abbreviated would still need mapping, and an xlsx that arrives with
/// full names would not. Keying off the file extension would get both wrong, so
/// the decision is made from the name itself, using the same marker the
/// orientation detector already treats as the abbreviation signal.
/// </para>
/// </remarks>
public static class AntibioticNameClassifier
{
    /// <summary>
    /// The susceptibility marker that identifies an abbreviated antibiotic
    /// column, for example <c>AMK %S</c>.
    /// </summary>
    public const string SusceptibilityMarker = "%S";

    /// <summary>
    /// Reports whether a name is an abbreviation awaiting a mapped meaning.
    /// </summary>
    /// <param name="antibioticName">The name exactly as the file supplies it.</param>
    /// <returns>True when the name carries the susceptibility marker.</returns>
    public static bool IsAbbreviation(string? antibioticName)
    {
        if (string.IsNullOrWhiteSpace(antibioticName))
        {
            return false;
        }

        string trimmedName = antibioticName.Trim();

        bool markerIsPresent = trimmedName.Contains(
            SusceptibilityMarker,
            StringComparison.OrdinalIgnoreCase);

        return markerIsPresent;
    }

    /// <summary>
    /// Reports whether a file needs no mapping at all, because none of its
    /// antibiotic names is an abbreviation.
    /// </summary>
    /// <remarks>
    /// An empty list reports true: a file with no antibiotics has nothing to
    /// map, so a page should not offer the mapping workflow for it.
    /// </remarks>
    /// <param name="antibioticNames">Every antibiotic name present in the file.</param>
    /// <returns>True when not one name needs a mapping.</returns>
    public static bool NeedsNoMapping(IReadOnlyList<string> antibioticNames)
    {
        ArgumentNullException.ThrowIfNull(antibioticNames);

        bool anyAbbreviationPresent = antibioticNames.Any(IsAbbreviation);

        return !anyAbbreviationPresent;
    }
}
