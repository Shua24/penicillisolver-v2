namespace penicillisolver_v2.Services;

using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Localization;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// Reads an xlsx workbook into a <see cref="SpreadsheetDocument"/>, using the
/// first worksheet only. ClosedXML exposes cells as typed values, so a numeric
/// susceptibility cell arrives as a number while the antibiotic header arrives
/// as text; both are read through their text form so one parsing rule covers
/// every cell.
/// </summary>
/// <remarks>
/// The two supported layouts are parsed by the halves of this partial class in
/// the sibling files <c>XlsxSpreadsheetReader.RowOriented.cs</c> and
/// <c>XlsxSpreadsheetReader.ColumnOriented.cs</c>.
/// </remarks>
public static partial class XlsxSpreadsheetReader
{
    /// <summary>
    /// Reads an xlsx file from disk and parses it into a document.
    /// </summary>
    /// <param name="originalFileName">The file name exactly as the uploader supplied it.</param>
    /// <returns>A result that either carries the document or explains the failure.</returns>
    public static SpreadsheetImportResult Read(string originalFileName, IStringLocalizer localizer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);

        try
        {
            // Opened through a stream on purpose. XLWorkbook(string) infers the
            // format from the file extension, and the upload pipeline stages
            // every file as "{guid}.incoming", so the path based constructor
            // rejected every xlsx upload with "Extension 'incoming' is not
            // supported" before it read a byte. A stream carries no extension to
            // guess from, so the staging name stops mattering.
            using FileStream fileStream = File.OpenRead(originalFileName);
            using XLWorkbook workbook = new XLWorkbook(fileStream);

            return ReadFirstWorksheet(workbook, originalFileName, localizer);
        }
        catch (IOException exception)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_FileCouldNotBeRead", originalFileName],
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_FileCouldNotBeOpened", originalFileName],
                exception);
        }
        catch (InvalidDataException exception)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_NotValidXlsxWorkbook", originalFileName],
                exception);
        }
        catch (FileFormatException exception)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_NotValidXlsxWorkbook", originalFileName],
                exception);
        }
    }

    /// <summary>
    /// Reads the first worksheet of an opened workbook into a document.
    /// </summary>
    private static SpreadsheetImportResult ReadFirstWorksheet(
        XLWorkbook workbook,
        string originalFileName,
        IStringLocalizer localizer)
    {
        if (workbook.Worksheets.Count == 0)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_WorkbookContainsNoWorksheet", originalFileName]);
        }

        IXLWorksheet worksheet = workbook.Worksheet(1);

        IXLRange? usedRange = worksheet.RangeUsed();

        if (usedRange is null)
        {
            return SpreadsheetImportResult.Failure(
                localizer["Service_XlsxWorksheetEmpty", worksheet.Name, originalFileName]);
        }

        int rowCount = usedRange.LastRow().RowNumber();
        int columnCount = usedRange.LastColumn().ColumnNumber();

        List<string> headerCells = ReadHeaderCells(worksheet, columnCount);

        SpreadsheetOrientation orientation = SpreadsheetOrientationDetector.Detect(headerCells);

        if (orientation == SpreadsheetOrientation.AntibioticsAsColumns)
        {
            return ParseColumnOriented(
                worksheet,
                originalFileName,
                orientation,
                rowCount,
                columnCount,
                headerCells,
                localizer);
        }

        return ParseRowOriented(
            worksheet,
            originalFileName,
            orientation,
            rowCount,
            columnCount,
            headerCells,
            localizer);
    }

    /// <summary>
    /// Reads the first row into a list of trimmed cell texts.
    /// </summary>
    private static List<string> ReadHeaderCells(IXLWorksheet worksheet, int columnCount)
    {
        List<string> headerCells = new List<string>();

        for (int columnIndex = 1; columnIndex <= columnCount; columnIndex++)
        {
            string cellText = ReadCellText(worksheet, 1, columnIndex);

            headerCells.Add(cellText);
        }

        return headerCells;
    }

    /// <summary>
    /// Reads one cell as trimmed display text, never null.
    /// </summary>
    private static string ReadCellText(
        IXLWorksheet worksheet,
        int rowIndex,
        int columnIndex)
    {
        IXLCell cell = worksheet.Cell(rowIndex, columnIndex);

        string cellText;

        if (cell.Value.IsBlank)
        {
            cellText = string.Empty;
        }
        else
        {
            cellText = cell.GetFormattedString();
        }

        string trimmedText = cellText.Trim();

        return trimmedText;
    }
}
