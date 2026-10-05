namespace penicillisolver_v2.Services;

using penicillisolver_v2.Domain.Enums;

/// <summary>
/// Describes one file that was written by <see cref="SpreadsheetStorageService"/>.
/// It carries everything the upload pipeline needs afterwards: where the bytes
/// landed, how to recognise them again, what format they are, and how large the
/// file is.
/// </summary>
/// <param name="StoredFilePath">The absolute path the file was written to.</param>
/// <param name="RelativeStoredFilePath">
/// The file name relative to the configured storage directory. This is what is
/// persisted on the upload row, so the row stays valid if the storage root
/// moves.
/// </param>
/// <param name="ContentHash">The lower case hexadecimal SHA-256 hash of the file contents.</param>
/// <param name="FileFormat">Whether the stored file is a csv or an xlsx workbook.</param>
/// <param name="ByteCount">The number of bytes that were written.</param>
public sealed record SpreadsheetStorageResult(
    string StoredFilePath,
    string RelativeStoredFilePath,
    string ContentHash,
    SpreadsheetFileFormat FileFormat,
    long ByteCount);
