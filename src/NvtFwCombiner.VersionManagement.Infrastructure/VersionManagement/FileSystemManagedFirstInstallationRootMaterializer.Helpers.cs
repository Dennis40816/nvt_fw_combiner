using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

public sealed partial class FileSystemManagedFirstInstallationRootMaterializer
{
    internal static WindowsStableTreeLimits CreateSetupTreeLimits(
        string root,
        ManagedDistributionPayloadIdentity payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return CreateSetupTreeLimits(
            root,
            payload.LauncherSize,
            payload.Bootstrap.Length);
    }

    internal static WindowsStableTreeLimits CreateSetupTreeLimits(
        string root,
        ManagedSetupRecoveryPayloadIdentity payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return CreateSetupTreeLimits(
            root,
            payload.LauncherSize,
            payload.BootstrapSize);
    }

    private static WindowsStableTreeLimits CreateSetupTreeLimits(
        string root,
        long launcherSize,
        long bootstrapSize)
    {
        long seedBytes = new FileInfo(Path.Combine(root, SeedFileName)).Length;
        return new WindowsStableTreeLimits(
            checked(FileSystemManagedVersionRepository.MaximumInstalledFiles + 3),
            checked(FileSystemManagedVersionRepository.MaximumInstalledDirectories + 2),
            checked(
                FileSystemManagedVersionRepository.MaximumInstalledBytes +
                launcherSize +
                bootstrapSize +
                seedBytes));
    }

    private ValueTask<bool> VerifyClosedRootAsync(
        string root,
        ManagedDistributionPayloadIdentity payload,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        return new ManagedSetupClosedRootVerifier(_repository).VerifyAsync(
            root,
            payload,
            admission,
            cancellationToken);
    }

    private static ValueTask<bool> VerifyStagedRootAsync(
        string root,
        ManagedDistributionPayloadIdentity payload,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        return ManagedSetupClosedRootVerifier.VerifyPayloadAndSeedAsync(
            root,
            payload,
            admission,
            cancellationToken);
    }

    private static ValueTask<bool> MatchesAsync(
        string path,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        return ManagedSetupClosedRootVerifier.MatchesAsync(
            path,
            expectedSize,
            expectedSha256,
            cancellationToken);
    }

    private static ManagedFirstInstallationMaterializationIssue MapInstallIssue(
        ManagedVersionInstallIssue issue)
    {
        return issue switch
        {
            ManagedVersionInstallIssue.PackageUnavailable =>
                ManagedFirstInstallationMaterializationIssue.SourceUnavailable,
            ManagedVersionInstallIssue.PackageMismatch or
            ManagedVersionInstallIssue.UnsafeArchive or
            ManagedVersionInstallIssue.InvalidPayload or
            ManagedVersionInstallIssue.IdentityConflict =>
                ManagedFirstInstallationMaterializationIssue.SourceChanged,
            ManagedVersionInstallIssue.PromotionFailed =>
                ManagedFirstInstallationMaterializationIssue.PromotionFailed,
            ManagedVersionInstallIssue.CleanupIncomplete =>
                ManagedFirstInstallationMaterializationIssue.RecoveryRequired,
            ManagedVersionInstallIssue.StateUnavailable =>
                ManagedFirstInstallationMaterializationIssue.StateUnavailable,
            ManagedVersionInstallIssue.None =>
                ManagedFirstInstallationMaterializationIssue.SourceChanged,
            _ => throw new InvalidOperationException("Repository returned an undefined install issue."),
        };
    }

    internal static ManagedFirstInstallationMaterializationIssue MapObservedRoot(
        ManagedInstallationRootStatus status)
    {
        return status switch
        {
            ManagedInstallationRootStatus.InvalidDestination =>
                ManagedFirstInstallationMaterializationIssue.InvalidDestination,
            ManagedInstallationRootStatus.PermissionDenied =>
                ManagedFirstInstallationMaterializationIssue.PermissionDenied,
            ManagedInstallationRootStatus.Unavailable =>
                ManagedFirstInstallationMaterializationIssue.StateUnavailable,
            ManagedInstallationRootStatus.Absent or
            ManagedInstallationRootStatus.Present or
            ManagedInstallationRootStatus.Residue =>
                ManagedFirstInstallationMaterializationIssue.RecoveryRequired,
            _ => throw new InvalidOperationException("Root probe returned an undefined status."),
        };
    }

