using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Exercises the csv reader against the real <c>amr.csv</c> export and then
/// feeds the parsed document through the ranking service, so the assertions
/// cover the whole path from raw file to leaderboard.
/// </summary>
public class CsvSpreadsheetReaderTests
{
    [Fact]
    public void Read_TheRealSample_CountsOrganismsAntibioticsAndMeasurements()
    {
        string samplePath = SampleDataLocator.CsvSamplePath();

        SpreadsheetImportResult result = CsvSpreadsheetReader.Read(samplePath, TestLocalizerFactory.Localizer);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        int organismCount = document.OrganismNames.Count;
        int antibioticCount = document.AntibioticNames.Count;
        int measurementCount = document.Measurements.Count;

        Assert.Equal(54, organismCount);
        Assert.Equal(75, antibioticCount);

        // Every cell of the matrix is now a measurement, because a blank cell is
        // read as zero (Q14). The invariant is organisms x antibiotics, and the
        // previous count of 894 was the non-blank cells only.
        Assert.Equal(organismCount * antibioticCount, measurementCount);
        Assert.Equal(4050, measurementCount);
    }

    [Fact]
    public void Read_TheRealSample_DetectsTheAntibioticsAsRowsLayout()
    {
        string samplePath = SampleDataLocator.CsvSamplePath();

        SpreadsheetImportResult result = CsvSpreadsheetReader.Read(samplePath, TestLocalizerFactory.Localizer);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        SpreadsheetOrientation orientation = document.Orientation;

        Assert.Equal(SpreadsheetOrientation.AntibioticsAsRows, orientation);
        Assert.Equal(SpreadsheetFileFormat.Csv, document.FileFormat);
    }

    [Fact]
    public void Read_TheRealSample_NeedsNoAbbreviationMapping()
    {
        // The csv lists its antibiotics as complete names ("Amoxicillin",
        // "Amoxicillin/Clavulanic acid"), so requirement 8 has nothing to do for
        // this file. Nothing may be presented as awaiting a mapping.
        SpreadsheetImportResult result = CsvSpreadsheetReader.Read(
            SampleDataLocator.CsvSamplePath(),
            TestLocalizerFactory.Localizer);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        IReadOnlyList<string> antibioticNames = result.Document!.AntibioticNames;

        bool needsNoMapping = AntibioticNameClassifier.NeedsNoMapping(antibioticNames);

        Assert.True(needsNoMapping);
    }

    [Fact]
    public void RankWithinOrganism_TheRealSample_LeadsWithTheMostSusceptibleTestedAntibiotics()
    {
        // A blank cell is an untested reading, not a zero (Q14 revision), so the
        // drugs this species was never tested against sort after every measured
        // drug. The leaderboard leads with the highest susceptibility reading.
        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            RankRealSampleFor("Acinetobacter baumannii");

        AntibioticSusceptibility mostSusceptible = rankedAntibiotics[0];

        Assert.True(mostSusceptible.Value.IsMeasured);
        Assert.Equal("Polymyxin B / Polysorbate 80", mostSusceptible.AntibioticName);
        Assert.Equal(100.0, mostSusceptible.Value.Percent!.Value, tolerance: 1e-9);

        List<string> leadingNames = rankedAntibiotics
            .Take(3)
            .Select(susceptibility => susceptibility.AntibioticName)
            .ToList();

        Assert.Equal(["Polymyxin B / Polysorbate 80", "Anidulafungin", "Ampicillin/Sulbactam"], leadingNames);
    }

    [Fact]
    public void RankWithinOrganism_TheRealSample_PlacesUntestedDrugsAfterEveryTestedDrug()
    {
        // The untested drugs are still listed, so a reader who asks for more rows
        // than the report has tested drugs sees what was not reported rather than
        // a table that stops short. They must all sort below every measured drug.
        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            RankRealSampleFor("Acinetobacter baumannii");

        int firstUntestedIndex = rankedAntibiotics
            .ToList()
            .FindIndex(susceptibility => !susceptibility.Value.IsMeasured);

        bool everyMeasuredRowComesFirst = rankedAntibiotics
            .Take(firstUntestedIndex)
            .All(susceptibility => susceptibility.Value.IsMeasured);

        bool everyUntestedRowComesLast = rankedAntibiotics
            .Skip(firstUntestedIndex)
            .All(susceptibility => !susceptibility.Value.IsMeasured);

        Assert.True(everyMeasuredRowComesFirst);
        Assert.True(everyUntestedRowComesLast);
    }

