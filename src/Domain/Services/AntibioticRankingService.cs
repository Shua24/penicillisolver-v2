using penicillisolver_v2.Domain.ValueObjects;

namespace penicillisolver_v2.Domain.Services;

/// <summary>
/// Ranks antibiotics by how resistant the tested organisms are to them.
/// Resistance is measured inversely through susceptibility: the lower the mean
/// percentage of susceptible isolates, the more resistant the antibiotic.
/// </summary>
public static class AntibioticRankingService
{
    /// <summary>
    /// Ranks every antibiotic that has at least one measurement, most resistant first.
    /// </summary>
    /// <remarks>
    /// Ordering is by ascending mean susceptibility, then by antibiotic name using
    /// ordinal comparison. The name tie-break is a correctness requirement rather
    /// than a nicety: the supplied sample data contains several exact ties on the
    /// mean (four antibiotics at 0 and four at 4.942857...), and an ordering
    /// without a secondary key would return them in an unstable, run dependent
    /// order. Antibiotics with no measurements at all are excluded entirely.
    /// </remarks>
    /// <param name="document">The parsed spreadsheet to rank.</param>
    /// <returns>Every measured antibiotic, most resistant first.</returns>
    public static IReadOnlyList<AntibioticResistance> RankByResistance(SpreadsheetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<AntibioticResistance> rankedAntibiotics = ComputeResistance(document.Measurements);

        IEnumerable<AntibioticResistance> sortedAntibiotics = rankedAntibiotics
            .OrderBy(resistance => resistance.MeanPercentSusceptible)
            .ThenBy(resistance => resistance.AntibioticName, StringComparer.Ordinal);

        List<AntibioticResistance> orderedResult = sortedAntibiotics.ToList();

        return orderedResult;
    }

    /// <summary>
    /// Ranks the antibiotics and returns only the requested number of leaders.
    /// </summary>
    /// <param name="document">The parsed spreadsheet to rank.</param>
    /// <param name="requestedCount">How many antibiotics to return. Values below one are clamped to one.</param>
    /// <returns>The most resistant antibiotics, at most <paramref name="requestedCount"/> of them.</returns>
    public static IReadOnlyList<AntibioticResistance> GetMostResistant(
        SpreadsheetDocument document,
        int requestedCount)
    {
        ArgumentNullException.ThrowIfNull(document);

        int effectiveCount = Math.Max(requestedCount, 1);

        IReadOnlyList<AntibioticResistance> rankedAntibiotics = RankByResistance(document);

        IEnumerable<AntibioticResistance> leadingAntibiotics = rankedAntibiotics.Take(effectiveCount);

        List<AntibioticResistance> limitedResult = leadingAntibiotics.ToList();

        return limitedResult;
    }

    /// <summary>
    /// Returns the antibiotics that carry no measurement at all.
    /// </summary>
    /// <remarks>
    /// These are reported separately rather than ranked. An antibiotic with no
    /// data has an unknown resistance profile; treating it as fully resistant
    /// would place it at the top of the list on no evidence whatsoever.
    /// </remarks>
    /// <param name="document">The parsed spreadsheet to inspect.</param>
    /// <returns>Antibiotic names that never appear in a measurement, in source order.</returns>
    public static IReadOnlyList<string> GetAntibioticsWithoutMeasurements(SpreadsheetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        HashSet<string> measuredAntibioticNames = document.Measurements
            .Select(measurement => measurement.AntibioticName)
            .ToHashSet(StringComparer.Ordinal);

        List<string> unmeasuredAntibioticNames = document.AntibioticNames
            .Where(antibioticName => !measuredAntibioticNames.Contains(antibioticName))
            .ToList();

        return unmeasuredAntibioticNames;
    }

    /// <summary>
    /// Groups measurements by antibiotic and reduces each group to a mean.
    /// </summary>
    private static List<AntibioticResistance> ComputeResistance(
        IReadOnlyList<SusceptibilityMeasurement> measurements)
    {
        IEnumerable<IGrouping<string, SusceptibilityMeasurement>> measurementsByAntibiotic =
            measurements.GroupBy(measurement => measurement.AntibioticName, StringComparer.Ordinal);

        List<AntibioticResistance> resistancePerAntibiotic = new List<AntibioticResistance>();

        foreach (IGrouping<string, SusceptibilityMeasurement> antibioticGroup in measurementsByAntibiotic)
        {
            int measurementCount = antibioticGroup.Count();

            double susceptibilityTotal = antibioticGroup.Sum(
                measurement => measurement.PercentSusceptible);

            double meanSusceptibility = susceptibilityTotal / measurementCount;

            AntibioticResistance resistance = new AntibioticResistance(
                antibioticGroup.Key,
                meanSusceptibility,
                measurementCount);

            resistancePerAntibiotic.Add(resistance);
        }

        return resistancePerAntibiotic;
    }
}
