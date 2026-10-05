using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;
using Xunit;

namespace PenicilliSolver.UnitTests;

public class AntibioticRankingServiceTests
{
    [Fact]
    public void RankByResistance_PlacesTheLowestMeanSusceptibilityFirst()
    {
        SpreadsheetDocument document = BuildDocument(
            antibioticNames: ["Broad", "Narrow"],
            measurements:
            [
                new SusceptibilityMeasurement("Broad", "Escherichia coli", 90),
                new SusceptibilityMeasurement("Narrow", "Escherichia coli", 5),
            ]);

        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            AntibioticRankingService.RankByResistance(document);

        string mostResistantAntibioticName = rankedAntibiotics[0].AntibioticName;

        Assert.Equal("Narrow", mostResistantAntibioticName);
    }

    [Fact]
    public void RankByResistance_AveragesEveryMeasurementForOneAntibiotic()
    {
        SpreadsheetDocument document = BuildDocument(
            antibioticNames: ["Tested"],
            measurements:
            [
                new SusceptibilityMeasurement("Tested", "Organism one", 0),
                new SusceptibilityMeasurement("Tested", "Organism two", 50),
                new SusceptibilityMeasurement("Tested", "Organism three", 100),
            ]);

        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            AntibioticRankingService.RankByResistance(document);

        double meanSusceptibility = rankedAntibiotics[0].MeanPercentSusceptible;
        int measurementCount = rankedAntibiotics[0].MeasurementCount;

        Assert.Equal(50, meanSusceptibility, precision: 6);
        Assert.Equal(3, measurementCount);
    }

    [Fact]
    public void RankByResistance_BreaksExactTiesByAntibioticName()
    {
        // All four share a mean of exactly zero, so without an ordinal name
        // tie-break the order would be unstable across runs.
        SpreadsheetDocument document = BuildDocument(
            antibioticNames: ["Delta", "Alpha", "Charlie", "Bravo"],
            measurements:
            [
                new SusceptibilityMeasurement("Delta", "Organism one", 0),
                new SusceptibilityMeasurement("Alpha", "Organism one", 0),
                new SusceptibilityMeasurement("Charlie", "Organism one", 0),
                new SusceptibilityMeasurement("Bravo", "Organism one", 0),
            ]);

        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            AntibioticRankingService.RankByResistance(document);

        List<string> orderedNames = rankedAntibiotics
            .Select(resistance => resistance.AntibioticName)
            .ToList();

        Assert.Equal(["Alpha", "Bravo", "Charlie", "Delta"], orderedNames);
    }

    [Fact]
    public void RankByResistance_ExcludesAntibioticsThatHaveNoMeasurements()
    {
        SpreadsheetDocument document = BuildDocument(
            antibioticNames: ["Measured", "Untested"],
            measurements: [new SusceptibilityMeasurement("Measured", "Organism one", 40)]);

        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            AntibioticRankingService.RankByResistance(document);

        int rankedCount = rankedAntibiotics.Count;

        Assert.Equal(1, rankedCount);
        Assert.Equal("Measured", rankedAntibiotics[0].AntibioticName);
    }

    [Fact]
    public void GetAntibioticsWithoutMeasurements_ReportsTheUnmeasuredOnesSeparately()
    {
        SpreadsheetDocument document = BuildDocument(
            antibioticNames: ["Measured", "Untested"],
            measurements: [new SusceptibilityMeasurement("Measured", "Organism one", 40)]);

        IReadOnlyList<string> unmeasuredAntibioticNames =
            AntibioticRankingService.GetAntibioticsWithoutMeasurements(document);

        Assert.Equal(["Untested"], unmeasuredAntibioticNames);
    }

    [Fact]
    public void GetMostResistant_ReturnsOnlyTheRequestedNumberOfLeaders()
    {
        SpreadsheetDocument document = BuildDocument(
            antibioticNames: ["First", "Second", "Third"],
            measurements:
            [
                new SusceptibilityMeasurement("First", "Organism one", 10),
                new SusceptibilityMeasurement("Second", "Organism one", 20),
                new SusceptibilityMeasurement("Third", "Organism one", 30),
            ]);

        IReadOnlyList<AntibioticResistance> leadingAntibiotics =
            AntibioticRankingService.GetMostResistant(document, requestedCount: 2);

        Assert.Equal(2, leadingAntibiotics.Count);
        Assert.Equal("First", leadingAntibiotics[0].AntibioticName);
        Assert.Equal("Second", leadingAntibiotics[1].AntibioticName);
    }

    [Fact]
    public void GetMostResistant_DefaultsToThreeWhenThatIsWhatIsAskedFor()
    {
        SpreadsheetDocument document = BuildDocument(
            antibioticNames: ["First", "Second", "Third", "Fourth"],
            measurements:
            [
                new SusceptibilityMeasurement("First", "Organism one", 10),
                new SusceptibilityMeasurement("Second", "Organism one", 20),
                new SusceptibilityMeasurement("Third", "Organism one", 30),
                new SusceptibilityMeasurement("Fourth", "Organism one", 40),
            ]);

        IReadOnlyList<AntibioticResistance> leadingAntibiotics =
            AntibioticRankingService.GetMostResistant(document, requestedCount: 3);

        int leadingCount = leadingAntibiotics.Count;

        Assert.Equal(3, leadingCount);
    }

    [Fact]
    public void GetMostResistant_NeverReturnsMoreThanExist()
    {
        SpreadsheetDocument document = BuildDocument(
            antibioticNames: ["Only"],
            measurements: [new SusceptibilityMeasurement("Only", "Organism one", 10)]);

        IReadOnlyList<AntibioticResistance> leadingAntibiotics =
            AntibioticRankingService.GetMostResistant(document, requestedCount: 10);

        int leadingCount = leadingAntibiotics.Count;

        Assert.Equal(1, leadingCount);
    }

    private static SpreadsheetDocument BuildDocument(
        IReadOnlyList<string> antibioticNames,
        IReadOnlyList<SusceptibilityMeasurement> measurements)
    {
        SpreadsheetDocument document = new SpreadsheetDocument(
            originalFileName: "test.csv",
            fileFormat: SpreadsheetFileFormat.Csv,
            orientation: SpreadsheetOrientation.AntibioticsAsRows,
            organismNames: ["Organism one", "Organism two", "Organism three"],
            antibioticNames: antibioticNames,
            measurements: measurements);

        return document;
    }
}
