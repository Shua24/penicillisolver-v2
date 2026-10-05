using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers resolving a typed organism name against the names in a document.
/// </summary>
/// <remarks>
/// The point of this type is that a near miss must NOT be coerced onto the
/// closest organism. Ranking the wrong bacterium would be presented to a
/// clinician with the same confidence as a correct answer, so an unmatched name
/// has to fail loudly instead.
/// </remarks>
public class OrganismResolverTests
{
    private static readonly IReadOnlyList<string> SampleOrganisms =
    [
        "Acinetobacter baumannii",
        "Acinetobacter junii",
        "Citrobacter koseri (diversus)",
        "Enterobacter cloacae",
        "Staphylococcus aureus",
        "Staphylococcus epidermidis",
    ];

    [Fact]
    public void Resolve_ReturnsTheOrganismWhenTheNameMatchesExactly()
    {
        OrganismLookupResult lookupResult =
            OrganismResolver.Resolve(SampleOrganisms, "Acinetobacter baumannii");

        Assert.Equal(OrganismLookupOutcome.Resolved, lookupResult.Outcome);
        Assert.Equal("Acinetobacter baumannii", lookupResult.OrganismName);
    }

    [Fact]
    public void Resolve_MatchesAnExactNameIgnoringCaseAndSurroundingSpace()
    {
        OrganismLookupResult lookupResult =
            OrganismResolver.Resolve(SampleOrganisms, "  acinetobacter BAUMANNII ");

        Assert.Equal(OrganismLookupOutcome.Resolved, lookupResult.Outcome);
        Assert.Equal("Acinetobacter baumannii", lookupResult.OrganismName);
    }

    [Fact]
    public void Resolve_PrefersAnExactMatchOverPartialOnes()
    {
        // "Staphylococcus aureus" is a partial match for nothing else here, but
        // "Staphylococcus" is a prefix of two names. An exact hit must win
        // without the caller being asked to disambiguate.
        OrganismLookupResult lookupResult =
            OrganismResolver.Resolve(SampleOrganisms, "Staphylococcus aureus");

        Assert.Equal(OrganismLookupOutcome.Resolved, lookupResult.Outcome);
        Assert.Equal("Staphylococcus aureus", lookupResult.OrganismName);
    }

    [Fact]
    public void Resolve_ReportsAmbiguousForAPartialMatchEvenWhenOnlyOneNameContainsIt()
    {
        // Q16: partial input never decides on its own, not even with a single
        // candidate. "cloacae" could equally be a prefix of a longer name that
        // the user mistyped, so it is offered for confirmation rather than
        // silently resolved.
        OrganismLookupResult lookupResult =
            OrganismResolver.Resolve(SampleOrganisms, "cloacae");

        Assert.Equal(OrganismLookupOutcome.Ambiguous, lookupResult.Outcome);
        Assert.Null(lookupResult.OrganismName);
        Assert.Equal(["Enterobacter cloacae"], lookupResult.Candidates);
    }

    [Fact]
    public void Resolve_ReportsAmbiguousWhenSeveralNamesContainTheInput()
    {
        OrganismLookupResult lookupResult =
            OrganismResolver.Resolve(SampleOrganisms, "Staphylococcus");

        Assert.Equal(OrganismLookupOutcome.Ambiguous, lookupResult.Outcome);
        Assert.Null(lookupResult.OrganismName);
        Assert.Equal(2, lookupResult.Candidates.Count);
        Assert.Contains("Staphylococcus aureus", lookupResult.Candidates);
        Assert.Contains("Staphylococcus epidermidis", lookupResult.Candidates);
    }

    [Fact]
    public void Resolve_ReportsAmbiguousForAMisspellingThatIsASubstring()
    {
        // The motivating case (Q16). One missing letter makes the typo a
        // SUBSTRING of the real name, so a naive partial match would resolve it
        // and rank a species the user never named. It must be offered as a
        // candidate instead.
        OrganismLookupResult lookupResult =
            OrganismResolver.Resolve(SampleOrganisms, "Acinetobacter baumanni");

        Assert.Equal(OrganismLookupOutcome.Ambiguous, lookupResult.Outcome);
        Assert.Null(lookupResult.OrganismName);
        Assert.Equal(["Acinetobacter baumannii"], lookupResult.Candidates);
    }

    [Fact]
    public void Resolve_ReportsNotFoundForAnInputThatIsNotASubstring()
    {
        OrganismLookupResult lookupResult =
            OrganismResolver.Resolve(SampleOrganisms, "Klebsiella pneumoniae");

        Assert.Equal(OrganismLookupOutcome.NotFound, lookupResult.Outcome);
        Assert.Null(lookupResult.OrganismName);
    }

    [Fact]
    public void Resolve_ReportsNotFoundForAnEmptyInput()
    {
        OrganismLookupResult lookupResult = OrganismResolver.Resolve(SampleOrganisms, "   ");

        Assert.Equal(OrganismLookupOutcome.NotFound, lookupResult.Outcome);
        Assert.Null(lookupResult.OrganismName);
    }

    [Fact]
    public void Resolve_ReportsNotFoundForANullInput()
    {
        OrganismLookupResult lookupResult = OrganismResolver.Resolve(SampleOrganisms, null);

        Assert.Equal(OrganismLookupOutcome.NotFound, lookupResult.Outcome);
        Assert.Null(lookupResult.OrganismName);
    }

    [Fact]
    public void Resolve_OffersLeadingNamesWhenNothingMatches()
    {
        // With no substring hit there is no meaningful nearest name, so the
        // first few organisms are offered so the reader can see the shape of
        // the names in the file.
        OrganismLookupResult lookupResult =
            OrganismResolver.Resolve(SampleOrganisms, "zzzz");

        Assert.Equal(OrganismLookupOutcome.NotFound, lookupResult.Outcome);
        Assert.NotEmpty(lookupResult.Candidates);
        Assert.Equal("Acinetobacter baumannii", lookupResult.Candidates[0]);
    }

    [Fact]
    public void Resolve_ReportsNotFoundWhenTheDocumentHasNoOrganisms()
    {
        OrganismLookupResult lookupResult = OrganismResolver.Resolve([], "Anything");

        Assert.Equal(OrganismLookupOutcome.NotFound, lookupResult.Outcome);
        Assert.Null(lookupResult.OrganismName);
    }

    [Fact]
    public void Resolve_ReturnsAnEmptyCandidateListWhenResolved()
    {
        OrganismLookupResult lookupResult =
            OrganismResolver.Resolve(SampleOrganisms, "Enterobacter cloacae");

        Assert.Equal(OrganismLookupOutcome.Resolved, lookupResult.Outcome);
        Assert.Empty(lookupResult.Candidates);
    }
}
