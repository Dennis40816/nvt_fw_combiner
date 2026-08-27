using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    private const string CatalogContentDigest =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    private static UpdateSourceRegistryLoadResult Registry(
        long revision,
        string digest,
        params (string Path, UpdateSourceRegistryEntryStatus Status)[] entries)
    {
        return RegistryWithPublication(
            "0.10.6",
            1,
            CatalogContentDigest,
            revision,
            digest,
            entries);
    }

    private static UpdateSourceRegistryLoadResult RegistryWithPublication(
        string latestVersion,
        int catalogSchemaVersion,
        string catalogDigest,
        long revision,
        string digest,
        params (string Path, UpdateSourceRegistryEntryStatus Status)[] entries)
    {
        return new(
            new UpdateSourceRegistrySnapshot(
                "nvt-fw-combiner-production",
                revision,
                new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
                new UpdateCatalogPublicationAssertion(
                    latestVersion,
                    catalogSchemaVersion,
                    catalogDigest),
                digest,
                [.. entries.Select(entry => new UpdateSourceRegistryEntry(
                    Path.Combine(entry.Path, "update-catalog.v1.json"),
                    entry.Status))]),
            UpdateSourceRegistryLoadIssue.None);
    }

    private static string SourcePath(string name)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine("registry-sources", name)));
    }

    private static UpdateCatalogSnapshot CatalogWithOlderReleaseNote(string olderNote)
    {
        var document = new NvtFwCombiner.Contracts.VersionManagement.UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [
                new(
                    "0.10.6",
                    "2026-08-21T00:00:00Z",
                    "packages/NvtFwCombiner-v0.10.6-win-x64.zip",
                    42,
                    Hash,
                    Hash,
                    "Release 0.10.6"),
                new(
                    "0.10.5",
                    "2026-08-20T00:00:00Z",
                    "packages/NvtFwCombiner-v0.10.5-win-x64.zip",
                    41,
                    Hash,
                    Hash,
                    olderNote),
            ]);
        return Assert.IsType<UpdateCatalogSnapshot>(UpdateCatalogValidator.Validate(document).Snapshot);
    }

    private static UpdateCatalogSnapshot CatalogWithVersionCount(int count)
    {
        var document = new NvtFwCombiner.Contracts.VersionManagement.UpdateCatalogDocument(
            1,
            "NVT FW Combiner",
            "win-x64",
            [.. Enumerable.Range(1, count)
                .Select(index => new NvtFwCombiner.Contracts.VersionManagement.UpdateCatalogVersionDocument(
                    $"0.10.{index}",
                    "2026-08-21T00:00:00Z",
                    $"packages/NvtFwCombiner-v0.10.{index}-win-x64.zip",
                    42,
                    Hash,
                    Hash,
                    $"Release 0.10.{index}"))]);
        return Assert.IsType<UpdateCatalogSnapshot>(UpdateCatalogValidator.Validate(document).Snapshot);
    }

    private sealed class SequenceRegistrySource(params UpdateSourceRegistryLoadResult[] results)
        : IUpdateSourceRegistry
    {
        private int _next;

        internal int LoadCount => _next;

        public ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            int index = Math.Min(Interlocked.Increment(ref _next) - 1, results.Length - 1);
            return ValueTask.FromResult(results[index]);
        }
    }

    private sealed class PathCatalogSource(
        params (string Path, UpdateCatalogLoadResult Result)[] results) : IRootCatalogSourceTestDouble
    {
        private readonly Dictionary<string, UpdateCatalogLoadResult> _results =
            results.ToDictionary(entry => entry.Path, entry => entry.Result, StringComparer.Ordinal);

        internal List<string> LoadedRoots { get; } = [];

        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            LoadedRoots.Add(sourceRoot);
            return ValueTask.FromResult(_results.TryGetValue(sourceRoot, out UpdateCatalogLoadResult? result)
                ? WithDefaultContentIdentity(result)
                : new UpdateCatalogLoadResult(null, UpdateCatalogLoadIssue.SourceMissing));
        }
    }

    private sealed class SequencePathCatalogSource(
        string path,
        params UpdateCatalogSnapshot[] snapshots) : IRootCatalogSourceTestDouble
    {
        private int _next;

        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            Assert.Equal(path, sourceRoot);
            int index = Math.Min(Interlocked.Increment(ref _next) - 1, snapshots.Length - 1);
            return ValueTask.FromResult(new UpdateCatalogLoadResult(
                snapshots[index],
                UpdateCatalogLoadIssue.None,
                DefaultCatalogContentIdentity));
        }
    }

    private sealed class CancelFirstCatalogSource(UpdateCatalogSnapshot snapshot)
        : IRootCatalogSourceTestDouble
    {
        private int _loadCount;

        internal TaskCompletionSource FirstLoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _loadCount) == 1)
            {
                _ = FirstLoadStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return new(snapshot, UpdateCatalogLoadIssue.None, DefaultCatalogContentIdentity);
        }
    }

    private static UpdateCatalogContentIdentity DefaultCatalogContentIdentity =>
        new(1, CatalogContentDigest);

    private static UpdateCatalogLoadResult WithDefaultContentIdentity(UpdateCatalogLoadResult result)
    {
        return result.IsSuccess && result.ContentIdentity is null
            ? result with { ContentIdentity = DefaultCatalogContentIdentity }
            : result;
    }

    private sealed class CountingRepository(ManagedAppVersion? mismatchedVersion = null)
        : IManagedVersionRepository
    {
        private readonly HealthyRepository _inner = new();

        internal List<string> VerifiedRoots { get; } = [];

        internal List<ManagedAppVersion> VerifiedVersions { get; } = [];

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            VerifiedRoots.Add(sourceRoot);
            VerifiedVersions.Add(package.Version);
            return package.Version == mismatchedVersion
                ? ValueTask.FromResult(new ManagedPackageVerificationResult(
                    Candidate: null,
                    ManagedVersionInstallIssue.PackageMismatch))
                : _inner.VerifyPackageAsync(sourceRoot, package, cancellationToken);
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            return _inner.InstallAsync(managedRoot, sourceRoot, package, cancellationToken);
        }

        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            return _inner.InventoryAsync(
                managedRoot,
                admissions,
                activeVersion,
                lastKnownGoodVersion,
                failedActivationVersion,
                cancellationToken);
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            return _inner.DeleteAsync(managedRoot, admission, activeVersion, cancellationToken);
        }
    }

    private sealed class BlockingFirstVerificationRepository : IManagedVersionRepository
    {
        private readonly HealthyRepository _inner = new();
        private int _verifyCount;

        internal TaskCompletionSource FirstVerificationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseFirstVerification { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _verifyCount) == 1)
            {
                _ = FirstVerificationStarted.TrySetResult();
                await ReleaseFirstVerification.Task.WaitAsync(cancellationToken);
            }
            return await _inner.VerifyPackageAsync(sourceRoot, package, cancellationToken);
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            return _inner.InstallAsync(managedRoot, sourceRoot, package, cancellationToken);
        }

        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            return _inner.InventoryAsync(
                managedRoot,
                admissions,
                activeVersion,
                lastKnownGoodVersion,
                failedActivationVersion,
                cancellationToken);
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            return _inner.DeleteAsync(
                managedRoot,
                admission,
                activeVersion,
                cancellationToken);
        }
    }

    private sealed class LeaseCountingStateStore(VersionManagerState state) : IVersionManagerStateStore
    {
        internal int LeaseRequestCount { get; private set; }

        internal int SaveCount { get; private set; }

        internal VersionManagerState State { get; private set; } = state;

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            LeaseRequestCount++;
            return ValueTask.FromResult(VersionManagerWriteLeaseTestSupport.Acquired());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(
                State,
                VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(
            VersionManagerState stateToSave,
            CancellationToken cancellationToken)
        {
            State = stateToSave;
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SequenceLoadStateStore(
        params VersionManagerStateLoadResult[] results) : IVersionManagerStateStore
    {
        private int _loadCount;

        internal int LoadCount => Volatile.Read(ref _loadCount);

        internal int LeaseRequestCount { get; private set; }

        internal int SaveCount { get; private set; }

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            LeaseRequestCount++;
            return ValueTask.FromResult(VersionManagerWriteLeaseTestSupport.Acquired());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = Math.Min(Interlocked.Increment(ref _loadCount) - 1, results.Length - 1);
            return ValueTask.FromResult(results[index]);
        }

        public ValueTask SaveAsync(
            VersionManagerState state,
            CancellationToken cancellationToken)
        {
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SequencedLeaseStateStore(VersionManagerState state)
        : IVersionManagerStateStore
    {
        private int _leaseCount;

        internal int SaveCount { get; private set; }

        internal VersionManagerState State { get; private set; } = state;

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Interlocked.Increment(ref _leaseCount) == 1
                ? VersionManagerWriteLeaseTestSupport.Acquired()
                : VersionManagerWriteLeaseTestSupport.Busy());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(
                State,
                VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(
            VersionManagerState stateToSave,
            CancellationToken cancellationToken)
        {
            State = stateToSave;
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DenyingMutationFence : ILauncherMutationFence
    {
        public ValueTask<LauncherMutationProtection> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new LauncherMutationProtection(
                LauncherMutationFenceIssue.None,
                HasPendingActivation: true,
                ActiveOwner: null,
                LastKnownGoodOwner: null,
                PendingOwners: []));
        }

        public ValueTask<LauncherMutationFenceIssue> RetireLastKnownGoodOwnerAsync(
            ManagedVersionAdmission expectedOwner,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(expectedOwner);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(LauncherMutationFenceIssue.Invalid);
        }
    }

    private sealed class ReloadChangedStateStore : IVersionManagerStateStore
    {
        private readonly VersionManagerState _initial;
        private readonly VersionManagerState _changed;
        private int _loadCount;

        internal ReloadChangedStateStore(
            VersionManagerState initial,
            VersionManagerState changed)
        {
            _initial = initial;
            _changed = changed;
            State = initial;
        }

        internal int SaveCount { get; private set; }

        internal VersionManagerState State { get; private set; }

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(VersionManagerWriteLeaseTestSupport.Acquired());
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            State = Interlocked.Increment(ref _loadCount) == 1 ? _initial : _changed;
            return ValueTask.FromResult(new VersionManagerStateLoadResult(
                State,
                VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(
            VersionManagerState stateToSave,
            CancellationToken cancellationToken)
        {
            State = stateToSave;
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }
}
