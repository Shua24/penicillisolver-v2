using System.Globalization;

namespace penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// A susceptibility reading that may be absent: either a percentage that was
/// reported, or the fact that the file reported nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// This exists because "never tested" and "tested, zero percent susceptible"
/// are different clinical findings. A blank cell means the drug was not
/// reported against that organism; a cell holding <c>0</c> means it was tested
/// and nothing was susceptible. Treating both as zero made the resistance
/// leaderboard lead with every untested drug, in alphabetical order, because
/// zero is the most resistant score there is — the untested antigens crowded
/// out the ones the report actually flagged.
/// </para>
/// <para>
/// The distinction lives in <see cref="IsMeasured"/> and <see cref="Percent"/>.
/// An untested reading has no percentage at all, so <see cref="Percent"/> is
/// null rather than zero and only a measured reading may be compared or
/// ranked. Callers that need a score must decide explicitly what an absent
/// reading means for them; they cannot silently receive a zero.
/// </para>
/// <para>
/// <see cref="Format"/> renders the value the way the spreadsheet page shows
/// it, so every caller produces the same text for the same reading and a
/// missing reading is never rendered as <c>0.00</c>.
/// </para>
/// </remarks>
public sealed class SusceptibilityValue
{
    /// <summary>The decimal places used when a percentage is rendered.</summary>
    private const string DisplayFormat = "F2";

    /// <summary>
    /// Creates a reading with the given measured state and percentage.
    /// </summary>
    /// <param name="isMeasured">Whether the file reported a percentage.</param>
    /// <param name="percent">The reported percentage, or null when untested.</param>
    private SusceptibilityValue(bool isMeasured, double? percent)
    {
        IsMeasured = isMeasured;
        Percent = percent;
    }

    /// <summary>The reading for a cell the file left blank.</summary>
    public static SusceptibilityValue Untested { get; } = new(false, null);

    /// <summary>True when the file reported a percentage, false when it reported nothing.</summary>
    public bool IsMeasured { get; }

    /// <summary>
    /// The reported percentage, or null when the value is
    /// <see cref="Untested"/>. Never zero by default.
    /// </summary>
    public double? Percent { get; }

    /// <summary>
    /// Creates a measured reading.
    /// </summary>
    /// <param name="percent">The reported percentage, from 0 to 100 inclusive.</param>
    /// <returns>The measured reading.</returns>
    public static SusceptibilityValue Measured(double percent)
    {
        return new SusceptibilityValue(true, percent);
    }

    /// <summary>
    /// Renders the reading for display: the percentage with two decimals when
    /// it was measured, or a dash when the file reported nothing.
    /// </summary>
    /// <returns>The display text.</returns>
    public string Format()
    {
        if (Percent is null)
        {
            return "—";
        }

        string formattedPercent = Percent.Value.ToString(
            DisplayFormat,
            CultureInfo.InvariantCulture);

        return formattedPercent;
    }
}
