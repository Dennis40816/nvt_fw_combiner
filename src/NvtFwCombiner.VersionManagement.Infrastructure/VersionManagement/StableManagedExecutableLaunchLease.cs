using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Holds one repository-verified executable open against write/delete through Process.Start.</summary>
internal sealed class StableManagedExecutableLaunchLease : IManagedExecutableLaunchLease
{
    private readonly FileStream _stream;

    private StableManagedExecutableLaunchLease(string executablePath, FileStream stream)
    {
        ExecutablePath = executablePath;
        WorkingDirectory = Path.GetDirectoryName(executablePath)!;
        _stream = stream;
    }

    public string ExecutablePath { get; }

    public string WorkingDirectory { get; }

    public void Dispose()
    {
        _stream.Dispose();
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Successful ownership transfers into the returned launch lease; all failure paths dispose in finally.")]
    internal static async ValueTask<ManagedExecutableLaunchLeaseResult> TryAcquireAsync(
        string executablePath,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        FileStream? stream = null;
        try
        {
            if (expectedSize <= 0 || ManagedPathSafety.IsReparsePoint(executablePath))
            {
                return Failure(ManagedExecutableLaunchIssue.UnsafePath);
            }
            stream = new FileStream(
                executablePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.RandomAccess);
            if (stream.Length != expectedSize || !HasPortableExecutableHeader(stream))
            {
                return Failure(ManagedExecutableLaunchIssue.Tampered);
            }
            stream.Position = 0;
            string actualSha256 = Convert.ToHexStringLower(
                await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            {
                return Failure(ManagedExecutableLaunchIssue.Tampered);
            }
            stream.Position = 0;
            var lease = new StableManagedExecutableLaunchLease(executablePath, stream);
            stream = null;
            return new(lease, ManagedExecutableLaunchIssue.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Failure(ManagedExecutableLaunchIssue.Unavailable);
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static bool HasPortableExecutableHeader(Stream stream)
    {
        if (stream.Length < 64)
        {
            return false;
        }
        Span<byte> dosHeader = stackalloc byte[64];
        stream.Position = 0;
        stream.ReadExactly(dosHeader);
        int peHeaderOffset = BinaryPrimitives.ReadInt32LittleEndian(dosHeader[0x3C..]);
        if (dosHeader[0] != (byte)'M' || dosHeader[1] != (byte)'Z' ||
            peHeaderOffset < dosHeader.Length || peHeaderOffset > stream.Length - 4)
        {
            return false;
        }
        stream.Position = peHeaderOffset;
        Span<byte> peSignature = stackalloc byte[4];
        stream.ReadExactly(peSignature);
        return peSignature.SequenceEqual("PE\0\0"u8);
    }

    private static ManagedExecutableLaunchLeaseResult Failure(ManagedExecutableLaunchIssue issue)
    {
        return new(null, issue);
    }
}
