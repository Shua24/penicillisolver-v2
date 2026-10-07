namespace penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// One row of the susceptibility leaderboard: an antibiotic together with the
/// susceptibility reading the file reported for ONE organism.
/// <para>
/// The higher the percentage, the more susceptible that organism is to the
/// antibiotic, so the most susceptible antibiotics sort first. The reading is
/// the single value reported for that organism, not a mean across organisms: the
/// ranking is always scoped to one species.
/// </para>
/// <para>
/// The reading may be <see cref="SusceptibilityValue.Untested"/>, which is what
/// a file that never reported the drug against this organism yields. An
/// untested row carries no percentage at all and therefore cannot be compared
/// with a measured one; it can still be LISTED, so a reader who asks for more
/// rows than the file has tested drugs gets the remaining drugs named rather
/// than a table that silently stops short.
/// </para>
/// </summary>
/// <param name="AntibioticName">The full display name of the antibiotic.</param>
/// <param name="Value">The susceptibility reading for the selected organism, measured or untested.</param>
public sealed record AntibioticSusceptibility(
    string AntibioticName,
    SusceptibilityValue Value);
