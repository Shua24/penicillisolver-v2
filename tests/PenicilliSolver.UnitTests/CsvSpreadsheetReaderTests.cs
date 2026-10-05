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

        SpreadsheetImportResult result = CsvSpreadsheetReader.Read(samplePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        int organismCount = document.OrganismNames.Count;
        int antibioticCount = document.AntibioticNames.Count;
        int measurementCount = document.Measurements.Count;

        Assert.Equal(54, organismCount);
        Assert.Equal(75, antibioticCount);
        Assert.Equal(894, measurementCount);
    }

    [Fact]
    public void Read_TheRealSample_DetectsTheAntibioticsAsRowsLayout()
    {
        string samplePath = SampleDataLocator.CsvSamplePath();

        SpreadsheetImportResult result = CsvSpreadsheetReader.Read(samplePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        SpreadsheetOrientation orientation = document.Orientation;

        Assert.Equal(SpreadsheetOrientation.AntibioticsAsRows, orientation);
        Assert.Equal(SpreadsheetFileFormat.Csv, document.FileFormat);
    }

    [Fact]
    public void RankByResistance_TheRealSample_OrdersTheFirstFourExactly()
    {
        IReadOnlyList<AntibioticResistance> rankedAntibiotics = RankRealSample();

        List<string> leadingNames = rankedAntibiotics
            .Take(4)
            .Select(resistance => resistance.AntibioticName)
            .ToList();

        Assert.Equal(
            ["Cefetamet", "Cefixime", "Ceftibuten", "Florfenicol"],
            leadingNames);
    }

    [Fact]
    public void RankByResistance_TheRealSample_OrdersRanksFiveToEightExactly()
    {
        IReadOnlyList<AntibioticResistance> rankedAntibiotics = RankRealSample();

        List<string> namesFiveToEight = rankedAntibiotics
            .Skip(4)
            .Take(4)
            .Select(resistance => resistance.AntibioticName)
            .ToList();

        Assert.Equal(
            ["Azlocilin", "Carbencilin", "Mezlocilin", "Ticarcillin"],
            namesFiveToEight);
    }

    [Fact]
    public void RankByResistance_TheRealSample_ReportsTheKnownLeadingMeans()
    {
        IReadOnlyList<AntibioticResistance> rankedAntibiotics = RankRealSample();

        double rankOneMean = rankedAntibiotics[0].MeanPercentSusceptible;
        double rankFiveMean = rankedAntibiotics[4].MeanPercentSusceptible;

        Assert.Equal(0.0, rankOneMean, tolerance: 1e-9);
        Assert.Equal(4.942857142857143, rankFiveMean, tolerance: 1e-9);
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
            SpreadsheetImportResult result = CsvSpreadsheetReader.Read(temporaryPath);

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
            SpreadsheetImportResult result = CsvSpreadsheetReader.Read(temporaryPath);

            Assert.False(result.IsSuccess);
            Assert.Null(result.Document);
            Assert.Contains("banana", result.ErrorMessage);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static IReadOnlyList<AntibioticResistance> RankRealSample()
    {
        string samplePath = SampleDataLocator.CsvSamplePath();

        SpreadsheetImportResult result = CsvSpreadsheetReader.Read(samplePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            AntibioticRankingService.RankByResistance(document);

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
