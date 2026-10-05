namespace penicillisolver_v2.Services;

/// <summary>
/// Maps a file name to the spreadsheet format that should be used to read it.
/// The lookup is deliberately extension based and case insensitive, because the
/// uploader supplies the name and casing is not meaningful on any platform the
/// application targets.
/// </summary>
public static class SpreadsheetFormatResolver
{
    private const string CsvExtension = ".csv";
    private const string XlsxExtension = ".xlsx";

    /// <summary>
    /// Resolves the file format implied by a file name.
    /// </summary>
    /// <param name="originalFileName">The file name exactly as the uploader supplied it.</param>
    /// <returns>The matching format, or null when the extension is unsupported.</returns>
    public static Domain.Enums.SpreadsheetFileFormat? Resolve(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return null;
        }

        string extension = Path.GetExtension(originalFileName);

        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        bool isCsv = extension.Equals(CsvExtension, StringComparison.OrdinalIgnoreCase);

        if (isCsv)
        {
            return Domain.Enums.SpreadsheetFileFormat.Csv;
        }

        bool isXlsx = extension.Equals(XlsxExtension, StringComparison.OrdinalIgnoreCase);

        if (isXlsx)
        {
            return Domain.Enums.SpreadsheetFileFormat.Xlsx;
        }

        return null;
    }
}
