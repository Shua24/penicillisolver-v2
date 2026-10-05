using System.ComponentModel.DataAnnotations;

namespace penicillisolver_v2.Components.Pages;

/// <summary>
/// The create-mapping form's bound model. Validation here is limited to the
/// shape of the input; whether the abbreviation actually exists in the file and
/// whether it is a duplicate is decided by the service, which knows the upload.
/// </summary>
public sealed class CreateMappingInput
{
    /// <summary>The abbreviation exactly as it appears in the spreadsheet header.</summary>
    [Required(ErrorMessage = "Enter the abbreviation to map.")]
    [StringLength(100, ErrorMessage = "An abbreviation cannot be longer than 100 characters.")]
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>The full drug name the abbreviation stands for.</summary>
    [Required(ErrorMessage = "Enter the full name the abbreviation stands for.")]
    [StringLength(300, ErrorMessage = "A full name cannot be longer than 300 characters.")]
    public string FullName { get; set; } = string.Empty;
}
