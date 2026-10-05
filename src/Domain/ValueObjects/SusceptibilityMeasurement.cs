namespace penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// One susceptibility measurement: how a single organism responds to a single
/// antibiotic, expressed as a percentage of susceptible isolates.
/// A measurement only exists when the source cell contained a value; a blank
/// cell produces no measurement at all, which is what lets the ranking exclude
/// untested combinations instead of treating them as zero.
/// </summary>
/// <param name="AntibioticName">The full display name of the antibiotic.</param>
/// <param name="OrganismName">The full display name of the organism.</param>
/// <param name="PercentSusceptible">The percentage of susceptible isolates, from 0 to 100 inclusive.</param>
public sealed record SusceptibilityMeasurement(
    string AntibioticName,
    string OrganismName,
    double PercentSusceptible);
