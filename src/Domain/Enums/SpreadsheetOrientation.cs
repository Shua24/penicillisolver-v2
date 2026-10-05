namespace penicillisolver_v2.Domain.Enums;

/// <summary>
/// How the antibiotic measurements are laid out inside a spreadsheet. The two
/// supplied sample files use opposite layouts, so the importer detects this
/// before any values are read.
/// </summary>
public enum SpreadsheetOrientation
{
    /// <summary>Antibiotics are columns, organisms are rows. This is the amr.xlsx layout.</summary>
    AntibioticsAsColumns = 0,

    /// <summary>Antibiotics are rows, organisms are columns. This is the amr.csv layout.</summary>
    AntibioticsAsRows = 1,
}
