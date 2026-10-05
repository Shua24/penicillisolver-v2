using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Services;
using penicillisolver_v2.Domain.ValueObjects;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers the picker's filter against the real <c>amr.csv</c> organism list, so
/// the search behaves the way a clinician actually types: by the distinctive
/// part of a long qualified name, not by its first word.
/// </summary>
public class OrganismNameFilterTests
{
    [Fact]
    public void Filter_WithBlankInput_OffersTheNamesFromTheStartOfTheList()
    {
        IReadOnlyList<string> organismNames = LoadRealOrganismNames();

        IReadOnlyList<string> filtered = OrganismNameFilter.Filter(organismNames, string.Empty);

        Assert.Equal(organismNames[0], filtered[0]);
        Assert.True(filtered.Count <= OrganismNameFilter.MaximumFilteredCount);
    }

    [Fact]
    public void Filter_MatchesAnywhereInTheNameNotJustTheStart()
    {
        // The real reason the old datalist was unusable: the file's names are
        // qualified, so the word a clinician types is rarely the first one.
        IReadOnlyList<string> organismNames = LoadRealOrganismNames();

        IReadOnlyList<string> filtered = OrganismNameFilter.Filter(organismNames, "aureus");

        Assert.NotEmpty(filtered);

        bool everyMatchContainsTheQuery = filtered
            .All(name => name.Contains("aureus", StringComparison.OrdinalIgnoreCase));

        Assert.True(everyMatchContainsTheQuery);

        Assert.Contains("Staphylococcus aureus ss. aureus", filtered);
    }

    [Fact]
    public void Filter_PutsNamesStartingWithTheQueryBeforeNamesThatMerelyContainIt()
    {
        // "Pseudomonas" starts with the query. "Campylobacter" only contains it
        // mid-word, so it must rank lower even though it is alphabetically
        // first.
        IReadOnlyList<string> organismNames =
        [
            "Campylobacter jejuni",
            "Pseudomonas aeruginosa",
            "Escherichia coli",
        ];

        IReadOnlyList<string> filtered = OrganismNameFilter.Filter(organismNames, "p");

        List<string> filteredList = filtered.ToList();

        int pseudomonasIndex = filteredList.IndexOf("Pseudomonas aeruginosa");
        int campylobacterIndex = filteredList.IndexOf("Campylobacter jejuni");

        // "Escherichia coli" contains no "p" at all and must be absent.
        int escherichiaIndex = filteredList.IndexOf("Escherichia coli");

        Assert.True(pseudomonasIndex >= 0);
        Assert.True(campylobacterIndex >= 0);
        Assert.True(pseudomonasIndex < campylobacterIndex);
        Assert.Equal(-1, escherichiaIndex);
    }

    [Fact]
    public void Filter_IsCaseInsensitive()
    {
        IReadOnlyList<string> organismNames = LoadRealOrganismNames();

        IReadOnlyList<string> lowerCase = OrganismNameFilter.Filter(organismNames, "escherichia");
        IReadOnlyList<string> upperCase = OrganismNameFilter.Filter(organismNames, "ESCHERICHIA");

        Assert.Equal(lowerCase, upperCase);
        Assert.Contains("Escherichia coli", lowerCase);
    }

    [Fact]
    public void Filter_IgnoresSurroundingSpaceInTheQuery()
    {
        IReadOnlyList<string> organismNames = LoadRealOrganismNames();

        IReadOnlyList<string> filtered = OrganismNameFilter.Filter(organismNames, "  coli  ");

        Assert.Contains("Escherichia coli", filtered);
    }

    [Fact]
    public void Filter_ReturnsNothingWhenNoNameMatches()
    {
        IReadOnlyList<string> organismNames = LoadRealOrganismNames();

        IReadOnlyList<string> filtered = OrganismNameFilter.Filter(organismNames, "zzzzzz");

        Assert.Empty(filtered);
    }

    [Fact]
    public void Filter_CapsTheOfferedNames()
    {
        List<string> manyNames = new List<string>();

        for (int index = 0; index < 200; index++)
        {
            manyNames.Add($"Organism number {index}");
        }

        IReadOnlyList<string> filtered = OrganismNameFilter.Filter(manyNames, "organism");

        Assert.Equal(OrganismNameFilter.MaximumFilteredCount, filtered.Count);
    }

    [Fact]
    public void Filter_ThrowsWhenTheNamesAreNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            OrganismNameFilter.Filter(null!, "anything"));
    }

    [Fact]
    public void Filter_OffersNamesThatTheResolverWillThenAccept()
    {
        // The filter and the resolver must agree: everything the picker offers
        // has to be a name the resolver accepts unchanged, or picking a
        // suggestion would produce "not found" for a name the picker just
        // showed. This is the seam the two types share.
        IReadOnlyList<string> organismNames = LoadRealOrganismNames();

        IReadOnlyList<string> filtered = OrganismNameFilter.Filter(organismNames, "staphylococcus");

        Assert.NotEmpty(filtered);

        foreach (string offeredName in filtered)
        {
            OrganismLookupResult lookupResult =
                OrganismResolver.Resolve(organismNames, offeredName);

            Assert.Equal(OrganismLookupOutcome.Resolved, lookupResult.Outcome);
        }
    }

    private static IReadOnlyList<string> LoadRealOrganismNames()
    {
        string samplePath = SampleDataLocator.CsvSamplePath();

        SpreadsheetImportResult importResult = CsvSpreadsheetReader.Read(samplePath);

        Assert.True(importResult.IsSuccess, importResult.ErrorMessage);

        return importResult.Document!.OrganismNames;
    }
}
