namespace penicillisolver_v2.Services;

using Microsoft.Extensions.Localization;
using OfficeOpenXml;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// The antibiotics-as-columns half of the xlsx reader: the header row carries
/// one antibiotic per column from the fourth onward, and each later row carries
/// an organism code, an organism name, an isolate count and then measurements.
/// </summary>
public static partial class XlsxSpreadsheetReader
{
    /// <summary>The one-based index of the column holding the organism short code.</summary>
    private const int OrganismCodeColumnIndex = 1;

    /// <summary>The one-based index of the column holding the organism display name.</summary>
    private const int OrganismNameColumnIndex = 2;

    /// <summary>The one-based index of the column holding the isolate count.</summary>
    private const int IsolateCountColumnIndex = 3;

    /// <summary>The one-based index of the first antibiotic column.</summary>
    private const int FirstAntibioticColumnIndex = 4;

    /// <summary>
    /// Parses the antibiotics-as-columns layout of a worksheet.
    /// </summary>
    private static SpreadsheetImportResult ParseColumnOriented(
        ExcelWorksheet worksheet,
        string originalFileName,
        SpreadsheetOrientation orientation,
        int rowCount,
        int columnCount,
        List<string> headerCells,
        IStringLocalizer localizer)
    {
        List<int> antibioticColumnIndexes = new List<int>();

        for (
            int columnIndex = FirstAntibioticColumnIndex;
            columnIndex <= columnCount;
            columnIndex++)
        {
            string antibioticName = headerCells[columnIndex - 1];

            if (antibioticName.Length > 0)
            {
                antibioticColumnIndexes.Add(columnIndex);
            }
        }

        if (antibioticColumnIndexes.Count == 0)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_XlsxNoAntibioticInHeader", worksheet.Name, originalFileName]);
        }

        List<string> antibioticNames = new List<string>();

        foreach (int antibioticColumnIndex in antibioticColumnIndexes)
        {
            antibioticNames.Add(headerCells[antibioticColumnIndex - 1]);
        }

        List<string> organismNames = new List<string>();
        List<SusceptibilityMeasurement> measurements = new List<SusceptibilityMeasurement>();

        for (int rowIndex = 2; rowIndex <= rowCount; rowIndex++)
        {
            string organismName = ReadCellText(worksheet, rowIndex, OrganismNameColumnIndex);

            if (organismName.Length == 0)
            {
                continue;
            }

            organismNames.Add(organismName);

            SpreadsheetImportResult? rowFailure = CollectColumnOrientedMeasurements(
                worksheet,
                rowIndex,
                organismName,
                antibioticColumnIndexes,
                antibioticNames,
                measurements,
                localizer);

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
    /// Reads one organism row into measurements, or reports the first cell that
    /// cannot be accepted.
    /// </summary>
    private static SpreadsheetImportResult? CollectColumnOrientedMeasurements(
        ExcelWorksheet worksheet,
        int rowIndex,
        string organismName,
        List<int> antibioticColumnIndexes,
        List<string> antibioticNames,
        List<SusceptibilityMeasurement> measurements,
        IStringLocalizer localizer)
    {
        for (int antibioticPosition = 0; antibioticPosition < antibioticColumnIndexes.Count; antibioticPosition++)
        {
            int columnIndex = antibioticColumnIndexes[antibioticPosition];

            string cellText = ReadCellText(worksheet, rowIndex, columnIndex);

            // A blank cell is NOT skipped: every matrix cell becomes a
            // measurement and a blank becomes an untested measurement (Q14
            // revision). See the remarks on ParseSusceptibilityValue for why a
            // missing value is now distinct from a measured zero, and why
            // malformed text is still an error.

            string antibioticName = antibioticNames[antibioticPosition];

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
