namespace penicillisolver_v2.Services;

using System.Globalization;
using OfficeOpenXml;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// The antibiotics-as-rows half of the xlsx reader, plus the shared cell
/// parsing rules used by both orientations.
/// </summary>
public static partial class XlsxSpreadsheetReader
{
    /// <summary>The lowest susceptibility percentage a valid measurement may carry.</summary>
    private const double MinimumPercentSusceptible = 0.0;

    /// <summary>The highest susceptibility percentage a valid measurement may carry.</summary>
    private const double MaximumPercentSusceptible = 100.0;

    /// <summary>
    /// Parses the antibiotics-as-rows layout of a worksheet.
    /// </summary>
    private static SpreadsheetImportResult ParseRowOriented(
        ExcelWorksheet worksheet,
        string originalFileName,
        SpreadsheetOrientation orientation,
        int rowCount,
        int columnCount,
        List<string> headerCells)
    {
        List<int> organismColumnIndexes = new List<int>();

        for (int columnIndex = 2; columnIndex <= columnCount; columnIndex++)
        {
            string organismName = headerCells[columnIndex - 1];

            if (organismName.Length > 0)
            {
                organismColumnIndexes.Add(columnIndex);
            }
        }

        if (organismColumnIndexes.Count == 0)
        {
            return SpreadsheetImportResult.Failure(
                $"The worksheet '{worksheet.Name}' in '{originalFileName}' does not name any organism in its header row.");
        }

        List<string> organismNames = new List<string>();

        foreach (int organismColumnIndex in organismColumnIndexes)
        {
            organismNames.Add(headerCells[organismColumnIndex - 1]);
        }

        List<string> antibioticNames = new List<string>();
        List<SusceptibilityMeasurement> measurements = new List<SusceptibilityMeasurement>();

        for (int rowIndex = 2; rowIndex <= rowCount; rowIndex++)
        {
            string antibioticName = ReadCellText(worksheet, rowIndex, 1);

            if (antibioticName.Length == 0)
            {
                continue;
            }

            antibioticNames.Add(antibioticName);

            SpreadsheetImportResult? rowFailure = CollectRowOrientedMeasurements(
                worksheet,
                rowIndex,
                antibioticName,
                organismColumnIndexes,
                organismNames,
                measurements);

            if (rowFailure is not null)
            {
                return rowFailure;
            }
        }

        SpreadsheetDocument document = new SpreadsheetDocument(
            originalFileName,
            SpreadsheetFileFormat.Xlsx,
            orientation,
            organismNames,
            antibioticNames,
            measurements);

        return SpreadsheetImportResult.Success(document);
    }

    /// <summary>
    /// Reads one antibiotic row into measurements, or reports the first cell
    /// that cannot be accepted.
    /// </summary>
    private static SpreadsheetImportResult? CollectRowOrientedMeasurements(
        ExcelWorksheet worksheet,
        int rowIndex,
        string antibioticName,
        List<int> organismColumnIndexes,
        List<string> organismNames,
        List<SusceptibilityMeasurement> measurements)
    {
        for (int organismPosition = 0; organismPosition < organismColumnIndexes.Count; organismPosition++)
        {
            int columnIndex = organismColumnIndexes[organismPosition];

            string cellText = ReadCellText(worksheet, rowIndex, columnIndex);

            if (cellText.Length == 0)
            {
                continue;
            }

            string organismName = organismNames[organismPosition];

            string cellReference = BuildCellReference(rowIndex, columnIndex);

            SpreadsheetImportResult? cellFailure = ParseSusceptibilityValue(
                cellText,
                antibioticName,
                organismName,
                cellReference,
                measurements);

            if (cellFailure is not null)
            {
                return cellFailure;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses one cell value, appending a measurement when the value is valid.
    /// </summary>
    /// <remarks>
    /// A blank cell is never passed here, because blank means untested rather
    /// than zero. A value that is present but not a number, or that falls
    /// outside 0 to 100, is an error rather than a silently dropped measurement.
    /// </remarks>
    private static SpreadsheetImportResult? ParseSusceptibilityValue(
        string cellText,
        string antibioticName,
        string organismName,
        string cellReference,
        List<SusceptibilityMeasurement> measurements)
    {
        bool parsed = double.TryParse(
            cellText,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double parsedValue);

        if (!parsed)
        {
            return SpreadsheetImportResult.Failure(
                $"The value '{cellText}' at {cellReference} is not a number.");
        }

        bool isInRange = parsedValue >= MinimumPercentSusceptible
            && parsedValue <= MaximumPercentSusceptible;

        if (!isInRange)
        {
            return SpreadsheetImportResult.Failure(
                $"The value '{cellText}' at {cellReference} is outside the permitted range of 0 to 100.");
        }

        SusceptibilityMeasurement measurement = new SusceptibilityMeasurement(
            antibioticName,
            organismName,
            parsedValue);

        measurements.Add(measurement);

        return null;
    }

    /// <summary>
    /// Builds the spreadsheet style address of a cell, such as <c>D12</c>.
    /// </summary>
    private static string BuildCellReference(int rowIndex, int columnIndex)
    {
        string columnName = ExcelCellAddress.GetColumnLetter(columnIndex);

        string cellReference = $"{columnName}{rowIndex}";

        return cellReference;
    }
}
