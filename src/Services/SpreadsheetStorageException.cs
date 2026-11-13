namespace penicillisolver_v2.Services;

/// <summary>
/// The categories of storage failure the application can tell apart.
/// </summary>
/// <remarks>
/// These are deliberately coarse. Each one corresponds to a different thing an
/// administrator has to check, and the application only claims a category when
/// the underlying exception actually proves it.
/// </remarks>
public enum SpreadsheetStorageFailureKind
{
    /// <summary>
    /// The staged file was gone when it was time to move it into place. In
    /// practice this means a second upload of the same file was being processed
    /// at the same moment, and it consumed the staged file first.
    /// </summary>
    StagedFileDisappeared,

    /// <summary>
    /// The application is not allowed to write to the storage folder. The folder
    /// exists but its permissions deny the account the application runs as.
    /// </summary>
    StorageDirectoryNotWritable,

    /// <summary>
    /// The write failed for some other reason: no space, a broken mount, or a
    /// failure the application cannot attribute.
    /// </summary>
    StorageUnavailable,
}

/// <summary>
/// Raised when an uploaded spreadsheet could not be written to storage.
/// </summary>
/// <remarks>
/// <para>
/// This exists so a failure is readable by somebody who did not write the
/// application. The <see cref="Exception.Message"/> states what the application
/// was doing, which category of failure it hit, whether the spreadsheet already
/// in use is intact, and what an administrator should check. The original
/// exception is preserved as <see cref="Exception.InnerException"/>.
/// </para>
/// <para>
/// It derives from <see cref="IOException"/> rather than
/// <see cref="FileNotFoundException"/> because a storage failure is not
/// necessarily a missing file: a permission denial derives from
/// <see cref="SystemException"/> and never touches the IO exception family, and
/// a full disk is a plain <see cref="IOException"/>. Deriving from
/// <see cref="FileNotFoundException"/> would force every failure to claim the
/// file was missing, which is a false diagnosis for at least two of the three
/// categories. Deriving from <see cref="IOException"/> keeps the type inside the
/// family existing handlers already catch.
/// </para>
/// <para>
/// There is deliberately no default constructor: every instance must state its
/// category, so no message can be produced that blames nobody.
/// </para>
/// </remarks>
public sealed class SpreadsheetStorageException : IOException
{
    private const string StorageFolderUnknown = "the configured storage folder";

    /// <summary>Creates the exception with an explicit category and detail.</summary>
    /// <param name="kind">Which category of storage failure occurred.</param>
    /// <param name="storageDirectoryPath">
    /// The folder the application writes spreadsheets into, as an absolute path.
    /// </param>
    /// <param name="technicalDetail">
    /// The underlying message, preserved verbatim for whoever reads the log.
    /// </param>
    /// <param name="innerException">The original exception, if there was one.</param>
    public SpreadsheetStorageException(
        SpreadsheetStorageFailureKind kind,
        string storageDirectoryPath,
        string technicalDetail,
        Exception? innerException = null)
        : base(
            BuildMessage(kind, storageDirectoryPath, technicalDetail),
            innerException)
    {
        Kind = kind;
        StorageDirectoryPath = storageDirectoryPath;
    }

    /// <summary>Which category of storage failure occurred.</summary>
    public SpreadsheetStorageFailureKind Kind { get; }

    /// <summary>The storage folder the application was writing into.</summary>
    public string StorageDirectoryPath { get; }

    /// <summary>
    /// Builds the exception for a failed promotion of a staged file onto its
    /// canonical name.
    /// </summary>
    /// <param name="cause">The exception the filesystem raised.</param>
    /// <param name="storageDirectoryPath">The folder the application writes spreadsheets into.</param>
    /// <returns>An exception describing the failure in administrative terms.</returns>
    public static SpreadsheetStorageException ForStorageFailure(
        Exception cause,
        string storageDirectoryPath)
    {
        ArgumentNullException.ThrowIfNull(cause);

        SpreadsheetStorageFailureKind kind = Classify(cause);

        string technicalDetail = $"{cause.GetType().Name}: {cause.Message}";

        SpreadsheetStorageException exception = new(
            kind,
            storageDirectoryPath,
            technicalDetail,
            cause);

        return exception;
    }

    /// <summary>
    /// Decides which category the cause proves. A cause that is not recognised
    /// is reported as a generic storage failure rather than being attributed to
    /// a guess.
    /// </summary>
    private static SpreadsheetStorageFailureKind Classify(Exception cause)
    {
        if (cause is FileNotFoundException)
        {
            return SpreadsheetStorageFailureKind.StagedFileDisappeared;
        }

        if (cause is UnauthorizedAccessException)
        {
            return SpreadsheetStorageFailureKind.StorageDirectoryNotWritable;
        }

        return SpreadsheetStorageFailureKind.StorageUnavailable;
    }

    /// <summary>
    /// Writes the message in the order an administrator needs it: what was
    /// happening, what went wrong, whether data was lost, what to check, and
    /// finally the raw technical detail.
    /// </summary>
    private static string BuildMessage(
        SpreadsheetStorageFailureKind kind,
        string storageDirectoryPath,
        string technicalDetail)
    {
        string folder = string.IsNullOrWhiteSpace(storageDirectoryPath)
            ? StorageFolderUnknown
            : storageDirectoryPath;

        string whatFailed;
        string whatToCheck;

        switch (kind)
        {
            case SpreadsheetStorageFailureKind.StagedFileDisappeared:
                whatFailed =
                    "The uploaded file disappeared before it could be moved into "
                    + "the storage folder. This happens when another upload of the "
                    + "same spreadsheet is saved at the same moment.";
                whatToCheck =
                    "Ask the person who uploaded to try again. If it keeps "
                    + "happening, two people are uploading at once and should take "
                    + "turns.";
                break;

            case SpreadsheetStorageFailureKind.StorageDirectoryNotWritable:
                whatFailed =
                    "The application is not allowed to write to the storage "
                    + "folder. The folder exists, but the account the application "
                    + "runs as does not have permission to create files in it.";
                whatToCheck =
                    "Check the folder's permissions and make sure the account the "
                    + "application runs as can create and rename files in it.";
                break;

            default:
                whatFailed =
                    "The application could not write to the storage folder. The "
                    + "usual cause is that there is no free disk space left, the "
                    + "folder is on a mount that is no longer available, or the "
                    + "filesystem reported an error.";
                whatToCheck =
                    "Check that there is free disk space on the volume holding "
                    + "the storage folder and that the folder is reachable.";
                break;
        }

        string message =
            $"The spreadsheet could not be saved. {whatFailed} "
            + "The spreadsheet already in use has not been changed, so the "
            + $"laboratory can keep working. {whatToCheck} "
            + $"Storage folder: {folder}. "
            + $"Technical detail: {technicalDetail}";

        return message;
    }
}