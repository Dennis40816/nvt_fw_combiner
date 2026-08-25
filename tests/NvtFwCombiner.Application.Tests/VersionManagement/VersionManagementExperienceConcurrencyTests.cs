using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    /// <summary>Experiences for the same managed root serialize through shared state and reload before committing.</summary>
    [Fact]
    public async Task SameManagedRootSharingStateSerializesInstallsAndReloadsBeforeSecondCommit()
    {
        VersionManagerState initial = VersionManagerState.Create(
            "source",
            activeVersion: null,
            lastKnownGoodVersion: null,
            admissions: [],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-a");
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
            "managed-a",
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

    /// <summary>A shared state file never lets another managed root observe or mutate its authority.</summary>
    [Theory]
    [InlineData("0.10.5")]
    [InlineData("0.10.7")]
    public async Task DifferentManagedRootSharingStateFailsClosedForSameOrDifferentRunningVersion(
        string runningVersion)
    {
        ManagedVersionAdmission active = Admission("0.10.5");
        VersionManagerState initial = VersionManagerState.Create(
            "source",
            active.Version,
            active.Version,
            [active],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-a");
        var stateStore = new SharedLeaseStateStore(initial);
        var repository = new FirstInstallBlockingRepository();
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse(runningVersion),
            "managed-b",
            stateStore,
            new FixedCatalogSource(Catalog("0.10.6")),
            repository);

        VersionManagementSnapshot initialized = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);
        VersionInstallOperationResult install = await experience.InstallAsync(
            ManagedAppVersion.Parse("0.10.6"),
            TestContext.Current.CancellationToken);
        VersionDeleteOperationResult delete = await experience.DeleteAsync(
            active.Version,
            rollbackLossConfirmed: true,
            TestContext.Current.CancellationToken);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await experience.PrepareActivationAsync(
                active.Version,
                TestContext.Current.CancellationToken));

        Assert.Equal(VersionManagerStateLoadIssue.ManagedRootMismatch, initialized.StateIssue);
        Assert.Null(initialized.State);
        Assert.Equal(ManagedVersionInstallIssue.StateUnavailable, install.Install.Issue);
        Assert.Equal(VersionDeleteOperationIssue.StateUnavailable, delete.OperationIssue);
        Assert.Equal(0, repository.InventoryCount);
        Assert.Equal(0, repository.InstallCount);
        Assert.Equal(0, repository.DeleteCount);
        Assert.Equal(initial, stateStore.State);
    }

    /// <summary>An admission is not reported already-installed when its exact current-root payload is absent or damaged.</summary>
    [Theory]
    [InlineData(true, ManagedVersionIntegrity.Healthy)]
    [InlineData(false, ManagedVersionIntegrity.Damaged)]
    public async Task ExistingAdmissionRequiresExactHealthyPayloadBeforeAlreadyInstalled(
        bool omitPayload,
        ManagedVersionIntegrity integrity)
    {
        UpdateCatalogSnapshot catalog = Catalog("0.10.6");
        UpdateCatalogVersionSnapshot package = Assert.Single(catalog.Versions);
        var admission = new ManagedVersionAdmission(
            package.Version,
            package.Identity,
            package.ReleaseManifestSha256);
        VersionManagerState initial = VersionManagerState.Create(
            "source",
            activeVersion: null,
            lastKnownGoodVersion: null,
            [admission],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "managed-a");
        var repository = new FirstInstallBlockingRepository
        {
            OmitInventory = omitPayload,
            InventoryIntegrity = integrity,
        };
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-a",
            new SharedLeaseStateStore(initial),
            new FixedCatalogSource(catalog),
            repository);
        _ = await experience.InitializeAsync(TestContext.Current.CancellationToken);
        _ = await experience.CheckAsync(isAutomatic: false, TestContext.Current.CancellationToken);

        VersionInstallOperationResult result = await experience.InstallAsync(
            admission.Version,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.InvalidPayload, result.Install.Issue);
        Assert.False(result.Install.WasAlreadyInstalled);
        Assert.Equal(0, repository.InstallCount);
        Assert.Equal([admission], result.Snapshot.State!.Admissions);
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

        internal int DeleteCount { get; private set; }

        internal int InventoryCount { get; private set; }

        internal bool OmitInventory { get; init; }

        internal ManagedVersionIntegrity InventoryIntegrity { get; init; } = ManagedVersionIntegrity.Healthy;

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

        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            InventoryCount++;
            ManagedVersionInventory inventory = OmitInventory
                ? ManagedVersionInventory.Create([])
                : ManagedVersionInventory.Create(admissions.Select(admission => new
                InstalledVersionSnapshot(
                    admission.Version,
                    admission.AdmissionIdentity,
                    InventoryIntegrity,
                    DamageReason: InventoryIntegrity == ManagedVersionIntegrity.Healthy
                        ? null
                        : ManagedVersionDamageReason.ContentMismatch,
                    IsActive: admission.Version == activeVersion,
                    IsLastKnownGood: admission.Version == lastKnownGoodVersion,
                    ManagedVersionAdmissionState.Admitted,
                    admission)));
            return ValueTask.FromResult(ManagedVersionInventoryReadResult.Success(inventory));
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            DeleteCount++;
            return ValueTask.FromResult(ManagedVersionDeleteIssue.None);
        }
    }
}
