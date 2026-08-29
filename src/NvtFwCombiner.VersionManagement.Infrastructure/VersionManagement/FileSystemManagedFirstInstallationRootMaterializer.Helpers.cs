using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
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
        long seedBytes = new FileInfo(Path.Combine(root, SeedFileName)).Length;
        return new WindowsStableTreeLimits(
            checked(FileSystemManagedVersionRepository.MaximumInstalledFiles + 3),
            checked(FileSystemManagedVersionRepository.MaximumInstalledDirectories + 2),
            checked(
                FileSystemManagedVersionRepository.MaximumInstalledBytes +
                payload.LauncherSize +
                payload.Bootstrap.Length +
                seedBytes));
    }

    private static bool HasClosedTopLevelInventory(string stagingRoot)
    {
        string[] expected =
        [
            BootstrapFileName,
            DistributionLauncherFileName,
            SeedFileName,
            FileSystemManagedVersionRepository.VersionsDirectoryName,
        ];
        try
        {
            string[] actual =
            [.. Directory.EnumerateFileSystemEntries(stagingRoot).Select(
                static path => Path.GetFileName(path) ?? string.Empty)];
            return actual.Order(StringComparer.Ordinal).SequenceEqual(
                    expected.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal) &&
                expected.All(name => !ManagedPathSafety.IsReparsePoint(Path.Combine(stagingRoot, name))) &&
                File.Exists(Path.Combine(stagingRoot, BootstrapFileName)) &&
                File.Exists(Path.Combine(stagingRoot, DistributionLauncherFileName)) &&
                File.Exists(Path.Combine(stagingRoot, SeedFileName)) &&
                Directory.Exists(Path.Combine(
                    stagingRoot,
                    FileSystemManagedVersionRepository.VersionsDirectoryName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async ValueTask<bool> VerifyClosedRootAsync(
        string root,
        ManagedDistributionPayloadIdentity payload,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        if (!await VerifyStagedRootAsync(root, payload, admission, cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }
        ManagedVersionInventoryReadResult inventory = await _repository.InventoryAsync(
            root,
            [admission],
            admission.Version,
            admission.Version,
            failedActivationVersion: null,
            cancellationToken).ConfigureAwait(false);
        ManagedVersionInventory? installed = inventory.Inventory;
        return inventory.IsSuccess && installed is not null &&
            installed.Versions.Count == 1 &&
            installed.HealthyCount == 1 &&
            installed.DamagedCount == 0;
    }

    private static async ValueTask<bool> VerifyStagedRootAsync(
        string root,
        ManagedDistributionPayloadIdentity payload,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        var seedStore = new JsonVersionManagerStateStore(
            Path.Combine(root, SeedFileName),
            allowUnboundSeedTemplate: true);
        VersionManagerStateLoadResult seed = await seedStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        VersionManagerState? seedState = seed.State;
        return seed.IsSuccess && seedState is not null &&
            ManagedVersionSeedPolicy.IsCanonicalFirstRunSeed(seedState) &&
            seedState.Admissions is [var only] &&
            only == admission &&
            HasClosedTopLevelInventory(root) &&
            await MatchesAsync(
                Path.Combine(root, DistributionLauncherFileName),
                payload.LauncherSize,
                payload.LauncherSha256,
                cancellationToken).ConfigureAwait(false) &&
            await MatchesAsync(
                Path.Combine(root, BootstrapFileName),
                payload.Bootstrap.Length,
                payload.Bootstrap.Sha256,
                cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> MatchesAsync(
        string path,
        long expectedSize,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || ManagedPathSafety.IsReparsePoint(path))
        {
            return false;
        }
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length != expectedSize)
        {
            return false;
        }
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return string.Equals(Convert.ToHexStringLower(hash), expectedSha256, StringComparison.Ordinal);
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

    private static ManagedSetupTransactionDocument? ParseMarker(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = EmbeddedVersionManagementSchema.ParseStrict(bytes, maximumDepth: 16);
        return ManagedSetupTransactionSchema.IsValid(document.RootElement)
            ? JsonSerializer.Deserialize(
                document.RootElement,
                MarkerJsonContext.ManagedSetupTransactionDocument)
            : null;
    }

    private static byte[] SerializeMarker(ManagedSetupTransactionDocument marker)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            marker,
            MarkerJsonContext.ManagedSetupTransactionDocument);
        return bytes.Length <= MaximumMarkerBytes && ParseMarker(bytes) is not null
            ? bytes
            : throw new InvalidDataException("Setup marker violated its canonical schema.");
    }

    private static async ValueTask<ManagedSetupTransactionDocument?> ReadMarkerAsync(
        FileStream stream,
        CancellationToken cancellationToken)
    {
        if (stream.Length is < 1 or > MaximumMarkerBytes)
        {
            return null;
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.Position = 0;
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return stream.Position == stream.Length ? ParseMarker(bytes) : null;
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
            actual = await ReadMarkerAsync(stream, cancellationToken).ConfigureAwait(false);
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
        byte[] bytes = SerializeMarker(replacement);
        ManagedSetupTransactionDocument? actual = await ReadMarkerAsync(stream, cancellationToken)
            .ConfigureAwait(false);
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
