using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.Services;
using Xunit;

namespace PenicilliSolver.UnitTests;

public class SpreadsheetOrientationDetectorTests
{
    [Fact]
    public void Detect_RecognisesAntibioticsAsColumns()
    {
        // The real amr.xlsx header: three label columns, then one antibiotic per
        // column, each carrying a susceptibility marker.
        IReadOnlyList<string> headerCells =
        [
            "Org", "Organism", "Number of isolates",
            "AMK %S", "AMX %S", "AMC %S", "AMP %S", "SAM %S",
        ];

        SpreadsheetOrientation orientation = SpreadsheetOrientationDetector.Detect(headerCells);

        Assert.Equal(SpreadsheetOrientation.AntibioticsAsColumns, orientation);
    }

    [Fact]
    public void Detect_RecognisesAntibioticsAsRows()
    {
        // The real amr.csv header: the first cell is a label and the rest are
        // organism names, with no susceptibility marker anywhere.
        IReadOnlyList<string> headerCells =
        [
            "Organism",
            "Acinetobacter baumannii", "Aeromonas hydrophila", "Candida albicans",
        ];

        SpreadsheetOrientation orientation = SpreadsheetOrientationDetector.Detect(headerCells);

        Assert.Equal(SpreadsheetOrientation.AntibioticsAsRows, orientation);
    }

    [Fact]
    public void Detect_TreatsAMarkerInAnyCasingAsAMarker()
    {
        // Realistic antibiotics-as-columns header: labels first, then a clear
        // majority of marked antibiotic columns.
        IReadOnlyList<string> headerCells = ["Org", "Organism", "Number of isolates", "AMK %s", "AMX %s"];

        SpreadsheetOrientation orientation = SpreadsheetOrientationDetector.Detect(headerCells);

        Assert.Equal(SpreadsheetOrientation.AntibioticsAsColumns, orientation);
    }

    [Fact]
    public void Detect_TreatsAHeaderWithAnyMarkerAsAntibioticsAsColumns()
    {
        // A header with markers present is the antibiotics-as-columns layout
        // even when its label columns outnumber the antibiotic columns.
        IReadOnlyList<string> headerCells = ["Organism", "Organism two", "AMK %S", "AMX %S"];

        SpreadsheetOrientation orientation = SpreadsheetOrientationDetector.Detect(headerCells);

        Assert.Equal(SpreadsheetOrientation.AntibioticsAsColumns, orientation);
    }

    [Fact]
    public void Detect_FallsBackToAntibioticsAsRowsWhenTheHeaderIsEmpty()
    {
        IReadOnlyList<string> headerCells = ["", "", ""];

        SpreadsheetOrientation orientation = SpreadsheetOrientationDetector.Detect(headerCells);

        Assert.Equal(SpreadsheetOrientation.AntibioticsAsRows, orientation);
    }

    [Fact]
    public void Detect_IgnoresBlankCellsWhenCountingMarkers()
    {
        IReadOnlyList<string> headerCells = ["", "AMK %S", "AMX %S", "AMC %S"];

        SpreadsheetOrientation orientation = SpreadsheetOrientationDetector.Detect(headerCells);

        Assert.Equal(SpreadsheetOrientation.AntibioticsAsColumns, orientation);
    }
}
