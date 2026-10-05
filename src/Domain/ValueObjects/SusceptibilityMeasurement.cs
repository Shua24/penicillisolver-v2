namespace penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// One susceptibility measurement: how a single organism responds to a single
/// antibiotic, expressed as a susceptibility reading that may be absent.
/// <para>
/// Every cell of the source matrix produces one of these, so the count of
/// measurements equals organisms x antibiotics for the file (Q14). A BLANK
/// source cell produces an UNTESTED reading rather than a measurement of zero
/// (Q14 revision): a drug the report never mentioned and a drug measured at
/// zero percent susceptible are different findings, and the ranking must be
/// able to tell them apart. See <see cref="SusceptibilityValue"/>.
/// </para>
/// </summary>
/// <param name="AntibioticName">The full display name of the antibiotic.</param>
/// <param name="OrganismName">The full display name of the organism.</param>
/// <param name="Value">The susceptibility reading for this cell, measured or untested.</param>
public sealed record SusceptibilityMeasurement(
    string AntibioticName,
    string OrganismName,
    SusceptibilityValue Value);
