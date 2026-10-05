namespace penicillisolver_v2.Services;

/// <summary>
/// The outcome of a write attempted against the antibiotic abbreviation
/// mappings. A failed result always carries a message that is safe to show to
/// the user; no raw exception detail ever reaches a page.
/// </summary>
/// <param name="Succeeded">Whether the write was applied.</param>
/// <param name="Message">A user-facing message describing the outcome.</param>
/// <param name="MappingId">
/// The identifier of the affected mapping when the write succeeded and produced
/// or updated one; otherwise null.
/// </param>
public sealed record WriteResult(bool Succeeded, string Message, int? MappingId = null)
{
    /// <summary>Builds a successful result with an optional message.</summary>
    public static WriteResult Success(string message, int? mappingId = null)
    {
        return new WriteResult(true, message, mappingId);
    }

    /// <summary>Builds a failed result carrying a user-facing message.</summary>
    public static WriteResult Failure(string message)
    {
        return new WriteResult(false, message);
    }
}
