using System.Buffers;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Verifies one immutable staged artifact without materializing another complete file copy.</summary>
internal static class StagedArtifactFileVerifier
{
    private const int ComparisonBufferLength = 128 * 1024;

    /// <summary>Compares the complete file length and bytes using bounded, cleared pooled storage.</summary>
    public static async ValueTask<bool> MatchesAsync(
        string path,
        ReadOnlyMemory<byte> expectedBytes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        if (stream.Length != expectedBytes.Length)
        {
            return false;
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(
            Math.Min(ComparisonBufferLength, Math.Max(1, expectedBytes.Length)));
        try
        {
            int offset = 0;
            while (offset < expectedBytes.Length)
            {
                int requestedLength = Math.Min(ComparisonBufferLength, expectedBytes.Length - offset);
                int bytesRead = await stream
                    .ReadAsync(buffer.AsMemory(0, requestedLength), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0 ||
                    !buffer.AsSpan(0, bytesRead).SequenceEqual(expectedBytes.Span.Slice(offset, bytesRead)))
                {
                    return false;
                }

                offset += bytesRead;
            }

            return stream.Position == stream.Length && stream.Length == expectedBytes.Length;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}

internal static class ExternalStagingDirectory
{
    internal static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
