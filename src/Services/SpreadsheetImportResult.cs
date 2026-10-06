namespace penicillisolver_v2.Services;

using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// The outcome of one attempt to read a spreadsheet file. An import either
/// produces a fully parsed <see cref="SpreadsheetDocument"/> or a single
/// human readable message explaining why it could not be produced; there is no
/// third state and no partially populated document. Callers are expected to
/// branch on <see cref="IsSuccess"/> and then read exactly one of the two
/// payload properties.
/// </summary>
public sealed class SpreadsheetImportResult
{
    /// <summary>
    /// Creates a result with the given success state, document, and error message.
    /// </summary>
    /// <param name="isSuccess">Whether the import produced a document.</param>
    /// <param name="document">The parsed document, or null on failure.</param>
    /// <param name="errorMessage">The failure explanation, or empty on success.</param>
    private SpreadsheetImportResult(
        bool isSuccess,
        SpreadsheetDocument? document,
        string errorMessage)
    {
        IsSuccess = isSuccess;
        Document = document;
        ErrorMessage = errorMessage;
    }

    /// <summary>Whether the import produced a document.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The parsed document when <see cref="IsSuccess"/> is true; otherwise null.
    /// </summary>
    public SpreadsheetDocument? Document { get; }

    /// <summary>
    /// The failure explanation when <see cref="IsSuccess"/> is false; otherwise
    /// the empty string.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Creates the success outcome for a parsed document.
    /// </summary>
    /// <param name="document">The document that was successfully parsed.</param>
    /// <returns>A successful import result carrying the document.</returns>
    public static SpreadsheetImportResult Success(SpreadsheetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        SpreadsheetImportResult result = new SpreadsheetImportResult(
            isSuccess: true,
            document: document,
            errorMessage: string.Empty);

        return result;
    }

    /// <summary>
    /// Creates the failure outcome for a human readable reason.
    /// </summary>
    /// <param name="message">Why the spreadsheet could not be imported.</param>
    /// <returns>A failed import result carrying the message.</returns>
    public static SpreadsheetImportResult Failure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        SpreadsheetImportResult result = new SpreadsheetImportResult(
            isSuccess: false,
            document: null,
            errorMessage: message);

        return result;
    }
}
