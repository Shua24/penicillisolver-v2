using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;
using penicillisolver_v2.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Exercises the xlsx reader against the real <c>amr.xlsx</c> workbook and then
/// feeds the parsed document through the ranking service, proving that the
/// spreadsheet header text is preserved as the antibiotic display name.
/// </summary>
public class XlsxSpreadsheetReaderTests
{
    [Fact]
    public void Read_TheRealSample_CountsOrganismsAntibioticsAndMeasurements()
    {
        string samplePath = SampleDataLocator.XlsxSamplePath();

        SpreadsheetImportResult result = XlsxSpreadsheetReader.Read(samplePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        int organismCount = document.OrganismNames.Count;
        int antibioticCount = document.AntibioticNames.Count;
        int measurementCount = document.Measurements.Count;

        Assert.Equal(47, organismCount);
        Assert.Equal(44, antibioticCount);
        Assert.Equal(735, measurementCount);
    }

    [Fact]
    public void Read_TheRealSample_DetectsTheAntibioticsAsColumnsLayout()
    {
        string samplePath = SampleDataLocator.XlsxSamplePath();

        SpreadsheetImportResult result = XlsxSpreadsheetReader.Read(samplePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        SpreadsheetOrientation orientation = document.Orientation;

        Assert.Equal(SpreadsheetOrientation.AntibioticsAsColumns, orientation);
        Assert.Equal(SpreadsheetFileFormat.Xlsx, document.FileFormat);
    }

    [Fact]
    public void Read_TheRealSample_KeepsTheHeaderTextIncludingTheMarker()
    {
        string samplePath = SampleDataLocator.XlsxSamplePath();

        SpreadsheetImportResult result = XlsxSpreadsheetReader.Read(samplePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        string firstAntibioticName = document.AntibioticNames[0];

        Assert.Equal("AMK %S", firstAntibioticName);
    }

    [Fact]
    public void RankByResistance_TheRealSample_OrdersTheTopThreeExactly()
    {
        IReadOnlyList<AntibioticResistance> rankedAntibiotics = RankRealSample();

        List<string> topThreeNames = rankedAntibiotics
            .Take(3)
            .Select(resistance => resistance.AntibioticName)
            .ToList();

        Assert.Equal(["GAT %S", "CZO %S", "CXM %S"], topThreeNames);
    }

    [Fact]
    public void RankByResistance_TheRealSample_ReportsTheKnownTopThreeMeans()
    {
        IReadOnlyList<AntibioticResistance> rankedAntibiotics = RankRealSample();

        double firstMean = rankedAntibiotics[0].MeanPercentSusceptible;
        double secondMean = rankedAntibiotics[1].MeanPercentSusceptible;
        double thirdMean = rankedAntibiotics[2].MeanPercentSusceptible;

        Assert.Equal(0.0, firstMean, tolerance: 1e-9);
        Assert.Equal(1.1, secondMean, tolerance: 1e-9);
        Assert.Equal(8.25, thirdMean, tolerance: 1e-9);
    }

    private static IReadOnlyList<AntibioticResistance> RankRealSample()
    {
        string samplePath = SampleDataLocator.XlsxSamplePath();

        SpreadsheetImportResult result = XlsxSpreadsheetReader.Read(samplePath);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            AntibioticRankingService.RankByResistance(document);

        return rankedAntibiotics;
    }
}
