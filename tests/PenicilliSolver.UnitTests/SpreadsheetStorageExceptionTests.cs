using penicillisolver_v2.Services;

using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Checks that a storage failure is described accurately enough for an
/// administrator who did not write the code. The message must say what
/// happened, what state the data is in, and what to check — and it must not
/// claim a cause that the exception does not prove.
/// </summary>
public sealed class SpreadsheetStorageExceptionTests
{
    /// <summary>A promotion failed because the staged file had already gone.</summary>
    [Fact]
    public void ForPromotion_WithMissingStagedFile_BlamesAConcurrentUpload()
    {
        FileNotFoundException cause = new(
            "Could not find file '/app/App_Data/spreadsheets/abc.incoming'.",
            "/app/App_Data/spreadsheets/abc.incoming");

        SpreadsheetStorageException exception = SpreadsheetStorageException.ForStorageFailure(
            cause,
            "/app/App_Data/spreadsheets");

        Assert.Equal(SpreadsheetStorageFailureKind.StagedFileDisappeared, exception.Kind);
        Assert.Contains("another upload", exception.Message);
        Assert.Contains("/app/App_Data/spreadsheets", exception.Message);
        Assert.Same(cause, exception.InnerException);

        // It must say the old spreadsheet is safe, because that is the question
        // a technician asks first.
        Assert.Contains("has not been changed", exception.Message);
    }

    /// <summary>A promotion failed because the folder is not writable.</summary>
    [Fact]
    public void ForPromotion_WithPermissionDenied_BlamesTheFolderPermissions()
    {
        UnauthorizedAccessException cause = new(
            "Access to the path '/app/App_Data/spreadsheets' is denied.");

        SpreadsheetStorageException exception = SpreadsheetStorageException.ForStorageFailure(
            cause,
            "/app/App_Data/spreadsheets");

        Assert.Equal(SpreadsheetStorageFailureKind.StorageDirectoryNotWritable, exception.Kind);
        Assert.Contains("permission", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("was not there", exception.Message);
    }

    /// <summary>A plain IO failure is reported as a generic storage failure.</summary>
    [Fact]
    public void ForPromotion_WithPlainIoFailure_ReportsAGenericStorageFailure()
    {
        IOException cause = new("There is not enough space on the disk.");

        SpreadsheetStorageException exception = SpreadsheetStorageException.ForStorageFailure(
            cause,
            "/app/App_Data/spreadsheets");

        Assert.Equal(SpreadsheetStorageFailureKind.StorageUnavailable, exception.Kind);

        // It must not claim a specific cause it cannot prove.
        Assert.DoesNotContain("was not there", exception.Message);
        Assert.DoesNotContain("permission", exception.Message, StringComparison.OrdinalIgnoreCase);

        // But it should name disk space as a thing to check.
        Assert.Contains("disk space", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The exception stays inside the IOException family, so existing catches work.</summary>
    [Fact]
    public void SpreadsheetStorageException_IsAnIOException()
    {
        SpreadsheetStorageException exception = new(
            SpreadsheetStorageFailureKind.StorageUnavailable,
            "/app/App_Data/spreadsheets",
            "technical detail");

        Assert.IsAssignableFrom<IOException>(exception);
    }

    /// <summary>
    /// The technical detail is preserved, because the administrator may need the
    /// original message even though the technician will not see it.
    /// </summary>
    [Fact]
    public void Message_CarriesTheOriginalTechnicalDetail()
    {
        IOException cause = new("ENOSPC: no space left on device");

        SpreadsheetStorageException exception = SpreadsheetStorageException.ForStorageFailure(
            cause,
            "/app/App_Data/spreadsheets");

        Assert.Contains("ENOSPC", exception.Message);
        Assert.Same(cause, exception.InnerException);
    }
}