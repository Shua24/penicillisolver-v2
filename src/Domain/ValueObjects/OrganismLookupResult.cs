namespace penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// How a typed organism name resolved against a document's organisms.
/// </summary>
public enum OrganismLookupOutcome
{
    /// <summary>Exactly one organism matched; the ranking can proceed.</summary>
    Resolved,

    /// <summary>Several organisms matched; the caller must ask which one is meant.</summary>
    Ambiguous,

    /// <summary>Nothing matched; no ranking may be produced.</summary>
    NotFound,
}

/// <summary>
/// The result of resolving a typed organism name.
/// </summary>
/// <remarks>
/// Only <see cref="OrganismLookupOutcome.Resolved"/> carries an organism the
/// caller may rank. The other two outcomes exist so the page can explain itself
/// instead of falling back to a guess.
/// </remarks>
public sealed record OrganismLookupResult
{
    private OrganismLookupResult(
        OrganismLookupOutcome outcome,
        string? organismName,
        IReadOnlyList<string> candidates)
    {
        Outcome = outcome;
        OrganismName = organismName;
        Candidates = candidates;
    }

    /// <summary>Which of the three cases this result represents.</summary>
    public OrganismLookupOutcome Outcome { get; }

    /// <summary>The single matched organism, or null when the outcome is not Resolved.</summary>
    public string? OrganismName { get; }

    /// <summary>
    /// The names to show the reader: the choices when ambiguous, or suggestions
    /// when not found. Empty when the outcome is Resolved.
    /// </summary>
    public IReadOnlyList<string> Candidates { get; }

    /// <summary>Records that exactly one organism matched.</summary>
    public static OrganismLookupResult Resolved(string organismName)
    {
        return new OrganismLookupResult(
            OrganismLookupOutcome.Resolved,
            organismName,
            []);
    }

    /// <summary>Records that several organisms matched and the reader must choose.</summary>
    public static OrganismLookupResult Ambiguous(IReadOnlyList<string> candidates)
    {
        return new OrganismLookupResult(
            OrganismLookupOutcome.Ambiguous,
            organismName: null,
            candidates);
    }

    /// <summary>Records that nothing matched.</summary>
    public static OrganismLookupResult NotFound(IReadOnlyList<string> suggestions)
    {
        return new OrganismLookupResult(
            OrganismLookupOutcome.NotFound,
            organismName: null,
            suggestions);
    }
}
