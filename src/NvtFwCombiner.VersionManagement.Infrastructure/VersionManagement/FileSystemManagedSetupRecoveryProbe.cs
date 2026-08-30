using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Reads one exact Setup marker through stable no-follow Windows custody.</summary>
public sealed class FileSystemManagedSetupRecoveryProbe : IManagedSetupRecoveryProbe
{
    private readonly Func<string, CancellationToken, WindowsStableCustodyResult> _acquire;
    private readonly Func<WindowsStablePathCustody, bool> _revalidate;

    /// <summary>Creates the production local filesystem adapter.</summary>
    public FileSystemManagedSetupRecoveryProbe()
        : this(
            static (path, cancellationToken) =>
                WindowsStablePathCustody.TryAcquireFile(path, cancellationToken: cancellationToken),
            static custody => custody.RevalidateClosedTree())
    {
    }

    internal FileSystemManagedSetupRecoveryProbe(
        Func<string, CancellationToken, WindowsStableCustodyResult> acquire,
        Func<WindowsStablePathCustody, bool> revalidate)
    {
        _acquire = acquire ?? throw new ArgumentNullException(nameof(acquire));
        _revalidate = revalidate ?? throw new ArgumentNullException(nameof(revalidate));
    }

    /// <inheritdoc />
    public ValueTask<ManagedSetupRecoveryFact> ObserveAsync(
        string managedRoot,
        string statePathIdentity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ManagedPathSafety.TryNormalizeExactAbsolutePath(managedRoot, out string root) ||
            !ManagedPathSafety.TryNormalizeExactAbsolutePath(statePathIdentity, out string statePath))
        {
            return ValueTask.FromResult(Fact(ManagedSetupRecoveryFactKind.IdentityMismatch));
        }

