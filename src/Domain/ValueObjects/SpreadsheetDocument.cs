using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Domain.ValueObjects;

namespace penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// A fully parsed and normalized spreadsheet. Both supported source layouts
/// collapse into this single shape, so everything downstream — ranking, display,
/// persistence — never needs to know which orientation the file used.
/// </summary>
public sealed class SpreadsheetDocument
{
    /// <summary>
    /// Creates a parsed spreadsheet document.
    /// </summary>
    /// <param name="originalFileName">The file name exactly as the uploader supplied it.</param>
    /// <param name="fileFormat">Whether the source was csv or xlsx.</param>
    /// <param name="orientation">The layout that was detected in the source file.</param>
    /// <param name="organismNames">Every organism present, in source order.</param>
    /// <param name="antibioticNames">Every antibiotic present, in source order.</param>
    /// <param name="measurements">Every non-blank measurement, in source order.</param>
    public SpreadsheetDocument(
        string originalFileName,
        SpreadsheetFileFormat fileFormat,
        SpreadsheetOrientation orientation,
        IReadOnlyList<string> organismNames,
        IReadOnlyList<string> antibioticNames,
        IReadOnlyList<SusceptibilityMeasurement> measurements)
    {
        OriginalFileName = originalFileName;
        FileFormat = fileFormat;
        Orientation = orientation;
        OrganismNames = organismNames;
        AntibioticNames = antibioticNames;
        Measurements = measurements;
    }

    /// <summary>The file name exactly as the uploader supplied it.</summary>
    public string OriginalFileName { get; }

    /// <summary>Whether the source was csv or xlsx.</summary>
    public SpreadsheetFileFormat FileFormat { get; }

    /// <summary>The layout that was detected in the source file.</summary>
    public SpreadsheetOrientation Orientation { get; }

    /// <summary>Every organism present, in source order.</summary>
    public IReadOnlyList<string> OrganismNames { get; }

    /// <summary>Every antibiotic present, in source order.</summary>
    public IReadOnlyList<string> AntibioticNames { get; }

    /// <summary>Every non-blank measurement, in source order. Blank cells contribute nothing.</summary>
    public IReadOnlyList<SusceptibilityMeasurement> Measurements { get; }
}
