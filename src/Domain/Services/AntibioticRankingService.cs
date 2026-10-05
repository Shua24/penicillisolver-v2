using penicillisolver_v2.Domain.ValueObjects;

namespace penicillisolver_v2.Domain.Services;

/// <summary>
/// Ranks antibiotics by how resistant ONE organism is to them.
/// </summary>
/// <remarks>
/// Resistance is measured inversely through susceptibility: the lower the
/// percentage of susceptible isolates, the more resistant the antibiotic, so
/// the most resistant antibiotics sort first.
/// <para>
/// The ranking is always scoped to a single organism. An earlier version
/// averaged each antibiotic across every organism in the file, which made the
/// result depend on how many organisms happened to be tested: an antibiotic
/// measured against five organisms could outrank one measured against fifty on
/// the strength of far less evidence. Ranking within one organism removes that
/// artefact and answers the question actually being asked, which is what a
/// single species is still susceptible to.
/// </para>
/// <para>
/// ONLY ACTUAL MEASUREMENTS ARE SCORED. A drug the file never reported against
/// this organism carries no value and sorts after every measured drug, because
/// "not tested" is not evidence of resistance (Q14 revision). Treating a blank
/// cell as a zero scored it as maximally resistant, which put every untested
/// antigen at the top of the leaderboard in alphabetical order. This mirrors
/// the reference implementation, whose sort drops missing values and whose
/// top-N is a descending sort of the reported percentages.
/// </para>
/// <para>
/// An untested drug is still LISTED, after the measured ones, so a reader who
/// asks for more rows than the report has tested drugs sees which drugs were
/// not reported instead of a table that quietly stops short. Those rows carry
/// <see cref="SusceptibilityValue.Untested"/> and render as a dash.
/// </para>
/// </remarks>
public static class AntibioticRankingService
{
    /// <summary>
    /// Ranks every antibiotic in the document for one organism, most resistant first.
    /// </summary>
    /// <remarks>
    /// Measured drugs come first, ordered by ascending susceptibility (most
    /// resistant first) and then by antibiotic name using ordinal comparison.
    /// The name tie-break is a correctness requirement rather than a nicety:
    /// drugs sharing a percentage would otherwise come back in an unstable, run
    /// dependent order.
    /// </remarks>
    /// <param name="document">The parsed spreadsheet to rank.</param>
    /// <param name="organismName">The organism to rank against.</param>
    /// <returns>
    /// Every antibiotic in the document, most resistant for that organism first.
    /// Empty when the organism is not present in the document.
    /// </returns>
    public static IReadOnlyList<AntibioticResistance> RankWithinOrganism(
        SpreadsheetDocument document,
        string organismName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(organismName);

        string trimmedOrganismName = organismName.Trim();

        bool organismIsPresent = document.OrganismNames.Any(name =>
            string.Equals(name, trimmedOrganismName, StringComparison.OrdinalIgnoreCase));

        if (!organismIsPresent)
        {
            return [];
        }

        List<AntibioticResistance> resistancePerAntibiotic =
            ComputeResistanceWithinOrganism(document, trimmedOrganismName);

        IReadOnlyList<AntibioticResistance> orderedResult =
            SortMostResistantFirst(resistancePerAntibiotic);

        return orderedResult;
    }

    /// <summary>
    /// Ranks the antibiotics for one organism and returns only the required leaders.
    /// </summary>
    /// <param name="document">The parsed spreadsheet to rank.</param>
    /// <param name="organismName">The organism to rank against.</param>
    /// <param name="requestedCount">How many to return. Values below one are clamped to one.</param>
    /// <returns>The most resistant antibiotics, at most <paramref name="requestedCount"/> of them.</returns>
    public static IReadOnlyList<AntibioticResistance> GetMostResistantWithinOrganism(
        SpreadsheetDocument document,
        string organismName,
        int requestedCount)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(organismName);

        int effectiveCount = Math.Max(requestedCount, 1);

        IReadOnlyList<AntibioticResistance> rankedAntibiotics =
            RankWithinOrganism(document, organismName);

        IEnumerable<AntibioticResistance> leadingAntibiotics = rankedAntibiotics.Take(effectiveCount);

        List<AntibioticResistance> limitedResult = leadingAntibiotics.ToList();

        return limitedResult;
    }

    /// <summary>
    /// Orders the rows with the most resistant measured drug first, followed by
    /// the drugs that carry no measurement.
    /// </summary>
    /// <remarks>
    /// A drug with no measurement is placed after every measured drug rather
    /// than given a score of zero. Zero is the most resistant score possible,
    /// so scoring an absent reading as zero let drugs nobody had tested lead
    /// the leaderboard.
    /// </remarks>
    private static IReadOnlyList<AntibioticResistance> SortMostResistantFirst(
        List<AntibioticResistance> resistancePerAntibiotic)
    {
        IEnumerable<AntibioticResistance> measuredFirst = resistancePerAntibiotic
            .OrderBy(resistance => resistance.Value.IsMeasured ? 0 : 1)
            .ThenBy(RankOrderKey)
            .ThenBy(resistance => resistance.AntibioticName, StringComparer.Ordinal);

        List<AntibioticResistance> orderedResult = measuredFirst.ToList();

        return orderedResult;
    }

    /// <summary>
    /// The percentage a row sorts on: the measured value, or a stand-in for a
    /// row that was never measured.
    /// </summary>
    /// <remarks>
    /// An untested row is given the highest possible key so it can never sort
    /// above a measured drug, including one measured at 100 percent
    /// susceptible. The primary ordering already separates the two groups, so
    /// this key only ever orders rows within their own group.
    /// </remarks>
    private static double RankOrderKey(AntibioticResistance resistance)
    {
        double? percentSusceptible = resistance.Value.Percent;

        if (percentSusceptible is null)
        {
            return double.MaxValue;
        }

        return percentSusceptible.Value;
    }

    /// <summary>
    /// Projects every antibiotic in the document to its reading for one organism.
    /// </summary>
    /// <remarks>
    /// Every antibiotic the file names gets exactly one row, so the ranking
    /// covers the whole matrix. An antibiotic the file never reported against
    /// this organism produces an untested row, which is listed after the
    /// measured ones rather than scored as resistant.
    /// </remarks>
    private static List<AntibioticResistance> ComputeResistanceWithinOrganism(
        SpreadsheetDocument document,
        string organismName)
    {
        HashSet<string> rankedAntibioticNames = new HashSet<string>(StringComparer.Ordinal);

        List<AntibioticResistance> resistancePerAntibiotic = new List<AntibioticResistance>();

        foreach (SusceptibilityMeasurement measurement in document.Measurements)
        {
            bool organismMatches = string.Equals(
                measurement.OrganismName,
                organismName,
                StringComparison.OrdinalIgnoreCase);

            if (!organismMatches)
            {
                continue;
            }

            AntibioticResistance resistance = new AntibioticResistance(
                measurement.AntibioticName,
                measurement.Value);

            resistancePerAntibiotic.Add(resistance);
            rankedAntibioticNames.Add(measurement.AntibioticName);
        }

        foreach (string antibioticName in document.AntibioticNames)
        {
            bool alreadyRanked = rankedAntibioticNames.Contains(antibioticName);

            if (alreadyRanked)
            {
                continue;
            }

            AntibioticResistance missingResistance = new AntibioticResistance(
                antibioticName,
                SusceptibilityValue.Untested);

            resistancePerAntibiotic.Add(missingResistance);
        }

        return resistancePerAntibiotic;
    }
}
