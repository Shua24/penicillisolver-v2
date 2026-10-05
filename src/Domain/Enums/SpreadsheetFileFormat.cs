namespace penicillisolver_v2.Domain.Enums;

/// <summary>
/// The format of an uploaded spreadsheet file.
/// </summary>
public enum SpreadsheetFileFormat
{
    /// <summary>A comma separated values text file.</summary>
    Csv = 0,

    /// <summary>An Office Open XML spreadsheet workbook.</summary>
    Xlsx = 1,
}
