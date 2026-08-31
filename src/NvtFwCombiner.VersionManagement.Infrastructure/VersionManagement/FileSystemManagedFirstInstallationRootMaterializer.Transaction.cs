using System.Text.Json;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

public sealed partial class FileSystemManagedFirstInstallationRootMaterializer
{
    private sealed class PromotedInstallation(
        string managedRoot,
        ManagedVersionAdmission admission,
        ManagedImmutableBootstrapIdentity bootstrapIdentity,
        ManagedSetupTransactionDocument marker,
        FileStream markerStream,
        WindowsManagedSetupPathCustody custody,
        Func<CancellationToken, ValueTask<bool>> revalidateRoot,
        Action? afterMarkerTopologyProof)
        : IManagedPromotedFirstInstallation
    {
        private readonly ManagedImmutableBootstrapIdentity _bootstrapIdentity =
            bootstrapIdentity ?? throw new ArgumentNullException(nameof(bootstrapIdentity));
        private int _launchLeaseClaimed;

        public string ManagedRoot { get; } = managedRoot;
        public ManagedVersionAdmission Admission { get; } = admission;

        public void Dispose()
        {
            markerStream.Dispose();
            custody.Dispose();
        }

        public async ValueTask<ManagedFirstInstallationTransactionIssue> RecordBootstrapLaunchAsync(
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(ManagedRoot) ||
                !string.Equals(marker.Phase, RootPromotedPhase, StringComparison.Ordinal))
            {
                return ManagedFirstInstallationTransactionIssue.RecoveryRequired;
            }
            try
            {
                if (!await revalidateRoot(cancellationToken).ConfigureAwait(false))
                {
                    return ManagedFirstInstallationTransactionIssue.RecoveryRequired;
                }
                if (!custody.RevalidateClosedTree())
                {
                    return ManagedFirstInstallationTransactionIssue.RecoveryRequired;
                }
                ManagedSetupTransactionDocument next = marker with
                {
                    Phase = BootstrapLaunchRecordedPhase,
                };
                await ReplaceExactMarkerAsync(
                        markerStream,
                        marker,
                        next,
                        custody.RevalidateClosedTree,
                        afterMarkerTopologyProof,
                        cancellationToken)
                    .ConfigureAwait(false);
                marker = next;
                return ManagedFirstInstallationTransactionIssue.None;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                return ManagedFirstInstallationTransactionIssue.RecoveryRequired;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return ManagedFirstInstallationTransactionIssue.RecoveryRequired;
            }
        }

        public async ValueTask<ManagedExecutableLaunchLeaseResult> AcquireBootstrapLaunchLeaseAsync(
            ManagedImmutableBootstrapIdentity expectedIdentity,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(expectedIdentity);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(marker.Phase, BootstrapLaunchRecordedPhase, StringComparison.Ordinal) ||
                Interlocked.Exchange(ref _launchLeaseClaimed, 1) != 0)
            {
                return new(null, ManagedExecutableLaunchIssue.Unavailable);
            }
            if (expectedIdentity != _bootstrapIdentity)
            {
                return new(null, ManagedExecutableLaunchIssue.UnsafePath);
            }

            WindowsStableCustodyResult cloned = custody.TryCloneClosedTree(cancellationToken);
            if (!cloned.IsAcquired)
            {
                cloned.Custody?.Dispose();
                return new(null, MapLaunchCustodyIssue(cloned.Issue));
            }
            return await StableManagedExecutableLaunchLease.TryCreateFromVerifiedTreeAsync(
                    cloned.Custody!,
                    expectedIdentity.FileName,
                    expectedIdentity.Length,
                    expectedIdentity.Sha256,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask<ManagedFirstInstallationTransactionIssue> CompleteAsync(
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(ManagedRoot) ||
                !string.Equals(marker.Phase, BootstrapLaunchRecordedPhase, StringComparison.Ordinal))
            {
                return ManagedFirstInstallationTransactionIssue.RecoveryRequired;
            }
            try
            {
                ExactMarkerDeleteResult deleted = await DeleteExactMarkerAsync(
                    markerStream,
                    marker,
                    revalidateRoot,
                    custody.RevalidateClosedTree,
                    afterMarkerTopologyProof,
                    cancellationToken).ConfigureAwait(false);
                return deleted switch
                {
                    ExactMarkerDeleteResult.Deleted => ManagedFirstInstallationTransactionIssue.None,
                    ExactMarkerDeleteResult.Mismatch =>
                        ManagedFirstInstallationTransactionIssue.RecoveryRequired,
                    ExactMarkerDeleteResult.Unavailable =>
                        ManagedFirstInstallationTransactionIssue.RecoveryRequired,
                    _ => throw new InvalidOperationException(
                        "Exact marker deletion returned an undefined result."),
                };
            }
            catch (UnauthorizedAccessException)
            {
                return ManagedFirstInstallationTransactionIssue.RecoveryRequired;
            }
            catch (IOException)
            {
                return ManagedFirstInstallationTransactionIssue.RecoveryRequired;
            }
        }

        private static ManagedExecutableLaunchIssue MapLaunchCustodyIssue(
            WindowsStableCustodyIssue issue)
        {
            return issue switch
            {
                WindowsStableCustodyIssue.InvalidPath or
                WindowsStableCustodyIssue.ReparsePoint or
                WindowsStableCustodyIssue.Changed => ManagedExecutableLaunchIssue.UnsafePath,
                WindowsStableCustodyIssue.AccessDenied or
                WindowsStableCustodyIssue.Contended or
                WindowsStableCustodyIssue.Unavailable => ManagedExecutableLaunchIssue.Unavailable,
                WindowsStableCustodyIssue.None => throw new InvalidOperationException(
                    "Successful cloned custody did not return its owner."),
                _ => throw new InvalidOperationException(
                    "Cloned Bootstrap custody returned an undefined issue."),
            };
        }
    }

    private enum ExactMarkerDeleteResult
    {
        Deleted,
        Mismatch,
        Unavailable,
    }

}