    [Fact]
    public void RankWithinOrganism_TheRealSample_RanksTestedAntibioticsByTheirScore()
    {
        // Candida albicans has only four tested antigens in the sample, so the
        // ordering is a real ordering rather than a name tie, except for the
        // leading pair which share 100 and must fall back to name. This is
        // exactly the shape the reference implementation produces with its
        // descending top-N sort.
        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            RankRealSampleFor("Candida albicans");

        List<AntibioticSusceptibility> testedAntibiotics = rankedAntibiotics
            .Where(susceptibility => susceptibility.Value.IsMeasured)
            .ToList();

        List<string> testedNames = testedAntibiotics
            .Select(susceptibility => susceptibility.AntibioticName)
            .ToList();

        Assert.Equal(
            ["Caspofungin", "Fluconazole", "Voriconazole", "Amphotericin B"],
            testedNames);

        Assert.Equal(100.0, testedAntibiotics[0].Value.Percent!.Value, tolerance: 1e-9);
        Assert.Equal(100.0, testedAntibiotics[1].Value.Percent!.Value, tolerance: 1e-9);
        Assert.Equal(88.9, testedAntibiotics[2].Value.Percent!.Value, tolerance: 1e-9);

        // The untested antigens are listed after them and carry no percentage.
        int testedCount = testedAntibiotics.Count;

        AntibioticSusceptibility firstUntested = rankedAntibiotics[testedCount];

        Assert.False(firstUntested.Value.IsMeasured);
        Assert.Null(firstUntested.Value.Percent);
    }

    [Fact]
    public void RankWithinOrganism_TheRealSample_EveryAntibioticIsRankedForTheSpecies()
    {
        // The count is the document's antibiotic count, not the number that
        // happened to carry a value in the source file: untested drugs are
        // listed last rather than dropped.
        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            RankRealSampleFor("Acinetobacter baumannii");

        int rankedCount = rankedAntibiotics.Count;

        Assert.True(rankedCount > 0);

        int untestedCount = rankedAntibiotics
            .Count(susceptibility => !susceptibility.Value.IsMeasured);

        Assert.Equal(51, untestedCount);
        Assert.Equal(75, rankedCount);
    }

    [Fact]
    public void Read_AFileWithAnOutOfRangeValue_ReturnsFailureInsteadOfThrowing()
    {
        string temporaryPath = WriteTemporaryCsv(
            "Test.csv",
            "Organism,Organism one",
            "Amoxicillin,999");

        try
        {
            SpreadsheetImportResult result = CsvSpreadsheetReader.Read(temporaryPath, TestLocalizerFactory.Localizer);

            Assert.False(result.IsSuccess);
            Assert.Null(result.Document);
            Assert.Contains("999", result.ErrorMessage);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    [Fact]
    public void Read_AFileWithAnUnparseableValue_ReturnsFailureInsteadOfThrowing()
    {
        string temporaryPath = WriteTemporaryCsv(
            "Test.csv",
            "Organism,Organism one",
            "Amoxicillin,banana");

        try
        {
            SpreadsheetImportResult result = CsvSpreadsheetReader.Read(temporaryPath, TestLocalizerFactory.Localizer);

            Assert.False(result.IsSuccess);
            Assert.Null(result.Document);
            Assert.Contains("banana", result.ErrorMessage);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static IReadOnlyList<AntibioticSusceptibility> RankRealSampleFor(string organismName)
    {
        string samplePath = SampleDataLocator.CsvSamplePath();

        SpreadsheetImportResult result = CsvSpreadsheetReader.Read(samplePath, TestLocalizerFactory.Localizer);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, organismName);

        return rankedAntibiotics;
    }

    private static string WriteTemporaryCsv(string fileName, params string[] rows)
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(temporaryDirectory);

        string temporaryPath = Path.Combine(temporaryDirectory, fileName);

        File.WriteAllLines(temporaryPath, rows);

        return temporaryPath;
    }
}
