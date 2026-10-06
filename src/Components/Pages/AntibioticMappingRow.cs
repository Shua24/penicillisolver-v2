using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Components.Pages;

/// <summary>
/// One editable row of the unified mapping table: a single abbreviation the
/// current file detected, with the meaning a pathologist is currently typing.
/// A row whose <see cref="Mapping"/> is null has not been mapped for this
/// upload yet, so its box starts empty.
/// </summary>
public sealed class AntibioticMappingRow
{
    /// <summary>The abbreviation exactly as detected in the file header.</summary>
    public string Abbreviation { get; init; } = string.Empty;

    /// <summary>The mapping this row edits, or null when the row is not mapped yet.</summary>
    public AntibioticAbbreviation? Mapping { get; init; }

    /// <summary>The identifier of the mapping this row edits, or null when unmapped.</summary>
    public int? MappingId => Mapping?.Id;

    /// <summary>The meaning stored in the database for this row; empty when unmapped.</summary>
    public string StoredFullName { get; init; } = string.Empty;

    /// <summary>What a pathologist is currently typing in this row's box.</summary>
    public string WorkingFullName { get; set; } = string.Empty;
}
