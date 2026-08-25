using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    /// <summary>Experiences with different managed roots serialize through shared state and reload before committing.</summary>
    [Fact]
    public async Task TwoManagedRootsSharingStateSerializeInstallsAndReloadBeforeSecondCommit()
    {
        VersionManagerState initial = VersionManagerState.Create(
            "source",
            activeVersion: null,
            lastKnownGoodVersion: null,
            admissions: [],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false);
        var stateStore = new SharedLeaseStateStore(initial);
        var repository = new FirstInstallBlockingRepository();
        using var first = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-a",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);
        using var second = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-b",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.7")),
            repository);
        _ = await first.InitializeAsync(TestContext.Current.CancellationToken);
        _ = await second.InitializeAsync(TestContext.Current.CancellationToken);
        _ = await first.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);
        _ = await second.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);

        Task<VersionInstallOperationResult> firstInstall = first.InstallAsync(
            ManagedAppVersion.Parse("0.10.6"),
            TestContext.Current.CancellationToken).AsTask();
        await repository.FirstInstallStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        VersionInstallOperationResult contended = await second.InstallAsync(
            ManagedAppVersion.Parse("0.10.7"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.StateUnavailable, contended.Install.Issue);
        Assert.Equal(1, repository.InstallCount);
        _ = repository.ReleaseFirstInstall.TrySetResult();
        Assert.True((await firstInstall).Install.IsSuccess);

        VersionInstallOperationResult retried = await second.InstallAsync(
            ManagedAppVersion.Parse("0.10.7"),
            TestContext.Current.CancellationToken);

        Assert.True(retried.Install.IsSuccess);
        Assert.Equal(2, repository.InstallCount);
        Assert.Equal(
            [ManagedAppVersion.Parse("0.10.7"), ManagedAppVersion.Parse("0.10.6")],
            stateStore.State.Admissions.Select(admission => admission.Version));
        Assert.Null(stateStore.State.PendingMutation);
    }

    private sealed class SharedLeaseStateStore(VersionManagerState state) : IVersionManagerStateStore
    {
        private int _writerOwned;
        private readonly Lock _stateSync = new();
        private VersionManagerState _state = state;

        internal VersionManagerState State
        {
            get
            {
                lock (_stateSync)
                {
                    return _state;
                }
            }
        }

        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.CompareExchange(ref _writerOwned, 1, 0) != 0)
            {
                return ValueTask.FromResult(
                    VersionManagerWriteLeaseTestSupport.Busy());
            }
#pragma warning disable CA2000 // Ownership transfers to VersionManagerWriteLeaseResult.
            var result = new VersionManagerWriteLeaseResult(
                VersionManagerWriteLeaseIssue.None,
                new ReleaseHandle(this));
#pragma warning restore CA2000
            return ValueTask.FromResult(result);
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            lock (_stateSync)
            {
                return ValueTask.FromResult(
                    new VersionManagerStateLoadResult(_state, VersionManagerStateLoadIssue.None));
            }
        }

        public ValueTask SaveAsync(
            VersionManagerState stateToSave,
            CancellationToken cancellationToken)
        {
            lock (_stateSync)
            {
                _state = stateToSave;
            }
            return ValueTask.CompletedTask;
        }

        private sealed class ReleaseHandle(SharedLeaseStateStore owner) : IDisposable
        {
            public void Dispose()
            {
                _ = Interlocked.Exchange(ref owner._writerOwned, 0);
            }
        }
    }

    private sealed class FirstInstallBlockingRepository : IManagedVersionRepository
    {
        private int _installCount;

        internal TaskCompletionSource FirstInstallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseFirstInstall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int InstallCount => _installCount;

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new ManagedPackageVerificationResult(
                new(package.Version, package.Identity, package.ReleaseNotes),
                ManagedVersionInstallIssue.None));
        }

        public async ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _installCount) == 1)
            {
                _ = FirstInstallStarted.TrySetResult();
                await ReleaseFirstInstall.Task.WaitAsync(cancellationToken);
            }
            return new(
                new(package.Version, package.Identity, package.ReleaseManifestSha256),
                ManagedVersionInstallIssue.None,
                WasAlreadyInstalled: false);
        }

        public ValueTask<ManagedVersionInventory> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ManagedVersionInventory.Create(admissions.Select(admission => new
                InstalledVersionSnapshot(
                    admission.Version,
                    admission.AdmissionIdentity,
                    ManagedVersionIntegrity.Healthy,
                    DamageReason: null,
                    IsActive: admission.Version == activeVersion,
                    IsLastKnownGood: admission.Version == lastKnownGoodVersion,
                    ManagedVersionAdmissionState.Admitted,
                    admission))));
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ManagedVersionDeleteIssue.None);
        }
    }
}
