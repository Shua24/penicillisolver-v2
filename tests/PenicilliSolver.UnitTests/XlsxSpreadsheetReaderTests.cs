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

        SpreadsheetImportResult result = XlsxSpreadsheetReader.Read(samplePath, TestLocalizerFactory.Localizer);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        int organismCount = document.OrganismNames.Count;
        int antibioticCount = document.AntibioticNames.Count;
        int measurementCount = document.Measurements.Count;

        Assert.Equal(47, organismCount);
        Assert.Equal(44, antibioticCount);

        // Every cell of the matrix is now a measurement, because a blank cell is
        // read as zero (Q14). The invariant is organisms x antibiotics, and the
        // previous count of 735 was the non-blank cells only.
        Assert.Equal(organismCount * antibioticCount, measurementCount);
        Assert.Equal(2068, measurementCount);
    }

    [Fact]
    public void Read_TheRealSample_DetectsTheAntibioticsAsColumnsLayout()
    {
        string samplePath = SampleDataLocator.XlsxSamplePath();

        SpreadsheetImportResult result = XlsxSpreadsheetReader.Read(samplePath, TestLocalizerFactory.Localizer);

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

        SpreadsheetImportResult result = XlsxSpreadsheetReader.Read(samplePath, TestLocalizerFactory.Localizer);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        string firstAntibioticName = document.AntibioticNames[0];

        Assert.Equal("AMK %S", firstAntibioticName);
    }

    [Fact]
    public void Read_TheRealSample_NeedsAbbreviationMapping()
    {
        // The mirror of the csv case: this file names its antibiotics as short
        // codes ("AMK %S"), so the mapping workflow genuinely applies.
        SpreadsheetImportResult result = XlsxSpreadsheetReader.Read(
            SampleDataLocator.XlsxSamplePath(),
            TestLocalizerFactory.Localizer);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        IReadOnlyList<string> antibioticNames = result.Document!.AntibioticNames;

        bool needsNoMapping = AntibioticNameClassifier.NeedsNoMapping(antibioticNames);

        Assert.False(needsNoMapping);
    }

    [Fact]
    public void RankWithinOrganism_TheRealSample_LeadsWithTheMostResistantTestedAntibiotics()
    {
        // A blank cell is an untested reading, not a zero (Q14 revision), so the
        // antigens this species was never tested against are no longer scored as
        // maximally resistant and the leaderboard leads with the tested drugs.
        // Acinetobacter baumannii's lowest reported value in this workbook is
        // 36 percent susceptible.
        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            RankRealSampleFor("Acinetobacter baumannii");

        List<string> leadingNames = rankedAntibiotics
            .Take(3)
            .Select(resistance => resistance.AntibioticName)
            .ToList();

        // Source column order is AMK, AMX, AMC, AMP, ... which is NOT
        // alphabetical. The leaders are the tested drugs, most resistant first:
        // the ones measured at zero percent susceptible, ordered by name.
        Assert.Equal(["AMP %S", "ATM %S", "CSL %S"], leadingNames);

        bool everyLeadingRowWasMeasured = rankedAntibiotics
            .Take(3)
            .All(resistance => resistance.Value.IsMeasured);

        Assert.True(everyLeadingRowWasMeasured);
    }

    [Fact]
    public void RankWithinOrganism_TheRealSample_PlacesUntestedDrugsAfterEveryTestedDrug()
    {
        // Untested antigens are listed after the measured ones rather than
        // dropped, so a reader still sees which drugs the workbook did not
        // report, but none of them is presented as resistant evidence.
        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            RankRealSampleFor("Acinetobacter baumannii");

        int firstUntestedIndex = rankedAntibiotics
            .ToList()
            .FindIndex(resistance => !resistance.Value.IsMeasured);

        bool everyMeasuredRowComesFirst = rankedAntibiotics
            .Take(firstUntestedIndex)
            .All(resistance => resistance.Value.IsMeasured);

        bool everyUntestedRowComesLast = rankedAntibiotics
            .Skip(firstUntestedIndex)
            .All(resistance => !resistance.Value.IsMeasured);

        Assert.True(everyMeasuredRowComesFirst);
        Assert.True(everyUntestedRowComesLast);
    }

    [Fact]
    public void RankWithinOrganism_TheRealSample_EveryAntibioticIsRankedForTheSpecies()
    {
        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            RankRealSampleFor("Acinetobacter baumannii");

        int rankedCount = rankedAntibiotics.Count;

        Assert.Equal(44, rankedCount);
    }

    [Fact]
    public void RankWithinOrganism_TheRealSample_RanksTestedAntibioticsAboveTheUntestedOnes()
    {
        // Acinetobacter baumannii has tested values of 0 and above in this file,
        // so every measured drug must sort ABOVE the untested antigens, and the
        // last measured row is the one with the highest percentage.
        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            RankRealSampleFor("Acinetobacter baumannii");

        AntibioticResistance firstTested = rankedAntibiotics
            .First(resistance => resistance.Value.IsMeasured);

        Assert.Equal(0.0, firstTested.Value.Percent!.Value, tolerance: 1e-9);

        int measuredCount = rankedAntibiotics
            .Count(resistance => resistance.Value.IsMeasured);

        bool everyMeasuredRowPrecedesTheUntestedOnes = rankedAntibiotics
            .Take(measuredCount)
            .All(resistance => resistance.Value.IsMeasured);

        Assert.True(everyMeasuredRowPrecedesTheUntestedOnes);
        Assert.Equal(25, measuredCount);
    }

    private static IReadOnlyList<AntibioticResistance> RankRealSampleFor(string organismName)
    {
        string samplePath = SampleDataLocator.XlsxSamplePath();

        SpreadsheetImportResult result = XlsxSpreadsheetReader.Read(samplePath, TestLocalizerFactory.Localizer);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        SpreadsheetDocument document = result.Document!;

        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            AntibioticRankingService.RankWithinOrganism(document, organismName);

        return rankedAntibiotics;
    }
}