        string markerPath = FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root);
        WindowsStableCustodyResult acquired = _acquire(markerPath, cancellationToken);
        if (!acquired.IsAcquired)
        {
            ManagedSetupRecoveryFactKind kind = acquired.IsExactChildMissing
                ? ManagedSetupRecoveryFactKind.Absent
                : MapCustodyIssue(acquired.Issue);
            acquired.Custody?.Dispose();
            return ValueTask.FromResult(Fact(kind));
        }

        return ObserveHeldAsync(
            acquired.Custody!,
            Path.GetFileName(markerPath),
            root,
            statePath,
            cancellationToken);
    }

    private async ValueTask<ManagedSetupRecoveryFact> ObserveHeldAsync(
        WindowsStablePathCustody custody,
        string markerName,
        string root,
        string statePath,
        CancellationToken cancellationToken)
    {
        using (custody)
        {
            try
            {
                await using FileStream stream = custody.OpenReadOnlyFile(markerName);
                ManagedSetupTransactionDocument? marker = await ManagedSetupTransactionCodec
                    .ReadAsync(stream, cancellationToken).ConfigureAwait(false);
                return marker is null
                    ? Fact(ManagedSetupRecoveryFactKind.Malformed)
                    : !_revalidate(custody)
                    ? Fact(ManagedSetupRecoveryFactKind.Changed)
                    : TryProjectExact(
                        marker,
                        root,
                        statePath,
                        out ManagedSetupRecoveryTransaction? exact)
                            ? new(ManagedSetupRecoveryFactKind.Exact, exact)
                            : Fact(ManagedSetupRecoveryFactKind.IdentityMismatch);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                return Fact(ManagedSetupRecoveryFactKind.AccessDenied);
            }
            catch (Exception exception) when (exception is
                IOException or InvalidDataException or NotSupportedException)
            {
                return Fact(ManagedSetupRecoveryFactKind.Unavailable);
            }
        }
    }

    private static bool TryProjectExact(
        ManagedSetupTransactionDocument marker,
        string root,
        string statePath,
        out ManagedSetupRecoveryTransaction? transaction)
    {
        transaction = null;
        if (!ManagedPathSafety.PathComparer.Equals(marker.ManagedRootIdentity, root) ||
            !ManagedPathSafety.PathComparer.Equals(marker.StatePathIdentity, statePath) ||
            !TryMapPhase(marker.Phase, out ManagedSetupRecoveryPhase phase))
        {
            return false;
        }

        string[] ownedPaths =
        [
            Path.GetFileName(root),
            Path.GetFileName(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(root)),
            string.Join(
                '/',
                Path.GetFileName(FileSystemManagedInstallationRootProbe.GetStagingContainerPath(root)),
                marker.TransactionId),
        ];
        if (!marker.OwnedPaths.SequenceEqual(ownedPaths, StringComparer.Ordinal))
        {
            return false;
        }

        transaction = new(
            marker.TransactionId,
            root,
            statePath,
            phase,
            ownedPaths,
            new(
                marker.DistributionLauncherExecutable.Size,
                marker.DistributionLauncherExecutable.Sha256,
                marker.PayloadAdmission.DescriptorSize,
                marker.PayloadAdmission.DescriptorSha256,
                marker.PayloadAdmission.BootstrapInstalledFileName,
                marker.PayloadAdmission.BootstrapSize,
                marker.PayloadAdmission.BootstrapSha256),
            new(
                marker.Candidate.RegistryRevision,
                marker.Candidate.RegistryDigest,
                marker.Candidate.CatalogSchemaVersion,
                marker.Candidate.CatalogLatestVersion,
                marker.Candidate.CatalogDigest,
                marker.Candidate.CatalogPath,
                marker.Candidate.RegistryId,
                marker.Candidate.SourceRoot,
                marker.Candidate.SourceStatus,
                marker.Candidate.Version,
                marker.Candidate.PackagePath,
                marker.Candidate.PackageSize,
                marker.Candidate.PackageSha256,
                marker.Candidate.ReleaseManifestSha256,
                marker.Candidate.EntryIdentity));
        return true;
    }

    private static bool TryMapPhase(string value, out ManagedSetupRecoveryPhase phase)
    {
        phase = value switch
        {
            ManagedSetupTransactionCodec.StagingPhase => ManagedSetupRecoveryPhase.Staging,
            ManagedSetupTransactionCodec.RootPromotedPhase => ManagedSetupRecoveryPhase.RootPromoted,
            ManagedSetupTransactionCodec.BootstrapLaunchRecordedPhase =>
                ManagedSetupRecoveryPhase.BootstrapLaunchRecorded,
            _ => default,
        };
        return value is ManagedSetupTransactionCodec.StagingPhase or
            ManagedSetupTransactionCodec.RootPromotedPhase or
            ManagedSetupTransactionCodec.BootstrapLaunchRecordedPhase;
    }

    private static ManagedSetupRecoveryFactKind MapCustodyIssue(WindowsStableCustodyIssue issue)
    {
        return issue switch
        {
            WindowsStableCustodyIssue.AccessDenied => ManagedSetupRecoveryFactKind.AccessDenied,
            WindowsStableCustodyIssue.Changed => ManagedSetupRecoveryFactKind.Changed,
            WindowsStableCustodyIssue.Contended => ManagedSetupRecoveryFactKind.Unavailable,
            WindowsStableCustodyIssue.InvalidPath or WindowsStableCustodyIssue.ReparsePoint =>
                ManagedSetupRecoveryFactKind.IdentityMismatch,
            WindowsStableCustodyIssue.Unavailable => ManagedSetupRecoveryFactKind.Unavailable,
            WindowsStableCustodyIssue.None => ManagedSetupRecoveryFactKind.Unavailable,
            _ => ManagedSetupRecoveryFactKind.Unavailable,
        };
    }

    private static ManagedSetupRecoveryFact Fact(ManagedSetupRecoveryFactKind kind)
    {
        return new(kind, transaction: null);
    }
}
