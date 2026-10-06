namespace penicillisolver_v2.Services;

using penicillisolver_v2.Domain.Entities;
using penicillisolver_v2.Domain.ValueObjects;

/// <summary>
/// The outcome of one attempt to accept an uploaded spreadsheet. An upload
/// either succeeds and yields both the freshly parsed document and the
/// persisted upload row, or it fails with a single message that is safe to
/// show to the user.
/// </summary>
/// <remarks>
/// The message is always written for the person who chose the file; a raw
/// exception message never reaches this type. Callers branch on
/// <see cref="IsSuccess"/> and then read exactly one of the two payload
/// properties.
/// </remarks>
public sealed class SpreadsheetUploadResult
{
    /// <summary>
    /// Creates a result with the given success state, document, upload, and message.
    /// </summary>
    /// <param name="isSuccess">Whether the upload was accepted.</param>
    /// <param name="document">The parsed document, or null on failure.</param>
    /// <param name="upload">The persisted upload row, or null on failure.</param>
    /// <param name="message">The confirmation or failure message.</param>
    private SpreadsheetUploadResult(
        bool isSuccess,
        SpreadsheetDocument? document,
        SpreadsheetUpload? upload,
        string message)
    {
        IsSuccess = isSuccess;
        Document = document;
        Upload = upload;
        Message = message;
    }

    /// <summary>Whether the upload was accepted.</summary>
    public bool IsSuccess { get; }

    /// <summary>The parsed document when the upload succeeded; otherwise null.</summary>
    public SpreadsheetDocument? Document { get; }

    /// <summary>The persisted upload row when the upload succeeded; otherwise null.</summary>
    public SpreadsheetUpload? Upload { get; }

    /// <summary>
    /// A confirmation sentence on success, or the failure explanation on
    /// failure. Never empty and never an exception message.
    /// </summary>
    public string Message { get; }

    /// <summary>Creates the success outcome.</summary>
    /// <param name="document">The document that was parsed.</param>
    /// <param name="upload">The upload row that was written.</param>
    /// <param name="message">The confirmation shown to the user.</param>
    /// <returns>A successful upload result.</returns>
    public static SpreadsheetUploadResult Success(
        SpreadsheetDocument document,
        SpreadsheetUpload upload,
        string message)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        SpreadsheetUploadResult result = new(
            isSuccess: true,
            document: document,
            upload: upload,
            message: message);

        return result;
    }

    /// <summary>Creates the failure outcome.</summary>
    /// <param name="message">Why the upload was rejected, in user facing terms.</param>
    /// <returns>A failed upload result.</returns>
    public static SpreadsheetUploadResult Failure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        SpreadsheetUploadResult result = new(
            isSuccess: false,
            document: null,
            upload: null,
            message: message);

        return result;
    }
}
