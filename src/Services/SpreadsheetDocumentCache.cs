namespace penicillisolver_v2.Services;

using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// Holds the one parsed current spreadsheet in memory between page loads.
/// </summary>
/// <remarks>
/// <para>
/// There is only ever one current spreadsheet, so this cache holds exactly one
/// entry rather than a dictionary with eviction: the entry is identified by the
/// content hash of the upload it was parsed from. A request that finds a
/// different hash overwrites the entry, which is precisely the invalidation
/// rule — the hash changes if and only if the stored bytes change.
/// </para>
/// <para>
/// It is registered as a singleton and its <see cref="Set"/> and
/// <see cref="Get"/> operations are lock protected, because an interactive
/// Blazor Server circuit and ordinary HTTP requests can reach it concurrently.
/// Only the immutable parsed document is stored; the caller's
/// <see cref="SpreadsheetDocument"/> is never handed out mutable.
/// </para>
/// </remarks>
public sealed class SpreadsheetDocumentCache
{
    private readonly object gate = new();

    private string? cachedContentHash;

    private SpreadsheetDocument? cachedDocument;

    /// <summary>
    /// Returns the cached document when the supplied hash matches the entry,
    /// otherwise null.
    /// </summary>
    /// <param name="contentHash">The content hash of the current upload.</param>
    /// <returns>The cached document, or null on a miss.</returns>
    public SpreadsheetDocument? Get(string contentHash)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        lock (gate)
        {
            bool hashMatches = string.Equals(
                cachedContentHash,
                contentHash,
                StringComparison.Ordinal);

            if (!hashMatches)
            {
                return null;
            }

            return cachedDocument;
        }
    }

    /// <summary>
    /// Stores the parsed document against its content hash, replacing any
    /// previous entry.
    /// </summary>
    /// <param name="contentHash">The content hash of the upload the document was parsed from.</param>
    /// <param name="document">The parsed document.</param>
    public void Set(string contentHash, SpreadsheetDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentNullException.ThrowIfNull(document);

        lock (gate)
        {
            cachedContentHash = contentHash;
            cachedDocument = document;
        }
    }

    /// <summary>
    /// Empties the cache. Used by tests; the running application never needs it
    /// because a new content hash displaces the previous entry on its own.
    /// </summary>
    public void Clear()
    {
        lock (gate)
        {
            cachedContentHash = null;
            cachedDocument = null;
        }
    }
}
