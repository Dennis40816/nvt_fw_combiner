// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text;

namespace NvtFwCombiner.TestSupport;

/// <summary>
/// Reads golden files without exposing missing paths. Missing files and directories retain their IO exception types.
/// </summary>
public static class GoldenFileIo
{
    /// <summary>Reads every byte of a file.</summary>
    public static byte[] ReadAllBytes(string path)
    {
        using FileStream stream = OpenRead(path);
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>Reads every byte of a file asynchronously.</summary>
    public static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = OpenRead(path, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    /// <summary>Reads a text file asynchronously, detecting a byte order mark and defaulting to UTF-8.</summary>
    public static async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        using FileStream stream = OpenRead(path);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a text file, detecting a byte order mark and defaulting to UTF-8.</summary>
    public static string ReadAllText(string path)
    {
        using FileStream stream = OpenRead(path);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>Opens a golden file and replaces missing-path diagnostics without retaining an inner exception.</summary>
    public static FileStream OpenRead(string path, FileOptions options = FileOptions.None)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, options);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException("Golden file was not found.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new DirectoryNotFoundException("Golden directory was not found.");
        }
    }
}
