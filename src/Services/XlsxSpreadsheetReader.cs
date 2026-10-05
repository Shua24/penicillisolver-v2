namespace penicillisolver_v2.Services;

using System.Globalization;
using OfficeOpenXml;
using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.Services;
using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// Reads an xlsx workbook into a <see cref="SpreadsheetDocument"/>, using the
/// first worksheet only. EPPlus exposes cells as typed values, so a numeric
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
    /// The licence attribution EPPlus records when a workbook is opened.
    /// </summary>
    private const string LicenseAttribution = "PenicilliSolver";

    /// <summary>
    /// Reads an xlsx file from disk and parses it into a document.
    /// </summary>
    /// <param name="originalFileName">The file name exactly as the uploader supplied it.</param>
    /// <returns>A result that either carries the document or explains the failure.</returns>
    public static SpreadsheetImportResult Read(string originalFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);

        ExcelPackage.License.SetNonCommercialPersonal(LicenseAttribution);

        try
        {
            FileInfo packageFile = new FileInfo(originalFileName);

            using ExcelPackage package = new ExcelPackage(packageFile);

            return ReadFirstWorksheet(package, originalFileName);
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
        catch (InvalidDataException exception)
        {
            return SpreadsheetImportResult.Failure(
                $"The file '{originalFileName}' is not a valid xlsx workbook: {exception.Message}");
        }
    }

    /// <summary>
    /// Reads the first worksheet of an opened workbook into a document.
    /// </summary>
    private static SpreadsheetImportResult ReadFirstWorksheet(
        ExcelPackage package,
        string originalFileName)
    {
        if (package.Workbook.Worksheets.Count == 0)
        {
            return SpreadsheetImportResult.Failure(
                $"The workbook '{originalFileName}' contains no worksheet.");
        }

        ExcelWorksheet worksheet = package.Workbook.Worksheets[0];

        if (worksheet.Dimension is null)
        {
            return SpreadsheetImportResult.Failure(
                $"The worksheet '{worksheet.Name}' in '{originalFileName}' is empty.");
        }

        int rowCount = worksheet.Dimension.End.Row;
        int columnCount = worksheet.Dimension.End.Column;

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
                headerCells);
        }

        return ParseRowOriented(
            worksheet,
            originalFileName,
            orientation,
            rowCount,
            columnCount,
            headerCells);
    }

    /// <summary>
    /// Reads the first row into a list of trimmed cell texts.
    /// </summary>
    private static List<string> ReadHeaderCells(ExcelWorksheet worksheet, int columnCount)
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
        ExcelWorksheet worksheet,
        int rowIndex,
        int columnIndex)
    {
        ExcelRange cell = worksheet.Cells[rowIndex, columnIndex];

        string cellText;

        if (cell.Value is null)
        {
            cellText = string.Empty;
        }
        else
        {
            cellText = cell.Text;
        }

        string trimmedText = cellText.Trim();

        return trimmedText;
    }
}
