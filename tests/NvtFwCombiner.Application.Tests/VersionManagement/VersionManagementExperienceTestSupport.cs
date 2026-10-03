using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Provides shared helpers for version management experience tests.</summary>
internal static partial class VersionManagementExperienceTestSupport
{
    internal const string Hash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    internal static VersionManagerState State(
        IReadOnlyList<ManagedVersionAdmission> admissions,
        string active,
        string lastKnownGood,
        string source = "source-root",
        VersionSourceRegistryState? sourceRegistryState = null)
    {
        return VersionManagerState.Create(
            source,
            ManagedAppVersion.Parse(active),
            ManagedAppVersion.Parse(lastKnownGood),
            admissions,
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-root",
            sourceRegistryState: sourceRegistryState);
    }

    internal static ManagedVersionAdmission Admission(string version)
    {
        return new(ManagedAppVersion.Parse(version), $"identity-{version}", Hash);
    }

    internal static UpdateCatalogSnapshot Catalog(string version)
    {
        var document = new UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [new(
                version,
                "2026-08-21T00:00:00Z",
                $"packages/NvtFwCombiner-v{version}-win-x64.zip",
                42,
                Hash,
                Hash,
                $"Release {version}")]);
        return Assert.IsType<UpdateCatalogSnapshot>(UpdateCatalogValidator.Validate(document).Snapshot);
    }

    internal static UpdateCatalogSnapshot CatalogV2(params (string Version, string Policy)[] versions)
    {
        var document = new UpdateCatalogV2Document(
            2,
            "NVT FW Combiner",
            "win-x64",
            [.. versions.Select(entry => new UpdateCatalogV2VersionDocument(
                entry.Version,
                "2026-09-01T00:00:00Z",
                $"packages/NvtFwCombiner-v{entry.Version}-win-x64.zip",
                42,
                Hash,
                Hash,
                $"Release {entry.Version}",
                entry.Policy))]);
        return Assert.IsType<UpdateCatalogSnapshot>(UpdateCatalogValidator.Validate(document).Snapshot);
    }

    internal sealed class FixedCatalogSource(UpdateCatalogSnapshot snapshot) : IRootCatalogSourceTestDouble
    {
        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new UpdateCatalogLoadResult(
                snapshot,
                UpdateCatalogLoadIssue.None,
                new(1, CatalogContentDigest)));
        }
    }

    internal sealed class MutableCatalogSource(UpdateCatalogSnapshot snapshot) : IRootCatalogSourceTestDouble
    {
        internal UpdateCatalogSnapshot Snapshot { get; set; } = snapshot;

        internal int LoadCount { get; private set; }

        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            return ValueTask.FromResult(new UpdateCatalogLoadResult(Snapshot, UpdateCatalogLoadIssue.None));
        }
    }

    internal sealed class MemoryStateStore(VersionManagerState state) : IVersionManagerStateStore
    {
        internal int SaveCount { get; private set; }
        internal int WriteLeaseCount { get; private set; }

        internal VersionManagerState State { get; private set; } = state;

        internal void ReplaceState(VersionManagerState replacement)
        {
            State = replacement;
        }

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            WriteLeaseCount++;
            return ValueTask.FromResult(VersionManagerWriteLeaseTestSupport.Acquired());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(State, VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(VersionManagerState stateToSave, CancellationToken cancellationToken)
        {
            State = stateToSave;
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class HealthyRepository(
        bool verifyPackages = true,
        ManagedVersionDeleteIssue deleteIssue = ManagedVersionDeleteIssue.None,
        ManagedPackageVerificationResult? verificationResult = null) : IManagedVersionRepository
    {
        internal List<ManagedAppVersion> Deleted { get; } = [];

        internal int VerifyCount { get; private set; }

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            VerifyCount++;
            return ValueTask.FromResult(verificationResult ?? (verifyPackages
                ? new ManagedPackageVerificationResult(
                    new(package.Version, package.Identity, package.ReleaseNotes),
                    ManagedVersionInstallIssue.None)
                {
                    HasSupportedManagedLauncher = true,
                }
                : new ManagedPackageVerificationResult(
                    Candidate: null,
                    ManagedVersionInstallIssue.PackageMismatch)));
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new ManagedVersionInstallResult(
                new(package.Version, package.Identity, package.ReleaseManifestSha256),
                ManagedVersionInstallIssue.None,
                WasAlreadyInstalled: false));
        }

        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ManagedVersionInventoryReadResult.Success(
                ManagedVersionInventory.Create(admissions.Select(admission =>
                    new InstalledVersionSnapshot(
                        admission.Version,
                        admission.AdmissionIdentity,
                        failedActivationVersion == admission.Version
                            ? ManagedVersionIntegrity.Damaged
                            : ManagedVersionIntegrity.Healthy,
                        failedActivationVersion == admission.Version
                            ? ManagedVersionDamageReason.FailedActivation
                            : null,
                        activeVersion == admission.Version,
                        lastKnownGoodVersion == admission.Version)))));
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            if (deleteIssue == ManagedVersionDeleteIssue.None)
            {
                Deleted.Add(admission.Version);
            }
            return ValueTask.FromResult(deleteIssue);
        }
    }

    internal const string FirstRegistryDigest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    internal const string SecondRegistryDigest =
        "2222222222222222222222222222222222222222222222222222222222222222";

    internal static LauncherMutationProtection PendingProtection()
    {
        return new(
            LauncherMutationFenceIssue.None,
            HasPendingActivation: true,
            ActiveOwner: Admission("0.10.5"),
            LastKnownGoodOwner: Admission("0.10.5"),
            PendingOwners: [Admission("0.10.6")]);
    }

    internal sealed class RecordingLauncherFence(LauncherMutationProtection protection)
        : ILauncherMutationFence
    {
        public int LoadCount { get; private set; }
        public int RetireCount => Retired.Count;
        public List<ManagedVersionAdmission> Retired { get; } = [];

        public ValueTask<LauncherMutationProtection> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            return ValueTask.FromResult(protection);
        }

        public ValueTask<LauncherMutationFenceIssue> RetireLastKnownGoodOwnerAsync(
            ManagedVersionAdmission expectedOwner,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Retired.Add(expectedOwner);
            protection = protection with { LastKnownGoodOwner = protection.ActiveOwner };
            return ValueTask.FromResult(LauncherMutationFenceIssue.None);
        }
    }
}
