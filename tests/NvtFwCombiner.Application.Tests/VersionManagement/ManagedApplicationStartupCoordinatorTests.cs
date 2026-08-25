using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Guards managed-child startup qualification and state reload behavior.</summary>
public sealed class ManagedApplicationStartupCoordinatorTests
{
    /// <summary>Only an accepted exact inherited READY write selects the bounded startup lease wait.</summary>
    [Theory]
    [InlineData(ApplicationReadySignalOutcome.NotInherited)]
    [InlineData(ApplicationReadySignalOutcome.InvalidInheritedContext)]
    [InlineData(ApplicationReadySignalOutcome.WriteFailed)]
    [InlineData(ApplicationReadySignalOutcome.Reported)]
    public async Task OnlyReportedReadyUsesBoundedManagedStartupInitialization(
        ApplicationReadySignalOutcome outcome)
    {
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        var signal = new FixedReadySignal(outcome);
        var experience = new RecordingExperience(Snapshot("durable"));
        var coordinator = new ManagedApplicationStartupCoordinator(version, signal, experience);

        ManagedApplicationStartupResult result = await coordinator.CompleteStartupAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(version, signal.ReportedVersion);
        Assert.Equal(outcome, result.ReadySignalOutcome);
        Assert.Equal("durable", result.Snapshot.State!.UpdateSource);
        if (outcome == ApplicationReadySignalOutcome.Reported)
        {
            Assert.Equal(0, experience.ImmediateInitializations);
            Assert.True(experience.ManagedReadyInitialization);
        }
        else
        {
            Assert.Equal(1, experience.ImmediateInitializations);
            Assert.False(experience.ManagedReadyInitialization);
        }
    }

    /// <summary>The managed READY path waits on the existing lease owner and reloads state committed before release.</summary>
    [Fact]
    public async Task ManagedReadyInitializationWaitsThenReloadsDurableState()
    {
        var store = new DelayedLeaseStateStore(State("before-ready"));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.6"),
            "managed",
            store,
            new EmptyCatalogSource(),
            new EmptyRepository());
        VersionManagementSnapshot initial = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal("before-ready", initial.State!.UpdateSource);
        IDisposable launcherLease = store.HoldExternalWriter();
        store.ReplaceDurableState(State("launcher-committed"));

        Task<VersionManagementSnapshot> waiting = experience.InitializeAfterManagedReadyAsync(
            TestContext.Current.CancellationToken).AsTask();
        await store.WaitObserved.WaitAsync(TestContext.Current.CancellationToken);

        Assert.False(waiting.IsCompleted);
        Assert.Equal(ManagedActivationCoordinator.DefaultWriterLeaseTimeout, store.LastWaitTimeout);
        launcherLease.Dispose();
        VersionManagementSnapshot reloaded = await waiting;

