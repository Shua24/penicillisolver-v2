namespace penicillisolver_v2.Services;

using System.Security.Cryptography;

/// <summary>
/// The low level file and stream operations used by
/// <see cref="SpreadsheetStorageService"/>. They are kept in their own partial
/// file so the public storage operations read as the workflow they are, without
/// a screenful of buffer plumbing between each step.
/// </summary>
public sealed partial class SpreadsheetStorageService
{
    /// <summary>
    /// Copies the uploaded stream to a file and returns the byte count.
    /// </summary>
    private static async Task<long> WriteStreamToFileAsync(Stream content, string filePath)
    {
        long writtenByteCount;

        await using (FileStream destinationStream = new(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            StreamCopyBufferSize,
            useAsync: true))
        {
            writtenByteCount = await CopyStreamAsync(content, destinationStream);
        }

        return writtenByteCount;
    }

    /// <summary>
    /// Copies a stream in fixed size chunks, counting bytes as it goes.
    /// </summary>
    private static async Task<long> CopyStreamAsync(Stream source, Stream destination)
    {
        byte[] buffer = new byte[StreamCopyBufferSize];

        long totalByteCount = 0;

        while (true)
        {
            int bytesRead = await source.ReadAsync(buffer);

            if (bytesRead == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead));

            totalByteCount += bytesRead;
        }

        return totalByteCount;
    }

    /// <summary>
    /// Computes the lower case hexadecimal SHA-256 hash of a file on disk.
    /// </summary>
    private static async Task<string> ComputeContentHashAsync(string filePath)
    {
        await using FileStream hashStream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            StreamCopyBufferSize,
            useAsync: true);

        byte[] hashBytes = await SHA256.HashDataAsync(hashStream);

        string hashText = Convert.ToHexStringLower(hashBytes);

        return hashText;
    }

    /// <summary>
    /// Computes the lower case hexadecimal SHA-256 hash of a file on disk,
    /// synchronously. Used by the promotion path, which is synchronous.
    /// </summary>
    private static string ComputeContentHash(string filePath)
    {
        using FileStream hashStream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            StreamCopyBufferSize);

        byte[] hashBytes = SHA256.HashData(hashStream);

        string hashText = Convert.ToHexStringLower(hashBytes);

        return hashText;
    }

    /// <summary>
    /// Deletes a staging file, ignoring a failure to do so. Used on a path that
    /// is already propagating an exception: the original failure is the one the
    /// caller needs, so a cleanup error must not replace it.
    /// </summary>
    private static void TryDeleteStagedFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
            // The original exception is more useful than this one.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: the caller is already reporting a storage failure.
        }
    }
}
