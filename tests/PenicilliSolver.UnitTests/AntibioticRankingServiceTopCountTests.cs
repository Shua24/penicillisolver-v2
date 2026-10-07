using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers the top-N entry point: how many rows it returns, how it behaves at
/// the edges of the requested count, and how it treats drugs the file never
/// measured.
/// </summary>
/// <remarks>
/// Split from <see cref="AntibioticRankingServiceTests"/>, which covers the
/// full ordering. Both build their documents through
/// <see cref="RankingTestDocument"/>.
/// </remarks>
public class AntibioticRankingServiceTopCountTests
{
    [Fact]
    public void GetMostSusceptibleWithinOrganism_ReturnsOnlyTheRequestedNumberOfLeaders()
    {
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["First", "Second", "Third"],
            measurements:
            [
                new SusceptibilityMeasurement("First", "Organism one", SusceptibilityValue.Measured(10)),
                new SusceptibilityMeasurement("Second", "Organism one", SusceptibilityValue.Measured(20)),
                new SusceptibilityMeasurement("Third", "Organism one", SusceptibilityValue.Measured(30)),
            ]);

        IReadOnlyList<AntibioticSusceptibility> leadingAntibiotics =
            AntibioticRankingService.GetMostSusceptibleWithinOrganism(
                document,
                "Organism one",
                requestedCount: 2);

        Assert.Equal(2, leadingAntibiotics.Count);
        Assert.Equal("Third", leadingAntibiotics[0].AntibioticName);
        Assert.Equal("Second", leadingAntibiotics[1].AntibioticName);
    }

    [Fact]
    public void GetMostSusceptibleWithinOrganism_FillsTheRequestFromUntestedDrugsWhenTooFewWereMeasured()
    {
        // Asking for more rows than the report has tested drugs still returns
        // that many rows, ending with the untested ones, rather than a table
        // that silently stops short of the number the user typed.
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Only", "Alpha", "Bravo"],
            measurements: [new SusceptibilityMeasurement("Only", "Organism one", SusceptibilityValue.Measured(10))]);

        IReadOnlyList<AntibioticSusceptibility> leadingAntibiotics =
            AntibioticRankingService.GetMostSusceptibleWithinOrganism(
                document,
                "Organism one",
                requestedCount: 3);

        Assert.Equal(3, leadingAntibiotics.Count);

        List<string> orderedNames = leadingAntibiotics
            .Select(susceptibility => susceptibility.AntibioticName)
            .ToList();

        Assert.Equal(["Only", "Alpha", "Bravo"], orderedNames);
    }

    [Fact]
    public void GetMostSusceptibleWithinOrganism_ClampsACountBelowOneToOne()
    {
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["First", "Second"],
            measurements:
            [
                new SusceptibilityMeasurement("First", "Organism one", SusceptibilityValue.Measured(10)),
                new SusceptibilityMeasurement("Second", "Organism one", SusceptibilityValue.Measured(20)),
            ]);

        IReadOnlyList<AntibioticSusceptibility> leadingAntibiotics =
            AntibioticRankingService.GetMostSusceptibleWithinOrganism(
                document,
                "Organism one",
                requestedCount: 0);

        int leadingCount = leadingAntibiotics.Count;

        Assert.Equal(1, leadingCount);
    }

    [Fact]
    public void GetMostSusceptibleWithinOrganism_NeverReturnsMoreThanExist()
    {
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Only"],
            measurements: [new SusceptibilityMeasurement("Only", "Organism one", SusceptibilityValue.Measured(10))]);

        IReadOnlyList<AntibioticSusceptibility> leadingAntibiotics =
            AntibioticRankingService.GetMostSusceptibleWithinOrganism(
                document,
                "Organism one",
                requestedCount: 10);

        int leadingCount = leadingAntibiotics.Count;

        Assert.Equal(1, leadingCount);
    }

    [Fact]
    public void GetMostSusceptibleWithinOrganism_ReturnsNothingForAnAbsentOrganism()
    {
        SpreadsheetDocument document = RankingTestDocument.Build(
            antibioticNames: ["Only"],
            measurements: [new SusceptibilityMeasurement("Only", "Organism one", SusceptibilityValue.Measured(10))]);

        IReadOnlyList<AntibioticSusceptibility> leadingAntibiotics =
            AntibioticRankingService.GetMostSusceptibleWithinOrganism(
                document,
                "Nothing like this",
                requestedCount: 3);

        int leadingCount = leadingAntibiotics.Count;

        Assert.Equal(0, leadingCount);
    }

    [Fact]
    public void GetMostSusceptibleWithinOrganism_ThrowsWhenTheDocumentIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AntibioticRankingService.GetMostSusceptibleWithinOrganism(null!, "Organism one", 3));
    }
}