        Assert.Equal("launcher-committed", reloaded.State!.UpdateSource);
    }

    /// <summary>Ordinary initialization never waits behind a managed writer.</summary>
    [Fact]
    public async Task OrdinaryInitializationKeepsZeroWaitContentionBehavior()
    {
        var store = new DelayedLeaseStateStore(State("durable"));
        using var experience = new VersionManagementExperience(
            ManagedAppVersion.Parse("0.10.6"),
            "managed",
            store,
            new EmptyCatalogSource(),
            new EmptyRepository());
        using IDisposable launcherLease = store.HoldExternalWriter();

        VersionManagementSnapshot result = await experience.InitializeAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.Zero, store.LastWaitTimeout);
        Assert.Equal(VersionManagerStateLoadIssue.Unavailable, result.StateIssue);
    }

    private static VersionManagementSnapshot Snapshot(string updateSource)
    {
        return new(
            State(updateSource),
            ManagedVersionInventory.Create([]),
            Catalog: null,
            VerifiedCandidate: null,
            VersionSourceStatus.Offline,
            CatalogIssue: null,
            Generation: 0,
            ShouldPromptForUpdate: false,
            VersionManagerStateLoadIssue.None);
    }

    private static VersionManagerState State(string updateSource)
    {
        return VersionManagerState.Create(
            updateSource,
            activeVersion: null,
            lastKnownGoodVersion: null,
            admissions: [],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false);
    }

    private sealed class FixedReadySignal(ApplicationReadySignalOutcome outcome)
        : IApplicationReadySignal
    {
        internal ManagedAppVersion? ReportedVersion { get; private set; }

        public ValueTask<ApplicationReadySignalOutcome> ReportReadyAsync(
            ManagedAppVersion version,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReportedVersion = version;
            return ValueTask.FromResult(outcome);
        }
    }

    private sealed class RecordingExperience(VersionManagementSnapshot snapshot)
        : IVersionManagementExperience
    {
        internal int ImmediateInitializations { get; private set; }

        internal bool ManagedReadyInitialization { get; private set; }

        public ValueTask<VersionManagementSnapshot> InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImmediateInitializations++;
            return ValueTask.FromResult(snapshot);
        }

        public ValueTask<VersionManagementSnapshot> InitializeAfterManagedReadyAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ManagedReadyInitialization = true;
            return ValueTask.FromResult(snapshot);
        }

        public ValueTask<VersionManagementSnapshot> CheckAsync(
            bool isAutomatic,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionManagementSnapshot> CommitUpdateSourceAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionInstallOperationResult> InstallAsync(
            ManagedAppVersion version,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionDeleteOperationResult> DeleteAsync(
            ManagedAppVersion version,
            bool rollbackLossConfirmed,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionManagementSnapshot> AcknowledgeRetentionReviewAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionManagerState> PrepareActivationAsync(
            ManagedAppVersion version,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<VersionManagementSnapshot> CancelPendingActivationAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class DelayedLeaseStateStore(VersionManagerState state) : IVersionManagerStateStore
    {
        private int _writerOwned;
        private VersionManagerState _state = state;

        internal TimeSpan? LastWaitTimeout { get; private set; }

        internal Task WaitObserved { get; private set; } = Task.CompletedTask;

        internal IDisposable HoldExternalWriter()
        {
            Assert.Equal(0, Interlocked.Exchange(ref _writerOwned, 1));
            var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            WaitObserved = observed.Task;
            _waitObserved = observed;
            return new ReleaseExternalWriter(this);
        }

        private TaskCompletionSource? _waitObserved;

        internal void ReplaceDurableState(VersionManagerState replacement)
        {
            _state = replacement;
        }

        public async ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            LastWaitTimeout = waitTimeout;
            if (Interlocked.CompareExchange(ref _writerOwned, 1, 0) == 0)
            {
                return Acquired();
            }
            _ = _waitObserved?.TrySetResult();
            if (waitTimeout == TimeSpan.Zero)
            {
                return VersionManagerWriteLeaseTestSupport.Busy();
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(waitTimeout);
            try
            {
                while (Interlocked.CompareExchange(ref _writerOwned, 1, 0) != 0)
                {
                    await Task.Delay(5, deadline.Token);
                }
                return Acquired();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return VersionManagerWriteLeaseTestSupport.Busy();
            }
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new VersionManagerStateLoadResult(
                _state,
                VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(VersionManagerState stateToSave, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state = stateToSave;
            return ValueTask.CompletedTask;
        }

        private VersionManagerWriteLeaseResult Acquired()
        {
#pragma warning disable CA2000 // Ownership transfers to VersionManagerWriteLeaseResult.
            return new VersionManagerWriteLeaseResult(
                VersionManagerWriteLeaseIssue.None,
                new ReleaseOwnedWriter(this));
#pragma warning restore CA2000
        }

        private sealed class ReleaseExternalWriter(DelayedLeaseStateStore owner) : IDisposable
        {
            public void Dispose()
            {
                _ = Interlocked.Exchange(ref owner._writerOwned, 0);
            }
        }

        private sealed class ReleaseOwnedWriter(DelayedLeaseStateStore owner) : IDisposable
        {
            public void Dispose()
            {
                _ = Interlocked.Exchange(ref owner._writerOwned, 0);
            }
        }
    }

    private sealed class EmptyCatalogSource : IUpdateCatalogSource
    {
        public ValueTask<UpdateCatalogLoadResult> LoadAsync(
            string sourceRoot,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class EmptyRepository : IManagedVersionRepository
    {
        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ManagedVersionInventory> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ManagedVersionInventory.Create([]));
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
