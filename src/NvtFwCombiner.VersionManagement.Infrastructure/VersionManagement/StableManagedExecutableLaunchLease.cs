using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Holds one verified executable and its complete admitted tree through Process.Start.</summary>
internal sealed class StableManagedExecutableLaunchLease : IManagedExecutableLaunchLease
{
    private readonly WindowsStablePathCustody _custody;
    private readonly FileStream _stream;

    private StableManagedExecutableLaunchLease(
        string executablePath,
        FileStream stream,
        WindowsStablePathCustody custody)
    {
        ExecutablePath = executablePath;
        WorkingDirectory = Path.GetDirectoryName(executablePath)!;
        _stream = stream;
        _custody = custody;
    }

    public string ExecutablePath { get; }

    public string WorkingDirectory { get; }

    public bool TryValidateForStart()
    {
        return _custody.RevalidateClosedTree();
    }

    public void Dispose()
    {
        try
        {
            _stream.Dispose();
        }
        finally
        {
            _custody.Dispose();
        }
    }

    internal async ValueTask CopyToAsync(
        string destination,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        _stream.Position = 0;
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await _stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        output.Flush(flushToDisk: true);
        _stream.Position = 0;
    }

    internal static async ValueTask<StableManagedExecutableMeasurementResult> TryMeasureAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        StableManagedExecutableAcquisitionResult acquired = await TryAcquireMeasuredCoreAsync(
            executablePath,
            beforeStableOpen: null,
            cancellationToken).ConfigureAwait(false);
        acquired.Lease?.Dispose();
        return acquired.IsAcquired
            ? new(acquired.Length, acquired.Sha256, ManagedExecutableLaunchIssue.None)
            : StableManagedExecutableMeasurementResult.Failure(acquired.Issue);
    }

    internal static ValueTask<StableManagedExecutableAcquisitionResult> TryAcquireMeasuredAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        return TryAcquireMeasuredCoreAsync(
            executablePath,
            beforeStableOpen: null,
            cancellationToken);
    }

    internal static ValueTask<ManagedExecutableLaunchLeaseResult> TryAcquireAsync(
        string executablePath,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        return TryAcquireAsync(
            executablePath,
            expectedSize,
            expectedSha256,
            beforeStableOpen: null,
            cancellationToken);
    }

    internal static async ValueTask<ManagedExecutableLaunchLeaseResult> TryAcquireAsync(
        string executablePath,
        long expectedSize,
        string expectedSha256,
        Action? beforeStableOpen,
        CancellationToken cancellationToken)
    {
        if (expectedSize <= 0 || string.IsNullOrWhiteSpace(expectedSha256))
        {
            return new(null, ManagedExecutableLaunchIssue.Tampered);
        }
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireFile(
            executablePath,
            AdaptHook(beforeStableOpen),
            cancellationToken);
        return acquired.IsAcquired
            ? await TryCreateAsync(
                acquired.Custody!,
                Path.GetFileName(Path.GetFullPath(executablePath)),
                expectedSize,
                expectedSha256,
                cancellationToken).ConfigureAwait(false)
            : new(null, MapIssue(acquired.Issue));
    }

    /// <summary>
    /// Consumes complete tree custody on every path and returns it only as part of a verified lease.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Successful stream and custody ownership transfer into the returned lease; finally owns failures.")]
    internal static async ValueTask<ManagedExecutableLaunchLeaseResult> TryCreateAsync(
        WindowsStablePathCustody ownedCustody,
        string executableRelativePath,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        return await TryCreateCoreAsync(
            ownedCustody,
            executableRelativePath,
            expectedSize,
            expectedSha256,
            verifyContent: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Consumes custody whose complete package bytes were already verifier-proven.</summary>
    internal static ValueTask<ManagedExecutableLaunchLeaseResult> TryCreateFromVerifiedTreeAsync(
        WindowsStablePathCustody ownedCustody,
        string executableRelativePath,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        return TryCreateCoreAsync(
            ownedCustody,
            executableRelativePath,
            expectedSize,
            expectedSha256,
            verifyContent: false,
            cancellationToken);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Successful stream and custody ownership transfer into the returned lease; finally owns failures.")]
    private static async ValueTask<ManagedExecutableLaunchLeaseResult> TryCreateCoreAsync(
        WindowsStablePathCustody ownedCustody,
        string executableRelativePath,
        long expectedSize,
        string expectedSha256,
        bool verifyContent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownedCustody);
        FileStream? stream = null;
        WindowsStablePathCustody? custody = ownedCustody;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expectedSize <= 0 || string.IsNullOrWhiteSpace(expectedSha256))
            {
                return new(null, ManagedExecutableLaunchIssue.Tampered);
            }
            string executablePath = custody.GetAbsoluteFilePath(executableRelativePath);
            stream = custody.OpenReadOnlyFile(executableRelativePath);
            if (stream.Length != expectedSize || !HasPortableExecutableHeader(stream))
            {
                return new(null, ManagedExecutableLaunchIssue.Tampered);
            }
            if (verifyContent)
            {
                StableManagedExecutableMeasurementResult measured = await MeasureAsync(
                    stream,
                    expectedSize,
                    expectedSha256,
                    cancellationToken).ConfigureAwait(false);
                if (!measured.IsMeasured)
                {
                    return new(null, measured.Issue);
                }
            }
            if (!custody.RevalidateClosedTree())
            {
                return new(null, ManagedExecutableLaunchIssue.UnsafePath);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var lease = new StableManagedExecutableLaunchLease(executablePath, stream, custody);
            stream = null;
            custody = null;
            return new(lease, ManagedExecutableLaunchIssue.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(null, ManagedExecutableLaunchIssue.Unavailable);
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            custody?.Dispose();
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Successful stream and custody ownership transfer into the returned lease; finally owns failures.")]
    private static async ValueTask<StableManagedExecutableAcquisitionResult> TryAcquireMeasuredCoreAsync(
        string executablePath,
        Action? beforeStableOpen,
        CancellationToken cancellationToken)
    {
        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireFile(
            executablePath,
            AdaptHook(beforeStableOpen),
            cancellationToken);
        if (!acquired.IsAcquired)
        {
            return StableManagedExecutableAcquisitionResult.Failure(MapIssue(acquired.Issue));
        }

        WindowsStablePathCustody? custody = acquired.Custody!;
        FileStream? stream = null;
        try
        {
            string relative = Path.GetFileName(Path.GetFullPath(executablePath));
            string absolute = custody.GetAbsoluteFilePath(relative);
            stream = custody.OpenReadOnlyFile(relative);
            StableManagedExecutableMeasurementResult measured = await MeasureAsync(
                stream,
                expectedSize: null,
                expectedSha256: null,
                cancellationToken).ConfigureAwait(false);
            if (!measured.IsMeasured)
            {
                return StableManagedExecutableAcquisitionResult.Failure(measured.Issue);
            }
            var lease = new StableManagedExecutableLaunchLease(absolute, stream, custody);
            stream = null;
            custody = null;
            return new(
                lease,
                measured.Length,
                measured.Sha256,
                ManagedExecutableLaunchIssue.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return StableManagedExecutableAcquisitionResult.Failure(
                ManagedExecutableLaunchIssue.Unavailable);
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            custody?.Dispose();
        }
    }

    private static async ValueTask<StableManagedExecutableMeasurementResult> MeasureAsync(
        FileStream stream,
        long? expectedSize,
        string? expectedSha256,
        CancellationToken cancellationToken)
    {
        long length = stream.Length;
        if ((expectedSize is not null && length != expectedSize.Value) ||
            !HasPortableExecutableHeader(stream))
        {
            return StableManagedExecutableMeasurementResult.Failure(
                ManagedExecutableLaunchIssue.Tampered);
        }
        stream.Position = 0;
        string sha256 = Convert.ToHexStringLower(
            await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        stream.Position = 0;
        return expectedSha256 is not null &&
               !string.Equals(sha256, expectedSha256, StringComparison.Ordinal)
            ? StableManagedExecutableMeasurementResult.Failure(ManagedExecutableLaunchIssue.Tampered)
            : new(length, sha256, ManagedExecutableLaunchIssue.None);
    }

    private static Action<WindowsStableCustodyStage>? AdaptHook(Action? hook)
    {
        return hook is null
            ? null
            : stage =>
            {
                if (stage == WindowsStableCustodyStage.BeforeRootOpen)
                {
                    hook();
                }
            };
    }

    private static ManagedExecutableLaunchIssue MapIssue(WindowsStableCustodyIssue issue)
    {
        return issue switch
        {
            WindowsStableCustodyIssue.None => ManagedExecutableLaunchIssue.None,
            WindowsStableCustodyIssue.InvalidPath or WindowsStableCustodyIssue.ReparsePoint or
            WindowsStableCustodyIssue.Changed => ManagedExecutableLaunchIssue.UnsafePath,
            WindowsStableCustodyIssue.AccessDenied or WindowsStableCustodyIssue.Contended or
            WindowsStableCustodyIssue.Unavailable => ManagedExecutableLaunchIssue.Unavailable,
            _ => throw new InvalidOperationException("Stable custody returned an undefined issue."),
        };
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
}

internal sealed record StableManagedExecutableAcquisitionResult(
    StableManagedExecutableLaunchLease? Lease,
    long Length,
    string Sha256,
    ManagedExecutableLaunchIssue Issue)
{
    internal bool IsAcquired => Lease is not null && Issue == ManagedExecutableLaunchIssue.None;

    internal static StableManagedExecutableAcquisitionResult Failure(
        ManagedExecutableLaunchIssue issue)
    {
        return new(null, 0, string.Empty, issue);
    }
}

internal sealed record StableManagedExecutableMeasurementResult(
    long Length,
    string Sha256,
    ManagedExecutableLaunchIssue Issue)
{
    internal bool IsMeasured =>
        Length > 0 && Sha256.Length == 64 && Issue == ManagedExecutableLaunchIssue.None;

    internal static StableManagedExecutableMeasurementResult Failure(
        ManagedExecutableLaunchIssue issue)
    {
        return new(0, string.Empty, issue);
    }
}
