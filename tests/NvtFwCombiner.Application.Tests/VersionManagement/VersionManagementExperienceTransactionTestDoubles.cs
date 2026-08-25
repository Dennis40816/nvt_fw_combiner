using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    private sealed class FailingStateStore(
        VersionManagerState state,
        int failOnSave) : IVersionManagerStateStore
    {
        private int _saveCount;

        internal int SaveCount => _saveCount;

        internal VersionManagerState State { get; private set; } = state;

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(VersionManagerWriteLeaseTestSupport.Acquired());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(State, VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(VersionManagerState stateToSave, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _saveCount) == failOnSave)
            {
                throw new IOException("Injected state commit failure.");
            }
            State = stateToSave;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TransactionRepository : IManagedVersionRepository
    {
        private readonly Dictionary<ManagedAppVersion, ManagedVersionAdmission?> _installed;

        internal TransactionRepository(
            IEnumerable<ManagedVersionAdmission> installed,
            string? unadmittedVersion = null)
        {
            _installed = installed.ToDictionary(
                admission => admission.Version,
                admission => (ManagedVersionAdmission?)admission);
            if (unadmittedVersion is not null)
            {
                _installed.Add(ManagedAppVersion.Parse(unadmittedVersion), null);
            }
        }

        internal int DeleteCalls { get; private set; }

        internal ManagedVersionDeleteIssue DeleteIssue { get; set; }

        internal int InstallCalls { get; private set; }

        internal ManagedVersionInstallIssue InstallIssue { get; set; }

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new ManagedPackageVerificationResult(
                new(package.Version, package.Identity, package.ReleaseNotes),
                ManagedVersionInstallIssue.None));
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            InstallCalls++;
            if (InstallIssue != ManagedVersionInstallIssue.None)
            {
                return ValueTask.FromResult(new ManagedVersionInstallResult(
                    Admission: null,
                    InstallIssue,
                    WasAlreadyInstalled: false));
            }
            var admission = new ManagedVersionAdmission(
                package.Version,
                package.Identity,
                package.ReleaseManifestSha256);
            _installed[package.Version] = admission;
            return ValueTask.FromResult(new ManagedVersionInstallResult(
                admission,
                ManagedVersionInstallIssue.None,
                WasAlreadyInstalled: false));
        }

        public ValueTask<ManagedVersionInventory> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            var committed = admissions.ToDictionary(admission => admission.Version);
            return ValueTask.FromResult(ManagedVersionInventory.Create(_installed.Select(pair =>
            {
                bool isAdmitted = committed.TryGetValue(pair.Key, out ManagedVersionAdmission? admission);
                ManagedVersionAdmission? observed = pair.Value;
                if (!isAdmitted && observed is null)
                {
                    return new InstalledVersionSnapshot(
                        pair.Key,
                        $"unadmitted:{pair.Key}",
                        ManagedVersionIntegrity.Damaged,
                        ManagedVersionDamageReason.UnexpectedPath,
                        IsActive: false,
                        IsLastKnownGood: false,
                        ManagedVersionAdmissionState.Unadmitted);
                }
                ManagedVersionAdmission identity = admission ?? observed!;
                return new InstalledVersionSnapshot(
                    pair.Key,
                    identity.AdmissionIdentity,
                    ManagedVersionIntegrity.Healthy,
                    DamageReason: null,
                    activeVersion == pair.Key,
                    lastKnownGoodVersion == pair.Key,
                    isAdmitted
                        ? ManagedVersionAdmissionState.Admitted
                        : ManagedVersionAdmissionState.Unadmitted,
                    identity);
            })));
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            DeleteCalls++;
            if (DeleteIssue == ManagedVersionDeleteIssue.NotInstalled)
            {
                _ = _installed.Remove(admission.Version);
                return ValueTask.FromResult(ManagedVersionDeleteIssue.NotInstalled);
            }
            return ValueTask.FromResult(DeleteIssue != ManagedVersionDeleteIssue.None
                ? DeleteIssue
                : _installed.Remove(admission.Version)
                    ? ManagedVersionDeleteIssue.None
                    : ManagedVersionDeleteIssue.NotInstalled);
        }
    }
}
