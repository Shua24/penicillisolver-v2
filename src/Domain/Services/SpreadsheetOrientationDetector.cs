using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Domain.Services;

/// <summary>
/// Works out which axis of a spreadsheet holds the antibiotics, by inspecting
/// the header row only. The two supported sample files use opposite layouts, so
/// this runs before any measurement is read.
/// </summary>
public static class SpreadsheetOrientationDetector
{
    /// <summary>
    /// Cells in the header of the antibiotics-as-columns layout carry a
    /// susceptibility marker, for example <c>AMK %S</c>. That marker is the
    /// signal used here. It is deliberately not keyed on a fixed list of
    /// antibiotic names, because the two sample files use entirely different
    /// antibiotic sets.
    /// </summary>
    private const string SusceptibilityMarker = "%S";

    /// <summary>
    /// Detects the layout from the cells of the first row.
    /// </summary>
    /// <remarks>
    /// The test is presence, not proportion. In the antibiotics-as-columns
    /// layout every antibiotic column is marked, so the header contains markers
    /// beside its three label cells. In the antibiotics-as-rows layout the
    /// header holds organism names and no cell is ever marked. A majority rule
    /// would misread a small workbook whose label columns outnumber its
    /// antibiotic columns, so the marker's presence alone is the signal.
    /// </remarks>
    /// <param name="headerCells">The text of every cell in the first row, left to right.</param>
    /// <returns>The detected orientation.</returns>
    public static SpreadsheetOrientation Detect(IReadOnlyList<string> headerCells)
    {
        ArgumentNullException.ThrowIfNull(headerCells);

        bool anyMarkerPresent = headerCells.Any(ContainsSusceptibilityMarker);

        if (anyMarkerPresent)
        {
            return SpreadsheetOrientation.AntibioticsAsColumns;
        }

        return SpreadsheetOrientation.AntibioticsAsRows;
    }

    /// <summary>
    /// Reports whether a single header cell carries the susceptibility marker.
    /// </summary>
    private static bool ContainsSusceptibilityMarker(string cellText)
    {
        if (string.IsNullOrWhiteSpace(cellText))
        {
            return false;
        }

        bool markerIsPresent = cellText.Contains(SusceptibilityMarker, StringComparison.OrdinalIgnoreCase);

        return markerIsPresent;
    }
}
