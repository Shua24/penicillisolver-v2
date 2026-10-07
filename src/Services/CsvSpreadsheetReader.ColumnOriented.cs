namespace penicillisolver_v2.Services;

using System.Globalization;
using Microsoft.Extensions.Localization;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// The antibiotics-as-columns half of the csv reader, plus the shared cell
/// parsing rules used by both orientations.
/// </summary>
public static partial class CsvSpreadsheetReader
{
    /// <summary>The label marking the isolate count row in a row oriented file.</summary>
    private const string IsolateCountRowLabel = "Number of isolates";

    /// <summary>The lowest susceptibility percentage a valid measurement may carry.</summary>
    private const double MinimumPercentSusceptible = 0.0;

    /// <summary>The highest susceptibility percentage a valid measurement may carry.</summary>
    private const double MaximumPercentSusceptible = 100.0;

    /// <summary>
    /// Parses the antibiotics-as-columns layout: antibiotics across the header,
    /// organisms down the first column.
    /// </summary>
    private static SpreadsheetImportResult ParseColumnOriented(
        string originalFileName,
        List<string[]> dataRows,
        SpreadsheetOrientation orientation,
        IStringLocalizer localizer)
    {
        string[] headerRow = dataRows[0];

        List<string> antibioticNames = new List<string>();

        for (int columnIndex = 1; columnIndex < headerRow.Length; columnIndex++)
        {
            string antibioticName = headerRow[columnIndex].Trim();

            if (antibioticName.Length > 0)
            {
                antibioticNames.Add(antibioticName);
            }
        }

        List<string> organismNames = new List<string>();
        List<SusceptibilityMeasurement> measurements = new List<SusceptibilityMeasurement>();

        for (int rowIndex = 1; rowIndex < dataRows.Count; rowIndex++)
        {
            string[] dataRow = dataRows[rowIndex];

            if (dataRow.Length == 0)
            {
                continue;
            }

            string organismName = dataRow[0].Trim();

            if (organismName.Length == 0)
            {
                continue;
            }

            organismNames.Add(organismName);

            SpreadsheetImportResult? rowFailure = CollectColumnOrientedMeasurements(
                dataRow,
                rowIndex,
                organismName,
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
            SpreadsheetFileFormat.Csv,
            orientation,
            organismNames,
            antibioticNames,
            measurements);

        return SpreadsheetImportResult.Success(document);
    }

    /// <summary>
    /// Reads one organism row into measurements in the column oriented layout.
    /// </summary>
    private static SpreadsheetImportResult? CollectColumnOrientedMeasurements(
        string[] dataRow,
        int rowIndex,
        string organismName,
        List<string> antibioticNames,
        List<SusceptibilityMeasurement> measurements,
        IStringLocalizer localizer)
    {
        for (int columnIndex = 1; columnIndex < dataRow.Length; columnIndex++)
        {
            string cellText = dataRow[columnIndex].Trim();

            // A blank cell is NOT skipped: every matrix cell becomes a
            // measurement and a blank becomes an untested measurement (Q14
            // revision). See the remarks on ParseSusceptibilityValue for why a
            // missing value is now distinct from a measured zero, and why
            // malformed text is still an error.

            int antibioticIndex = columnIndex - 1;

            bool antibioticIndexIsKnown = antibioticIndex >= 0
                && antibioticIndex < antibioticNames.Count;

            if (!antibioticIndexIsKnown)
            {
                continue;
            }

            string antibioticName = antibioticNames[antibioticIndex];

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

    /// <summary>
    /// Parses one cell value, appending a measurement when the value is valid.
    /// </summary>
    /// <remarks>
    /// A BLANK cell does produce a measurement, so every cell of the organism x
    /// antibiotic matrix is represented (Q14), but that measurement is UNTESTED
    /// rather than zero. The distinction matters clinically: a drug the file
    /// never reported and a drug measured at zero percent susceptible are not
    /// the same finding, and collapsing them made the leaderboard lead with
    /// drugs nobody had tested. See <see cref="SusceptibilityValue"/> for how the
    /// two cases are kept apart.
    /// <para>
    /// A value that is present but NOT a number, or that falls outside 0 to
    /// 100, remains an error rather than being coerced to a missing value.
    /// Silently turning malformed text into "not tested" would hide a corrupt
    /// file behind a plausible-looking panel, which is the one outcome worse
    /// than refusing the file.
    /// </para>
    /// </remarks>
    private static SpreadsheetImportResult? ParseSusceptibilityValue(
        string cellText,
        string antibioticName,
        string organismName,
        string cellReference,
        List<SusceptibilityMeasurement> measurements,
        IStringLocalizer localizer)
    {
        if (cellText.Length == 0)
        {
            SusceptibilityMeasurement untestedMeasurement = new SusceptibilityMeasurement(
                antibioticName,
                organismName,
                SusceptibilityValue.Untested);

            measurements.Add(untestedMeasurement);

            return null;
        }

        bool parsed = double.TryParse(
            cellText,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double parsedValue);

        if (!parsed)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_CellValueNotANumber", cellText, cellReference]);
        }

        bool isInRange = parsedValue >= MinimumPercentSusceptible
            && parsedValue <= MaximumPercentSusceptible;

        if (!isInRange)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_CellValueOutOfRange", cellText, cellReference]);
        }

        SusceptibilityMeasurement measurement = new SusceptibilityMeasurement(
            antibioticName,
            organismName,
            SusceptibilityValue.Measured(parsedValue));

        measurements.Add(measurement);

        return null;
    }

    /// <summary>
    /// Builds a human readable reference for a cell, using spreadsheet style
    /// one-based row and column numbers.
    /// </summary>
    private static string BuildCellReference(int rowIndex, int columnIndex)
    {
        int displayRow = rowIndex + 1;
        int displayColumn = columnIndex + 1;

        string reference = $"row {displayRow}, column {displayColumn}";

        return reference;
    }
}
