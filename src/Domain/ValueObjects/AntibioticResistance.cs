namespace penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// One row of the resistance leaderboard: an antibiotic together with the mean
/// susceptibility computed from every non-blank measurement recorded for it.
/// The lower the mean, the more resistant the organisms are to the antibiotic,
/// so the most resistant antibiotics sort first.
/// </summary>
/// <param name="AntibioticName">The full display name of the antibiotic.</param>
/// <param name="MeanPercentSusceptible">The arithmetic mean of all non-blank measurements for this antibiotic.</param>
/// <param name="MeasurementCount">How many non-blank measurements the mean was computed from. Always at least one.</param>
public sealed record AntibioticResistance(
    string AntibioticName,
    double MeanPercentSusceptible,
    int MeasurementCount);
