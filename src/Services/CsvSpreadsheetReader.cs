namespace penicillisolver_v2.Services;

using System.Globalization;
using Microsoft.VisualBasic.FileIO;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// Reads a comma separated spreadsheet into a <see cref="SpreadsheetDocument"/>.
/// Parsing is delegated to <see cref="TextFieldParser"/>, which understands
/// quoted fields and both CRLF and LF line endings, so an exported csv is not
/// rejected over incidental quoting.
/// </summary>
/// <remarks>
/// The two supported layouts are parsed by the halves of this partial class in
/// the sibling files <c>CsvSpreadsheetReader.RowOriented.cs</c> and
/// <c>CsvSpreadsheetReader.ColumnOriented.cs</c>. The shared entry point and
/// the shared cell parsing rules live here.
/// </remarks>
public static partial class CsvSpreadsheetReader
{
    /// <summary>
    /// Reads a csv file from disk and parses it into a document.
    /// </summary>
    /// <param name="originalFileName">The file name exactly as the uploader supplied it.</param>
    /// <returns>A result that either carries the document or explains the failure.</returns>
    public static SpreadsheetImportResult Read(string originalFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);

        List<string[]> dataRows;

        try
        {
            dataRows = ReadAllRows(originalFileName);
        }
        catch (IOException exception)
        {
            return SpreadsheetImportResult.Failure(
                $"The file '{originalFileName}' could not be read: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return SpreadsheetImportResult.Failure(
                $"The file '{originalFileName}' could not be opened: {exception.Message}");
        }
        catch (MalformedLineException exception)
        {
            return SpreadsheetImportResult.Failure(
                $"The file '{originalFileName}' contains a malformed line: {exception.Message}");
        }

        if (dataRows.Count == 0)
        {
            return SpreadsheetImportResult.Failure(
                $"The file '{originalFileName}' contains no rows.");
        }

        List<string> headerCells = new List<string>(dataRows[0]);

        SpreadsheetOrientation orientation = SpreadsheetOrientationDetector.Detect(headerCells);

        if (orientation == SpreadsheetOrientation.AntibioticsAsColumns)
        {
            return ParseColumnOriented(originalFileName, dataRows, orientation);
        }

        return ParseRowOriented(originalFileName, dataRows, orientation);
    }

    /// <summary>
    /// Reads every raw row of the file into memory, preserving cell text.
    /// </summary>
    private static List<string[]> ReadAllRows(string originalFileName)
    {
        List<string[]> dataRows = new List<string[]>();

        using TextFieldParser parser = new TextFieldParser(originalFileName);

        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = true;

        while (!parser.EndOfData)
        {
            string[]? fields = parser.ReadFields();

            if (fields is null)
            {
                continue;
            }

            dataRows.Add(fields);
        }

        return dataRows;
    }
}
