using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers ranking within a single organism.
/// </summary>
/// <remarks>
/// These tests replace an earlier set that asserted a global mean across every
/// organism. That behaviour was removed deliberately: averaging made the result
/// depend on how many organisms happened to be tested, so a drug measured
/// against five organisms could outrank one measured against fifty. The rule is
/// now per organism, and every test below states which organism it ranks.
/// <para>
/// The second thing these tests pin down is that an UNTESTED reading is not a
/// zero. A blank cell carries no percentage at all, so it is never scored and
/// never competes with a measured drug; it is listed after every measured drug
/// instead.
/// </para>
/// </remarks>
public class AntibioticRankingServiceTests
{
    [Fact]
    public void RankWithinOrganism_PlacesTheHighestSusceptibilityFirst()
    {
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Broad", "Narrow"],
            measurements:
            [
                new SusceptibilityMeasurement("Broad", "Escherichia coli", SusceptibilityValue.Measured(90)),
                new SusceptibilityMeasurement("Narrow", "Escherichia coli", SusceptibilityValue.Measured(5)),
            ]);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, "Escherichia coli");

        string mostSusceptibleAntibioticName = rankedAntibiotics[0].AntibioticName;

        Assert.Equal("Broad", mostSusceptibleAntibioticName);
    }

    [Fact]
    public void RankWithinOrganism_UsesOnlyTheSelectedOrganismsValue()
    {
        // The old global rule averaged these into 50. The per-organism rule must
        // report the single value belonging to the organism asked for.
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Tested"],
            measurements:
            [
                new SusceptibilityMeasurement("Tested", "Organism one", SusceptibilityValue.Measured(0)),
                new SusceptibilityMeasurement("Tested", "Organism two", SusceptibilityValue.Measured(50)),
                new SusceptibilityMeasurement("Tested", "Organism three", SusceptibilityValue.Measured(100)),
            ]);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, "Organism three");

        double? susceptibilityForOrganismThree = rankedAntibiotics[0].Value.Percent;

        Assert.Equal(100, susceptibilityForOrganismThree!.Value, precision: 6);
    }

    [Fact]
    public void RankWithinOrganism_BreaksExactTiesByAntibioticName()
    {
        // Every antibiotic shares a score, so without an ordinal name tie-break
        // the order would be unstable across runs.
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Delta", "Alpha", "Charlie", "Bravo"],
            measurements:
            [
                new SusceptibilityMeasurement("Delta", "Organism one", SusceptibilityValue.Measured(0)),
                new SusceptibilityMeasurement("Alpha", "Organism one", SusceptibilityValue.Measured(0)),
                new SusceptibilityMeasurement("Charlie", "Organism one", SusceptibilityValue.Measured(0)),
                new SusceptibilityMeasurement("Bravo", "Organism one", SusceptibilityValue.Measured(0)),
            ]);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, "Organism one");

        List<string> orderedNames = rankedAntibiotics
            .Select(susceptibility => susceptibility.AntibioticName)
            .ToList();

        Assert.Equal(["Alpha", "Bravo", "Charlie", "Delta"], orderedNames);
    }

    [Fact]
    public void RankWithinOrganism_IncludesEveryAntibioticInTheDocument()
    {
        // Every antibiotic in the file is listed, including ones the file never
        // measured against this organism. The count is the document's antibiotic
        // count, not the measured count.
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Measured", "Zero"],
            measurements:
            [
                new SusceptibilityMeasurement("Measured", "Organism one", SusceptibilityValue.Measured(40)),
                new SusceptibilityMeasurement("Zero", "Organism one", SusceptibilityValue.Measured(0)),
            ]);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, "Organism one");

        int rankedCount = rankedAntibiotics.Count;

        Assert.Equal(2, rankedCount);

        // "Measured" scores higher, so it leads.
        Assert.Equal("Measured", rankedAntibiotics[0].AntibioticName);
    }

    [Fact]
    public void RankWithinOrganism_ScoresAnAntibioticWithNoValueAsUntested()
    {
        // A drug the file does not measure carries no percentage at all rather
        // than a zero, so it is not evidence of resistance.
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Present", "Absent"],
            measurements: [new SusceptibilityMeasurement("Present", "Organism one", SusceptibilityValue.Measured(70))]);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, "Organism one");

        AntibioticSusceptibility absentAntibiotic = rankedAntibiotics[0];

        Assert.Equal("Present", absentAntibiotic.AntibioticName);

        AntibioticSusceptibility untestedAntibiotic = rankedAntibiotics[1];

        Assert.Equal("Absent", untestedAntibiotic.AntibioticName);
        Assert.False(untestedAntibiotic.Value.IsMeasured);
        Assert.Null(untestedAntibiotic.Value.Percent);
    }

    [Fact]
    public void RankWithinOrganism_NeverRanksAnUntestedDrugAboveAMeasuredOne()
    {
        // The regression this whole change exists to fix: a blank cell used to
        // score zero rather than being left untested, so the untested drugs
        // were treated as if they held a value and competed with the measured
        // ones on the value axis.
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Aardvark", "Zebra", "Measured"],
            measurements:
            [
                new SusceptibilityMeasurement("Aardvark", "Organism one", SusceptibilityValue.Untested),
                new SusceptibilityMeasurement("Zebra", "Organism one", SusceptibilityValue.Untested),
                new SusceptibilityMeasurement("Measured", "Organism one", SusceptibilityValue.Measured(100)),
            ]);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, "Organism one");

        // The measured drug leads even when it is the one reading the file
        // reported: 100 percent susceptible is the highest value, so it tops
        // the measured group and the two untested drugs trail it in name order.
        string leaderName = rankedAntibiotics[0].AntibioticName;

        Assert.Equal("Measured", leaderName);

        List<string> trailingNames = rankedAntibiotics
            .Skip(1)
            .Select(susceptibility => susceptibility.AntibioticName)
            .ToList();

        Assert.Equal(["Aardvark", "Zebra"], trailingNames);
    }

    [Fact]
    public void RankWithinOrganism_ListsUntestedDrugsAfterTheMeasuredOnesInNameOrder()
    {
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Delta", "Alpha", "Charlie", "Bravo"],
            measurements:
            [
                new SusceptibilityMeasurement("Delta", "Organism one", SusceptibilityValue.Measured(30)),
                new SusceptibilityMeasurement("Alpha", "Organism one", SusceptibilityValue.Untested),
                new SusceptibilityMeasurement("Charlie", "Organism one", SusceptibilityValue.Measured(10)),
                new SusceptibilityMeasurement("Bravo", "Organism one", SusceptibilityValue.Untested),
            ]);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, "Organism one");

        List<string> orderedNames = rankedAntibiotics
            .Select(susceptibility => susceptibility.AntibioticName)
            .ToList();

        Assert.Equal(["Delta", "Charlie", "Alpha", "Bravo"], orderedNames);
    }

    [Fact]
    public void RankWithinOrganism_ReturnsEmptyWhenTheOrganismIsAbsent()
    {
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Only"],
            measurements: [new SusceptibilityMeasurement("Only", "Organism one", SusceptibilityValue.Measured(10))]);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, "Nothing like this");

        int rankedCount = rankedAntibiotics.Count;

        Assert.Equal(0, rankedCount);
    }

    [Fact]
    public void RankWithinOrganism_MatchesTheOrganismIgnoringCaseAndSurroundingSpace()
    {
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Only"],
            measurements: [new SusceptibilityMeasurement("Only", "Escherichia coli", SusceptibilityValue.Measured(10))]);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, "  escherichia COLI  ");

        int rankedCount = rankedAntibiotics.Count;

        Assert.Equal(1, rankedCount);
    }

    [Fact]
    public void RankWithinOrganism_ThrowsWhenTheDocumentIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AntibioticRankingService.RankWithinOrganism(null!, "Organism one"));
    }
}
