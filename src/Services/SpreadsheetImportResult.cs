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
    /// Creates a result with the given success state, document, error message,
    /// and the exception behind a failure when one caused it.
    /// </summary>
    /// <param name="isSuccess">Whether the import produced a document.</param>
    /// <param name="document">The parsed document, or null on failure.</param>
    /// <param name="errorMessage">The failure explanation, or empty on success.</param>
    /// <param name="failureCause">The exception behind the failure, or null.</param>
    private SpreadsheetImportResult(
        bool isSuccess,
        SpreadsheetDocument? document,
        string errorMessage,
        Exception? failureCause)
    {
        IsSuccess = isSuccess;
        Document = document;
        ErrorMessage = errorMessage;
        FailureCause = failureCause;
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
    /// The exception behind the failure when one caused it; otherwise null.
    /// </summary>
    /// <remarks>
    /// This is never shown to a reader, and it is deliberately a separate value
    /// from <see cref="ErrorMessage"/>. The message is the reader facing
    /// sentence in their own language; this is what the log records, so a
    /// corrupt upload leaves a diagnosable trail without putting a framework's
    /// English text into an Indonesian banner. Splitting the two is also why the
    /// readers need no logger: the caller that has one does the logging.
    /// </remarks>
    public Exception? FailureCause { get; }

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
            errorMessage: string.Empty,
            failureCause: null);

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
            errorMessage: message,
            failureCause: null);

        return result;
    }

    /// <summary>
    /// Creates the failure outcome for a reason the reader can act on, keeping
    /// the exception that caused it for the log.
    /// </summary>
    /// <param name="message">Why the spreadsheet could not be imported, in the reader's language.</param>
    /// <param name="cause">The exception behind the failure.</param>
    /// <returns>A failed import result carrying the message and the cause.</returns>
    public static SpreadsheetImportResult Failure(string message, Exception cause)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(cause);

        SpreadsheetImportResult result = new SpreadsheetImportResult(
            isSuccess: false,
            document: null,
            errorMessage: message,
            failureCause: cause);

        return result;
    }
}
