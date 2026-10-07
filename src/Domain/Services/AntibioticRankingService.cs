using penicillisolver_v2.Domain.ValueObjects;

namespace penicillisolver_v2.Domain.Services;

/// <summary>
/// Ranks antibiotics by how susceptible ONE organism is to them.
/// </summary>
/// <remarks>
/// The leaderboard is ordered by how susceptible ONE organism is to each
/// antibiotic: the higher the percentage of susceptible isolates, the more the
/// drug still works against that species, so the most susceptible (highest
/// percentage) antibiotics sort first. A drug reported at 0.00, or one the
/// file never tested, is the least useful, so those sink to the bottom.
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
/// ONLY ACTUAL MEASUREMENTS LEAD THE LIST. A drug the file never reported
/// against this organism carries no value and sorts after every measured drug,
/// because "not tested" is not evidence of susceptibility (Q14 revision). This
/// mirrors the reference implementation, whose top-N is a descending sort of the
/// reported percentages and whose blank cells therefore land at the bottom.
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
    /// Ranks every antibiotic in the document for one organism, most susceptible first.
    /// </summary>
    /// <remarks>
    /// Measured drugs come first, ordered by descending susceptibility (the
    /// highest percentage leads, the drug the organism is still most susceptible
    /// to) and then by antibiotic name using ordinal comparison. A drug reported
    /// at 0.00 therefore sits at the bottom of the measured group. The name
    /// tie-break is a correctness requirement rather than a nicety: drugs
    /// sharing a percentage would otherwise come back in an unstable, run
    /// dependent order.
    /// </remarks>
    /// <param name="document">The parsed spreadsheet to rank.</param>
    /// <param name="organismName">The organism to rank against.</param>
    /// <returns>
    /// Every antibiotic in the document, most susceptible for that organism
    /// first. Empty when the organism is not present in the document.
    /// </returns>
    public static IReadOnlyList<AntibioticSusceptibility> RankWithinOrganism(
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

        List<AntibioticSusceptibility> susceptibilityPerAntibiotic =
            ComputeSusceptibilityWithinOrganism(document, trimmedOrganismName);

        IReadOnlyList<AntibioticSusceptibility> orderedResult =
            SortMostSusceptibleFirst(susceptibilityPerAntibiotic);

        return orderedResult;
    }

    /// <summary>
    /// Ranks the antibiotics for one organism and returns only the required leaders.
    /// </summary>
    /// <param name="document">The parsed spreadsheet to rank.</param>
    /// <param name="organismName">The organism to rank against.</param>
    /// <param name="requestedCount">How many to return. Values below one are clamped to one.</param>
    /// <returns>The most susceptible antibiotics, at most <paramref name="requestedCount"/> of them.</returns>
    public static IReadOnlyList<AntibioticSusceptibility> GetMostSusceptibleWithinOrganism(
        SpreadsheetDocument document,
        string organismName,
        int requestedCount)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(organismName);

        int effectiveCount = Math.Max(requestedCount, 1);

        IReadOnlyList<AntibioticSusceptibility> rankedAntibiotics =
            RankWithinOrganism(document, organismName);

        IEnumerable<AntibioticSusceptibility> leadingAntibiotics = rankedAntibiotics.Take(effectiveCount);

        List<AntibioticSusceptibility> limitedResult = leadingAntibiotics.ToList();

        return limitedResult;
    }

    /// <summary>
    /// Orders the rows with the most susceptible measured drug first, followed by
    /// the drugs that carry no measurement.
    /// </summary>
    /// <remarks>
    /// A drug with no measurement is placed after every measured drug rather
    /// than given a score. An untested reading carries no percentage at all, so
    /// it can never compete with a measured drug on the value axis; the
    /// untested group always trails the measured group.
    /// </remarks>
    private static IReadOnlyList<AntibioticSusceptibility> SortMostSusceptibleFirst(
        List<AntibioticSusceptibility> susceptibilityPerAntibiotic)
    {
        IEnumerable<AntibioticSusceptibility> measuredFirst = susceptibilityPerAntibiotic
            .OrderBy(susceptibility => susceptibility.Value.IsMeasured ? 0 : 1)
            .ThenByDescending(RankOrderKey)
            .ThenBy(susceptibility => susceptibility.AntibioticName, StringComparer.Ordinal);

        List<AntibioticSusceptibility> orderedResult = measuredFirst.ToList();

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
    private static double RankOrderKey(AntibioticSusceptibility susceptibility)
    {
        double? percentSusceptible = susceptibility.Value.Percent;

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
    /// measured ones rather than carrying a value.
    /// </remarks>
    private static List<AntibioticSusceptibility> ComputeSusceptibilityWithinOrganism(
        SpreadsheetDocument document,
        string organismName)
    {
        HashSet<string> rankedAntibioticNames = new HashSet<string>(StringComparer.Ordinal);

        List<AntibioticSusceptibility> susceptibilityPerAntibiotic = new List<AntibioticSusceptibility>();

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

            AntibioticSusceptibility susceptibility = new AntibioticSusceptibility(
                measurement.AntibioticName,
                measurement.Value);

            susceptibilityPerAntibiotic.Add(susceptibility);
            rankedAntibioticNames.Add(measurement.AntibioticName);
        }

        foreach (string antibioticName in document.AntibioticNames)
        {
            bool alreadyRanked = rankedAntibioticNames.Contains(antibioticName);

            if (alreadyRanked)
            {
                continue;
            }

            AntibioticSusceptibility missingSusceptibility = new AntibioticSusceptibility(
                antibioticName,
                SusceptibilityValue.Untested);

            susceptibilityPerAntibiotic.Add(missingSusceptibility);
        }

        return susceptibilityPerAntibiotic;
    }
}
