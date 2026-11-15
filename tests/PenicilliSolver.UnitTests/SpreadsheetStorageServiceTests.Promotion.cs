using penicillisolver_v2.Domain.Enums;
using penicillisolver_v2.Services;

using Xunit;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// The promotion half of <see cref="SpreadsheetStorageServiceTests"/>: moving a
/// staged file onto its content-addressed name, the races that can happen on the
/// way, and the clean-up that must survive a failed write. Kept in a partial
/// file so neither half of the fixture exceeds the project's 300-line cap.
/// </summary>
public sealed partial class SpreadsheetStorageServiceTests
{
    [Fact]
    public async Task PromoteStagedFile_MovesTheStagedFileOntoItsContentAddressedName()
    {
        SpreadsheetStorageResult stagingResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(BuildCsvBytes()),
            SpreadsheetFileFormat.Csv);

        string canonicalPath = Path.Combine(
            storageService.StorageDirectoryPath,
            stagingResult.RelativeStoredFilePath);

        Assert.NotEqual(canonicalPath, stagingResult.StoredFilePath);

        storageService.PromoteStagedFile(
            stagingResult.StoredFilePath,
            stagingResult.RelativeStoredFilePath);

        Assert.True(File.Exists(canonicalPath));
        Assert.False(File.Exists(stagingResult.StoredFilePath));
    }

    [Fact]
    public async Task PromoteStagedFile_IsIdempotentForIdenticalContent()
    {
        byte[] csvBytes = BuildCsvBytes();

        using (MemoryStream firstContent = new(csvBytes))
        {
            await storageService.SaveCurrentSpreadsheetAsync(
                firstContent,
                SpreadsheetFileFormat.Csv);
        }

        SpreadsheetStorageResult stagingResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(csvBytes),
            SpreadsheetFileFormat.Csv);

        // Promoting content that is already stored must not throw and must not
        // destroy or duplicate the file already in place.
        storageService.PromoteStagedFile(
            stagingResult.StoredFilePath,
            stagingResult.RelativeStoredFilePath);

        string canonicalPath = Path.Combine(
            storageService.StorageDirectoryPath,
            stagingResult.RelativeStoredFilePath);

        Assert.True(File.Exists(canonicalPath));
        Assert.False(File.Exists(stagingResult.StoredFilePath));

        int fileCount = Directory.GetFiles(storageService.StorageDirectoryPath).Length;

        Assert.Equal(1, fileCount);
    }

    [Fact]
    public async Task PromoteStagedFile_TreatsACollisionWithIdenticalContentAsSuccess()
    {
        // The race the promotion must survive: both uploads staged their own
        // copy, and the other one promoted first. The staged copy is redundant
        // and must be discarded rather than making this upload fail.
        byte[] csvBytes = BuildCsvBytes();

        SpreadsheetStorageResult stagingResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(csvBytes),
            SpreadsheetFileFormat.Csv);

        string canonicalPath = Path.Combine(
            storageService.StorageDirectoryPath,
            stagingResult.RelativeStoredFilePath);

        await File.WriteAllBytesAsync(canonicalPath, csvBytes);

        storageService.PromoteStagedFile(
            stagingResult.StoredFilePath,
            stagingResult.RelativeStoredFilePath);

        Assert.True(File.Exists(canonicalPath));
        Assert.False(File.Exists(stagingResult.StoredFilePath));
        Assert.Equal(csvBytes, await File.ReadAllBytesAsync(canonicalPath));
    }

    [Fact]
    public async Task PromoteStagedFile_PropagatesACollisionWithDifferentContent()
    {
        // A file at the canonical name that does NOT match the hash it is named
        // after is corruption, not a successful duplicate. The promotion must
        // fail and leave the staged copy alone.
        byte[] expectedBytes = BuildCsvBytes("expected");

        SpreadsheetStorageResult stagingResult = await storageService.SaveIncomingFileAsync(
            new MemoryStream(expectedBytes),
            SpreadsheetFileFormat.Csv);

        string canonicalPath = Path.Combine(
            storageService.StorageDirectoryPath,
            stagingResult.RelativeStoredFilePath);

        await File.WriteAllBytesAsync(canonicalPath, BuildCsvBytes("different"));

        Assert.Throws<IOException>(() => storageService.PromoteStagedFile(
            stagingResult.StoredFilePath,
            stagingResult.RelativeStoredFilePath));

        Assert.True(File.Exists(stagingResult.StoredFilePath));
    }

    [Fact]
    public async Task SaveCurrentSpreadsheetAsync_ConcurrentIdenticalUploadsLeaveOneFileAndNoStaging()
    {
        byte[] csvBytes = BuildCsvBytes();

        Task<SpreadsheetStorageResult>[] uploadTasks = Enumerable
            .Range(0, 8)
            .Select(_ => storageService.SaveCurrentSpreadsheetAsync(
                new MemoryStream(csvBytes),
                SpreadsheetFileFormat.Csv))
            .ToArray();

        SpreadsheetStorageResult[] results = await Task.WhenAll(uploadTasks);

        Assert.All(
            results,
            result => Assert.Equal(results[0].RelativeStoredFilePath, result.RelativeStoredFilePath));

        string[] storedFiles = Directory.GetFiles(storageService.StorageDirectoryPath);

        Assert.Single(storedFiles);
        Assert.DoesNotContain(
            storedFiles,
            path => path.EndsWith(".incoming", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveIncomingFileAsync_RemovesTheStagingFileWhenTheWriteFails()
    {
        using FailingReadStream failingContent = new();

        await Assert.ThrowsAsync<IOException>(() => storageService.SaveIncomingFileAsync(
            failingContent,
            SpreadsheetFileFormat.Csv));

        string[] remainingFiles = Directory.GetFiles(storageService.StorageDirectoryPath);

        Assert.Empty(remainingFiles);
    }

    /// <summary>
    /// A read-only stream that yields one byte and then fails, so the storage
    /// service's write path can be driven into its failure branch.
    /// </summary>
    private sealed class FailingReadStream : Stream
    {
        private int readCount;

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new IOException("simulated read failure");
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            readCount++;

            if (readCount > 1)
            {
                throw new IOException("simulated read failure");
            }

            buffer.Span[0] = (byte)'x';

            return ValueTask.FromResult(1);
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
