// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text;

namespace NvtFwCombiner.TestSupport;

/// <summary>
/// Reads whole golden files for assertions. It avoids the repository's banned <c>File.ReadAll*</c> overloads and keeps
/// their exception behavior: a missing file or an access error surfaces as the same IO exception.
/// </summary>
public static class GoldenFileIo
{
    /// <summary>Reads every byte of a file.</summary>
    public static byte[] ReadAllBytes(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>Reads every byte of a file asynchronously.</summary>
    public static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    /// <summary>Creates or replaces a file with the given bytes.</summary>
    public static async Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        await using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a text file asynchronously, detecting a byte order mark and defaulting to UTF-8.</summary>
    public static async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