    private static bool RemoveEmptyRepositoryStaging(
        WindowsManagedSetupPathCustody custody,
        string root,
        Action<string>? beforeDelete)
    {
        string staging = Path.Combine(root, FileSystemManagedVersionRepository.StagingDirectoryName);
        if (!Directory.Exists(staging))
        {
            return true;
        }
        try
        {
            return custody.DeleteEmptyStagingChild(staging, beforeDelete) &&
                !Directory.Exists(staging);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "The native marker handle transfers to FileStream and is disposed on every failure path.")]
    private static async ValueTask<MarkerCreateResult> CreateNewMarkerAsync(
        WindowsManagedSetupPathCustody custody,
        string markerName,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        ManagedFirstInstallationMaterializationIssue created = custody.CreateMarker(
            markerName,
            out SafeFileHandle handle);
        if (created != ManagedFirstInstallationMaterializationIssue.None)
        {
            handle.Dispose();
            return new(null, created);
        }
        SafeFileHandle? ownedHandle = handle;
        FileStream stream;
        try
        {
            stream = new FileStream(
                ownedHandle,
                FileAccess.ReadWrite,
                bufferSize: 16 * 1024,
                isAsync: false);
            ownedHandle = null;
        }
        finally
        {
            ownedHandle?.Dispose();
        }
        try
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
            stream.Position = 0;
            return new(stream, ManagedFirstInstallationMaterializationIssue.None);
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed record MarkerCreateResult(
        FileStream? Stream,
        ManagedFirstInstallationMaterializationIssue Issue)
    {
        internal bool IsSuccess => Stream is not null &&
            Issue == ManagedFirstInstallationMaterializationIssue.None;
    }

    private static async ValueTask<ExactMarkerDeleteResult> DeleteExactMarkerAsync(
        FileStream stream,
        ManagedSetupTransactionDocument expected,
        Func<CancellationToken, ValueTask<bool>> revalidateRoot,
        Func<bool> revalidateTopology,
        Action? afterTopologyProof,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!WindowsStablePathCustody.HasExpectedType(stream.SafeFileHandle, directory: false))
        {
            return ExactMarkerDeleteResult.Mismatch;
        }

        ManagedSetupTransactionDocument? actual;
        try
        {
            actual = await ManagedSetupTransactionCodec.ReadAsync(stream, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is
            JsonException or InvalidDataException or InvalidOperationException)
        {
            return ExactMarkerDeleteResult.Mismatch;
        }
        if (actual is null || !ManagedSetupTransactionDocument.Equivalent(actual, expected))
        {
            return ExactMarkerDeleteResult.Mismatch;
        }
        if (!await revalidateRoot(cancellationToken).ConfigureAwait(false) ||
            !revalidateTopology())
        {
            return ExactMarkerDeleteResult.Mismatch;
        }
        if (afterTopologyProof is not null)
        {
            afterTopologyProof();
            if (!revalidateTopology())
            {
                return ExactMarkerDeleteResult.Mismatch;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return WindowsStablePathCustody.MarkDeleteOnClose(stream.SafeFileHandle)
                ? ExactMarkerDeleteResult.Deleted
                : ExactMarkerDeleteResult.Unavailable;
    }

    private static async ValueTask ReplaceExactMarkerAsync(
        FileStream stream,
        ManagedSetupTransactionDocument expected,
        ManagedSetupTransactionDocument replacement,
        Func<bool> revalidateTopology,
        Action? afterTopologyProof,
        CancellationToken cancellationToken)
    {
        byte[] bytes = ManagedSetupTransactionCodec.Serialize(replacement);
        ManagedSetupTransactionDocument? actual = await ManagedSetupTransactionCodec.ReadAsync(
            stream,
            cancellationToken).ConfigureAwait(false);
        if (actual is null || !ManagedSetupTransactionDocument.Equivalent(actual, expected))
        {
            throw new InvalidDataException("Setup marker identity or phase changed.");
        }
        if (!revalidateTopology())
        {
            throw new InvalidDataException("Setup root topology changed after verification.");
        }
        if (afterTopologyProof is not null)
        {
            afterTopologyProof();
            if (!revalidateTopology())
            {
                throw new InvalidDataException("Setup root topology changed at marker mutation.");
            }
        }
        stream.Position = 0;
        stream.SetLength(0);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static ManagedFirstInstallationMaterializationResult Failure(
        ManagedFirstInstallationMaterializationIssue issue)
    {
        return new(null, issue);
    }
}
