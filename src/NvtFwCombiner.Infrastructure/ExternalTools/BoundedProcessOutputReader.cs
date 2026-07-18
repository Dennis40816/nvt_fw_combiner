using System.Buffers;
using System.Text;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Continuously drains one process stream while retaining bounded diagnostic context.</summary>
internal static class BoundedProcessOutputReader
{
    internal const int MaximumCapturedCharacters = 64 * 1024;
    internal const string TruncationMarker = "\n...[process output truncated]...\n";

    private const int PrefixLength = MaximumCapturedCharacters / 2;
    private const int ReadBufferLength = 4096;
    private const int TailLength = MaximumCapturedCharacters - PrefixLength;

    internal static async Task<string> ReadAsync(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        char[] readBuffer = ArrayPool<char>.Shared.Rent(ReadBufferLength);
        char[]? tail = null;
        var prefix = new StringBuilder();
        long totalCharacters = 0;
        int tailCount = 0;
        int tailWriteIndex = 0;
        try
        {
            while (true)
            {
                int read = await reader.ReadAsync(
                    readBuffer.AsMemory(0, ReadBufferLength),
                    CancellationToken.None).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                totalCharacters = totalCharacters > long.MaxValue - read
                    ? long.MaxValue
                    : totalCharacters + read;
                int offset = 0;
                if (prefix.Length < PrefixLength)
                {
                    int prefixCount = Math.Min(PrefixLength - prefix.Length, read);
                    _ = prefix.Append(readBuffer, 0, prefixCount);
                    offset = prefixCount;
                }

                if (offset < read)
                {
                    tail ??= ArrayPool<char>.Shared.Rent(TailLength);
                    AppendTail(
                        readBuffer.AsSpan(offset, read - offset),
                        tail.AsSpan(0, TailLength),
                        ref tailWriteIndex,
                        ref tailCount);
                }
            }

            return totalCharacters <= MaximumCapturedCharacters
                ? CreateCompleteOutput(prefix, tail, tailCount)
                : CreateTruncatedOutput(prefix, tail!, tailWriteIndex);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(readBuffer, clearArray: true);
            if (tail is not null)
            {
                ArrayPool<char>.Shared.Return(tail, clearArray: true);
            }
        }
    }

    private static void AppendTail(
        ReadOnlySpan<char> source,
        Span<char> tail,
        ref int writeIndex,
        ref int count)
    {
        if (source.IsEmpty)
        {
            return;
        }

        if (source.Length >= tail.Length)
        {
            source[^tail.Length..].CopyTo(tail);
            writeIndex = 0;
            count = tail.Length;
            return;
        }

        int firstCount = Math.Min(source.Length, tail.Length - writeIndex);
        source[..firstCount].CopyTo(tail[writeIndex..]);
        source[firstCount..].CopyTo(tail);
        writeIndex = (writeIndex + source.Length) % tail.Length;
        count = Math.Min(tail.Length, count + source.Length);
    }

    private static string CreateCompleteOutput(StringBuilder prefix, char[]? tail, int tailCount)
    {
        return tailCount == 0
            ? prefix.ToString()
            : string.Concat(prefix.ToString(), new string(tail!, 0, tailCount));
    }

    private static string CreateTruncatedOutput(StringBuilder prefix, char[] tail, int tailWriteIndex)
    {
        if (prefix.Length > 0 && char.IsHighSurrogate(prefix[^1]))
        {
            prefix.Length--;
        }

        int retainedTailLength = MaximumCapturedCharacters - prefix.Length - TruncationMarker.Length;
        int retainedTailStart = (tailWriteIndex + TailLength - retainedTailLength) % TailLength;
        if (retainedTailLength > 0 && char.IsLowSurrogate(tail[retainedTailStart]))
        {
            retainedTailStart = (retainedTailStart + 1) % TailLength;
            retainedTailLength--;
        }

        if (retainedTailLength > 0)
        {
            int retainedTailEnd = (retainedTailStart + retainedTailLength - 1) % TailLength;
            if (char.IsHighSurrogate(tail[retainedTailEnd]))
            {
                retainedTailLength--;
            }
        }

        var result = new StringBuilder(MaximumCapturedCharacters);
        _ = result.Append(prefix);
        _ = result.Append(TruncationMarker);
        int firstCount = Math.Min(retainedTailLength, TailLength - retainedTailStart);
        _ = result.Append(tail, retainedTailStart, firstCount);
        _ = result.Append(tail, 0, retainedTailLength - firstCount);
        return result.ToString();
    }
}
