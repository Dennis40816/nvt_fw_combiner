using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Tests launcher ready commit and bounded fallback orchestration.</summary>
public sealed class ManagedActivationCoordinatorTests
{
    /// <summary>Pending ready commits active and last-known-good.</summary>
    [Fact]
    public async Task ReadyCommitsCandidate()
    {
        VersionManagerState pending = VersionActivationPolicy.BeginActivation(State(), ManagedAppVersion.Parse("0.10.6"));
        var store = new FakeStateStore(pending);
        var process = new FakeProcess(ManagedProcessStartOutcome.Ready);
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            store,
            new HealthyRepository(),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.Ready, result.Outcome);
        Assert.Equal("0.10.6", store.State.ActiveVersion?.ToString());
        Assert.Equal("0.10.6", store.State.LastKnownGoodVersion?.ToString());
        Assert.Equal(["0.10.6"], process.Starts);
    }

    /// <summary>Candidate failure starts the prior last-known-good once and clears pending before fallback.</summary>
    [Fact]
    public async Task FailureRollsBackExactlyOnce()
    {
        VersionManagerState pending = VersionActivationPolicy.BeginActivation(State(), ManagedAppVersion.Parse("0.10.6"));
        var store = new FakeStateStore(pending);
        var process = new FakeProcess(
            ManagedProcessStartOutcome.ReadyTimeout,
            ManagedProcessStartOutcome.Ready);
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            store,
            new HealthyRepository(),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.RolledBack, result.Outcome);
        Assert.Equal(["0.10.6", "0.10.5"], process.Starts);
        Assert.Null(store.State.PendingActivation);
        Assert.Equal("0.10.6", store.State.FailedActivationVersion?.ToString());
    }

    /// <summary>Missing or unreadable launcher state never guesses an active version.</summary>
    [Theory]
    [InlineData(VersionManagerStateLoadIssue.Missing)]
    [InlineData(VersionManagerStateLoadIssue.Invalid)]
    [InlineData(VersionManagerStateLoadIssue.Unavailable)]
    public async Task InvalidStateReturnsTypedOutcome(VersionManagerStateLoadIssue issue)
    {
        var process = new FakeProcess();
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            new LoadIssueStateStore(issue),
            new HealthyRepository(),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.InvalidState, result.Outcome);
        Assert.Empty(process.Starts);
    }

    /// <summary>A valid empty state reports that no active version is selectable.</summary>
    [Fact]
    public async Task EmptyStateReportsNoActiveVersion()
    {
        VersionManagerState state = VersionManagerState.Create(
            null, null, null, [], null, null, false);
        var process = new FakeProcess();
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            new FakeStateStore(state),
            new HealthyRepository(),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.NoActiveVersion, result.Outcome);
        Assert.Empty(process.Starts);
    }

    /// <summary>A damaged committed active version cannot launch and has no implicit rollback.</summary>
    [Fact]
    public async Task DamagedCommittedVersionDoesNotLaunch()
    {
        var process = new FakeProcess();
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            new FakeStateStore(State()),
            new HealthyRepository("0.10.5"),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.DamagedVersion, result.Outcome);
        Assert.Equal("0.10.5", result.FailedVersion?.ToString());
        Assert.Empty(process.Starts);
    }

    /// <summary>A failed committed process returns StartFailed without launching last-known-good again.</summary>
    [Fact]
    public async Task CommittedVersionStartFailureDoesNotAutoRollback()
    {
        var process = new FakeProcess(ManagedProcessStartOutcome.ExitedBeforeReady);
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            new FakeStateStore(State()),
            new HealthyRepository(),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.StartFailed, result.Outcome);
        Assert.Equal(["0.10.5"], process.Starts);
    }

    /// <summary>A damaged pending candidate skips candidate start and launches healthy rollback once.</summary>
    [Fact]
    public async Task DamagedPendingCandidateRollsBackBeforeProcessStart()
    {
        VersionManagerState pending = VersionActivationPolicy.BeginActivation(
            State(),
            ManagedAppVersion.Parse("0.10.6"));
        var process = new FakeProcess(ManagedProcessStartOutcome.Ready);
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            new FakeStateStore(pending),
            new HealthyRepository("0.10.6"),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.RolledBack, result.Outcome);
        Assert.Equal(["0.10.5"], process.Starts);
    }

    /// <summary>A fallback process that also misses readiness ends after exactly two starts.</summary>
    [Fact]
    public async Task FailedFallbackStopsAfterOneRollbackAttempt()
    {
        VersionManagerState pending = VersionActivationPolicy.BeginActivation(
            State(),
            ManagedAppVersion.Parse("0.10.6"));
        var process = new FakeProcess(
            ManagedProcessStartOutcome.ReadyTimeout,
            ManagedProcessStartOutcome.InvalidReadySignal);
        var coordinator = new ManagedActivationCoordinator(
            "managed",
            new FakeStateStore(pending),
            new HealthyRepository(),
            process,
            TimeSpan.FromSeconds(1));

        ManagedLauncherResult result = await coordinator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ManagedLauncherOutcome.StartFailed, result.Outcome);
        Assert.Equal(["0.10.6", "0.10.5"], process.Starts);
    }

    private static VersionManagerState State()
    {
        ManagedAppVersion old = ManagedAppVersion.Parse("0.10.5");
        return VersionManagerState.Create(
            null,
            old,
            old,
            [Admission("0.10.5"), Admission("0.10.6")],
            null,
            null,
            false);
    }

    private static ManagedVersionAdmission Admission(string version)
    {
        return new(ManagedAppVersion.Parse(version), $"identity-{version}", new string('a', 64));
    }

    private sealed class FakeStateStore(VersionManagerState state) : IVersionManagerStateStore
    {
        internal VersionManagerState State { get; private set; } = state;

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(State, VersionManagerStateLoadIssue.None));
        }

        public ValueTask SaveAsync(VersionManagerState stateToSave, CancellationToken cancellationToken)
        {
            State = stateToSave;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class LoadIssueStateStore(VersionManagerStateLoadIssue issue) : IVersionManagerStateStore
    {
        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new VersionManagerStateLoadResult(null, issue));
        }

        public ValueTask SaveAsync(VersionManagerState stateToSave, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeProcess(params ManagedProcessStartOutcome[] outcomes) : IManagedApplicationProcess
    {
        private readonly Queue<ManagedProcessStartOutcome> _outcomes = new(outcomes);

        internal List<string> Starts { get; } = [];

        public ValueTask<ManagedProcessStartResult> StartUntilReadyAsync(
            string managedRoot,
            ManagedAppVersion version,
            TimeSpan readyDeadline,
            CancellationToken cancellationToken)
        {
            Starts.Add(version.ToString());
            return ValueTask.FromResult(new ManagedProcessStartResult(_outcomes.Dequeue(), null));
        }
    }

    private sealed class HealthyRepository(params string[] damagedVersions) : IManagedVersionRepository
    {
        private readonly HashSet<ManagedAppVersion> _damaged =
            [.. damagedVersions.Select(ManagedAppVersion.Parse)];

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
            return ValueTask.FromResult(ManagedVersionInventory.Create(admissions.Select(admission =>
            {
                bool damaged = _damaged.Contains(admission.Version);
                return new InstalledVersionSnapshot(
                    admission.Version,
                    admission.AdmissionIdentity,
                    damaged ? ManagedVersionIntegrity.Damaged : ManagedVersionIntegrity.Healthy,
                    damaged ? ManagedVersionDamageReason.ContentMismatch : null,
                    activeVersion == admission.Version,
                    lastKnownGoodVersion == admission.Version);
            })));
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
