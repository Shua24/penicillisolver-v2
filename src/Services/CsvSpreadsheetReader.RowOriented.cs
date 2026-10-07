namespace penicillisolver_v2.Services;

using Microsoft.Extensions.Localization;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// The antibiotics-as-rows half of the csv reader: organisms across the header,
/// antibiotics down the first column, and the second row holding isolate counts
/// rather than measurements.
/// </summary>
public static partial class CsvSpreadsheetReader
{
    /// <summary>
    /// Parses the antibiotics-as-rows layout.
    /// </summary>
    private static SpreadsheetImportResult ParseRowOriented(
        string originalFileName,
        List<string[]> dataRows,
        SpreadsheetOrientation orientation,
        IStringLocalizer localizer)
    {
        if (dataRows.Count < 2)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_CsvMissingHeaderOrRows", originalFileName]);
        }

        string[] organismHeaderRow = dataRows[0];

        List<string> organismNames = new List<string>();

        for (int columnIndex = 1; columnIndex < organismHeaderRow.Length; columnIndex++)
        {
            string organismName = organismHeaderRow[columnIndex].Trim();

            if (organismName.Length > 0)
            {
                organismNames.Add(organismName);
            }
        }

        if (organismNames.Count == 0)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_CsvNoOrganismInHeader", originalFileName]);
        }

        int isolateCountRowIndex = FindIsolateCountRowIndex(dataRows);

        List<string> antibioticNames = new List<string>();
        List<SusceptibilityMeasurement> measurements = new List<SusceptibilityMeasurement>();

        for (int rowIndex = 1; rowIndex < dataRows.Count; rowIndex++)
        {
            if (rowIndex == isolateCountRowIndex)
            {
                continue;
            }

            string[] dataRow = dataRows[rowIndex];

            if (dataRow.Length == 0)
            {
                continue;
            }

            string antibioticName = dataRow[0].Trim();

            if (antibioticName.Length == 0)
            {
                continue;
            }

            antibioticNames.Add(antibioticName);

            SpreadsheetImportResult? rowFailure = CollectRowOrientedMeasurements(
                dataRow,
                rowIndex,
                antibioticName,
                organismNames,
                measurements,
                localizer);

            if (rowFailure is not null)
            {
                return rowFailure;
            }
        }

        SpreadsheetDocument document = new SpreadsheetDocument(
            originalFileName,
            SpreadsheetFileFormat.Csv,
            orientation,
            organismNames,
            antibioticNames,
            measurements);

        return SpreadsheetImportResult.Success(document);
    }

    /// <summary>
    /// Finds the index of the isolate count row, if the layout carries one.
    /// </summary>
    /// <remarks>
    /// The row is identified by its label in the first cell. When the label is
    /// absent the method returns -1, which keeps a plain organism by antibiotic
    /// csv importable instead of rejecting it for a row it never had.
    /// </remarks>
    private static int FindIsolateCountRowIndex(List<string[]> dataRows)
    {
        for (int rowIndex = 1; rowIndex < dataRows.Count; rowIndex++)
        {
            string[] dataRow = dataRows[rowIndex];

            if (dataRow.Length == 0)
            {
                continue;
            }

            string label = dataRow[0].Trim();

            bool isIsolateCountRow = label.Equals(
                IsolateCountRowLabel,
                StringComparison.OrdinalIgnoreCase);

            if (isIsolateCountRow)
            {
                return rowIndex;
            }
        }

        return -1;
    }

    /// <summary>
    /// Reads one antibiotic row into measurements, or reports the first cell
    /// that cannot be accepted.
    /// </summary>
    private static SpreadsheetImportResult? CollectRowOrientedMeasurements(
        string[] dataRow,
        int rowIndex,
        string antibioticName,
        List<string> organismNames,
        List<SusceptibilityMeasurement> measurements,
        IStringLocalizer localizer)
    {
        for (int columnIndex = 1; columnIndex < dataRow.Length; columnIndex++)
        {
            string cellText = dataRow[columnIndex].Trim();

            // A blank cell is NOT skipped: the settled rule (Q14) is that every
            // cell of the matrix becomes a measurement, and a blank becomes 0.
            // ParseSusceptibilityValue turns the empty string into an untested
            // measurement (Q14 revision); the malformed-text error path below is
            // untouched, so non-numeric text is still an import failure rather
            // than a missing value.

            int organismIndex = columnIndex - 1;

            bool organismIndexIsKnown = organismIndex >= 0
                && organismIndex < organismNames.Count;

            if (!organismIndexIsKnown)
            {
                continue;
            }

            string organismName = organismNames[organismIndex];

            string cellReference = BuildCellReference(rowIndex, columnIndex);

            SpreadsheetImportResult? cellFailure = ParseSusceptibilityValue(
                cellText,
                antibioticName,
                organismName,
                cellReference,
                measurements,
                localizer);

            if (cellFailure is not null)
            {
                return cellFailure;
            }
        }

        return null;
    }
}
