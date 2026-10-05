namespace penicillisolver_v2.Domain.Entities;

/// <summary>
/// One manually entered meaning for a single antibiotic abbreviation, scoped to
/// exactly one spreadsheet upload.
/// </summary>
/// <remarks>
/// The mapping is deliberately NOT global. The same abbreviation string can
/// stand for different drugs in different files, so the abbreviation is not a
/// unique key on its own; the unique key is the pair of the upload it belongs
/// to together with the abbreviation string. An abbreviation with no row here
/// for a given upload is simply unmapped for that upload, and the application
/// ships no built-in dictionary: the meanings are decided by the pathologists.
/// </remarks>
public class AntibioticAbbreviation
{
    /// <summary>The database generated primary key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The upload this mapping belongs to. The same abbreviation may be mapped
    /// to different meanings under different uploads.
    /// </summary>
    public int SpreadsheetUploadId { get; set; }

    /// <summary>The upload this mapping belongs to.</summary>
    public SpreadsheetUpload? SpreadsheetUpload { get; set; }

    /// <summary>
    /// The abbreviation exactly as it appears in the spreadsheet header, for
    /// example <c>GAT %S</c>. Matching is an exact ordinal string comparison.
    /// </summary>
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>
    /// The full name the abbreviation stands for, for example
    /// <c>Gatifloxacin</c>. Entered by hand; never seeded.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>The identifier of the account that created this mapping.</summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>When this mapping was created, in coordinated universal time.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>The identifier of the account that most recently changed this mapping.</summary>
    public string LastModifiedByUserId { get; set; } = string.Empty;

    /// <summary>When this mapping was most recently changed, in coordinated universal time.</summary>
    public DateTimeOffset LastModifiedAtUtc { get; set; }
}
