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
}
