using penicillisolver_v2.Domain.Enums;

namespace penicillisolver_v2.Domain.Entities;

/// <summary>
/// The persisted record of one accepted spreadsheet upload. It stores where the
/// original file was kept, a content hash for duplicate detection, and a small
/// amount of summary information about what the file contained.
/// </summary>
public class SpreadsheetUpload
{
    /// <summary>The database generated primary key.</summary>
    public int Id { get; set; }

    /// <summary>The file name exactly as the uploader supplied it.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// The path, relative to the configured storage root, where the original
    /// file was written.
    /// </summary>
    public string StoredFilePath { get; set; } = string.Empty;

    /// <summary>
    /// A hash of the file contents. Two uploads with the same hash are the
    /// same file, regardless of their names.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>When the upload was accepted, in coordinated universal time.</summary>
    public DateTimeOffset UploadedAtUtc { get; set; }

    /// <summary>The identifier of the account that performed the upload.</summary>
    public string UploadedByUserId { get; set; } = string.Empty;

    /// <summary>Whether the source file was a comma separated file or a workbook.</summary>
    public SpreadsheetFileFormat FileFormat { get; set; }

    /// <summary>The layout that was detected inside the source file.</summary>
    public SpreadsheetOrientation Orientation { get; set; }

    /// <summary>How many organisms the file contained.</summary>
    public int OrganismCount { get; set; }

    /// <summary>How many antibiotics the file contained.</summary>
    public int AntibioticCount { get; set; }

    /// <summary>
    /// The abbreviation mappings that belong to this upload. Each upload owns
    /// its own set; a mapping is never shared between uploads.
    /// </summary>
    public ICollection<AntibioticAbbreviation> AntibioticAbbreviations { get; set; } =
        new List<AntibioticAbbreviation>();
}
