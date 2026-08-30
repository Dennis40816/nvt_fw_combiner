using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Locks the complete Application-owned read-only recovery decision table.</summary>
public sealed class ManagedSetupRecoveryDiagnosisTests
{
    private const string ManagedRoot = @"C:\managed\NVT FW Combiner";
    private const string StatePath = @"C:\state\version-manager-state.json";
    private static readonly HashSet<(ManagedSetupRecoveryPhase Phase, StateCase State,
        ManagedInstallationRootStatus Root)> ActionableExactTuples =
        [
            (ManagedSetupRecoveryPhase.Staging, StateCase.Missing,
                ManagedInstallationRootStatus.Residue),
            (ManagedSetupRecoveryPhase.RootPromoted, StateCase.Missing,
                ManagedInstallationRootStatus.Residue),
            (ManagedSetupRecoveryPhase.BootstrapLaunchRecorded, StateCase.Missing,
                ManagedInstallationRootStatus.Residue),
            (ManagedSetupRecoveryPhase.BootstrapLaunchRecorded, StateCase.Exact,
                ManagedInstallationRootStatus.Residue),
        ];
    private static readonly HashSet<(StateCase State, ManagedInstallationRootStatus Root)>
        HealthyAbsentTuples =
        [
            (StateCase.Missing, ManagedInstallationRootStatus.Absent),
            (StateCase.Exact, ManagedInstallationRootStatus.Present),
        ];
    private static readonly HashSet<(ManagedSetupRecoveryPhase Phase, StateCase State,
        ManagedInstallationRootStatus Root)> ExactHealthUnavailableTuples =
        BuildExactHealthUnavailableTuples();
    private static readonly HashSet<(StateCase State, ManagedInstallationRootStatus Root)>
        AbsentHealthUnavailableTuples = BuildAbsentHealthUnavailableTuples();

    /// <summary>Every exact marker phase is evaluated against every state and root category.</summary>
    [Theory]
    [MemberData(nameof(ExactMarkerMatrix))]
    public async Task ExactMarkerDecisionCoversEveryPhaseStateAndRootCombination(
        ManagedSetupRecoveryPhase phase,
        StateCase stateCase,
        ManagedInstallationRootStatus rootStatus,
        ManagedSetupRecoveryOutcome expected)
    {
        ManagedSetupRecoveryTransaction transaction = CreateTransaction(phase);
        ManagedInstallationRecoveryExperience experience = CreateExperience(
            StateResult(stateCase),
            rootStatus,
            new(ManagedSetupRecoveryFactKind.Exact, transaction));

        ManagedSetupRecoveryDiagnosis result = await experience.DiagnoseAsync(
            ManagedRoot,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        if (expected == ManagedSetupRecoveryOutcome.ActionAvailable)
        {
            Assert.Same(transaction, result.Transaction);
        }
        else
        {
            Assert.Null(result.Transaction);
        }
    }

    /// <summary>An absent marker is valid only for a clean empty root or a healthy bound install.</summary>
    [Theory]
    [MemberData(nameof(AbsentMarkerMatrix))]
    public async Task AbsentMarkerDecisionCoversEveryStateAndRootCombination(
        StateCase stateCase,
        ManagedInstallationRootStatus rootStatus,
        ManagedSetupRecoveryOutcome expected)
    {
        ManagedInstallationRecoveryExperience experience = CreateExperience(
            StateResult(stateCase),
            rootStatus,
            new(ManagedSetupRecoveryFactKind.Absent, transaction: null));

        ManagedSetupRecoveryDiagnosis result = await experience.DiagnoseAsync(
            ManagedRoot,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Transaction);
    }

    /// <summary>Every malformed, foreign, or incomplete marker fact fails closed.</summary>
    [Theory]
    [InlineData(ManagedSetupRecoveryFactKind.Malformed, ManagedSetupRecoveryOutcome.ManualInterventionRequired)]
    [InlineData(ManagedSetupRecoveryFactKind.IdentityMismatch, ManagedSetupRecoveryOutcome.ManualInterventionRequired)]
    [InlineData(ManagedSetupRecoveryFactKind.AccessDenied, ManagedSetupRecoveryOutcome.HealthUnavailable)]
    [InlineData(ManagedSetupRecoveryFactKind.Changed, ManagedSetupRecoveryOutcome.ManualInterventionRequired)]
    [InlineData(ManagedSetupRecoveryFactKind.Unavailable, ManagedSetupRecoveryOutcome.HealthUnavailable)]
    public async Task NonExactMarkerFactsUseClosedTerminalOutcomes(
        ManagedSetupRecoveryFactKind kind,
        ManagedSetupRecoveryOutcome expected)
    {
        ManagedInstallationRecoveryExperience experience = CreateExperience(
            StateResult(StateCase.Exact),
            ManagedInstallationRootStatus.Present,
            new(kind, transaction: null));

        ManagedSetupRecoveryDiagnosis result = await experience.DiagnoseAsync(
            ManagedRoot,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Transaction);
    }

    /// <summary>Transient marker contention is incomplete health and exposes no action.</summary>
    [Fact]
    public async Task TransientMarkerContentionReturnsHealthUnavailableWithoutTransaction()
    {
        var marker = new RecordingMarkerProbe(
            new(ManagedSetupRecoveryFactKind.Unavailable, transaction: null));
        var experience = new ManagedInstallationRecoveryExperience(
            new RecordingStateReader(StateResult(StateCase.Exact)),
            new RecordingRootProbe(ManagedInstallationRootStatus.Present),
            marker,
            new RecordingLifetimeProbe(_ => ManagedProcessLifetimeStatus.Exited));

        ManagedSetupRecoveryDiagnosis result = await experience.DiagnoseAsync(
            ManagedRoot,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryOutcome.HealthUnavailable, result.Outcome);
        Assert.Null(result.Transaction);
        Assert.Equal(1, marker.CallCount);
    }

    /// <summary>The reader-owned state identity is reused for every path-bound observation.</summary>
    [Fact]
    public async Task ReaderIdentityOwnsEveryRecoveryStatePathObservation()
    {
        const string ReaderStatePath = @"C:\state-a\version-manager-state.json";
        var state = new RecordingStateReader(
            StateResult(StateCase.Missing),
            statePathIdentity: ReaderStatePath);
        var marker = new RecordingMarkerProbe(
            new(ManagedSetupRecoveryFactKind.Absent, transaction: null));
        var lifetime = new RecordingLifetimeProbe(_ => ManagedProcessLifetimeStatus.Exited);
        var experience = new ManagedInstallationRecoveryExperience(
            state,
            new RecordingRootProbe(ManagedInstallationRootStatus.Absent),
            marker,
            lifetime);

        ManagedSetupRecoveryDiagnosis result = await experience.DiagnoseAsync(
            ManagedRoot,
            TestContext.Current.CancellationToken);

        string exactStatePath = Path.GetFullPath(ReaderStatePath);
        Assert.Equal(ManagedSetupRecoveryOutcome.NoRecoveryNeeded, result.Outcome);
        Assert.Equal(Enumerable.Repeat(exactStatePath, 3), lifetime.StatePaths);
        Assert.Equal([exactStatePath], marker.StatePaths);
    }

    /// <summary>Any active role wins over all unavailable roles and blocks recovery observation.</summary>
    [Theory]
    [MemberData(nameof(ActiveUnavailablePrecedenceCases))]
    public async Task EveryActiveLifetimeRoleWinsOverEveryDistinctUnavailableRole(
        ManagedProcessLifetimeKind activeRole,
        ManagedProcessLifetimeKind unavailableRole)
    {
        var lifetime = new RecordingLifetimeProbe(kind =>
            kind == activeRole
                ? ManagedProcessLifetimeStatus.Active
                : kind == unavailableRole
                    ? ManagedProcessLifetimeStatus.Unavailable
                    : ManagedProcessLifetimeStatus.Exited);
        var state = new RecordingStateReader(StateResult(StateCase.Exact));
        var root = new RecordingRootProbe(ManagedInstallationRootStatus.Present);
        var marker = new RecordingMarkerProbe(
            new(ManagedSetupRecoveryFactKind.Absent, transaction: null));
        var experience = new ManagedInstallationRecoveryExperience(state, root, marker, lifetime);

        ManagedSetupRecoveryDiagnosis result = await experience.DiagnoseAsync(
            ManagedRoot,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryOutcome.Busy, result.Outcome);
        Assert.Equal(3, lifetime.Calls.Count);
        Assert.Equal(0, state.CallCount);
        Assert.Equal(0, root.CallCount);
        Assert.Equal(0, marker.CallCount);
    }

    /// <summary>Cancellation at every observation boundary propagates and stops later reads.</summary>
    [Theory]
    [InlineData(ObservationBoundary.BootstrapLifetime, 1, 0, 0, 0)]
    [InlineData(ObservationBoundary.ApplicationLifetime, 2, 0, 0, 0)]
    [InlineData(ObservationBoundary.LauncherLifetime, 3, 0, 0, 0)]
    [InlineData(ObservationBoundary.State, 3, 1, 0, 0)]
    [InlineData(ObservationBoundary.Root, 3, 1, 1, 0)]
    [InlineData(ObservationBoundary.Marker, 3, 1, 1, 1)]
    public async Task CancellationAtEveryBoundaryPropagatesWithoutLaterObservation(
        ObservationBoundary boundary,
        int expectedLifetimeCalls,
        int expectedStateCalls,
        int expectedRootCalls,
        int expectedMarkerCalls)
    {
        using var cancellation = new CancellationTokenSource();
        var lifetime = new RecordingLifetimeProbe(
            _ => ManagedProcessLifetimeStatus.Exited,
            kind =>
            {
                if (boundary == LifetimeBoundary(kind))
                {
                    cancellation.Cancel();
                }
            });
        var state = new RecordingStateReader(
            StateResult(StateCase.Exact),
            () => CancelAt(ObservationBoundary.State));
        var root = new RecordingRootProbe(
            ManagedInstallationRootStatus.Present,
            () => CancelAt(ObservationBoundary.Root));
        var marker = new RecordingMarkerProbe(
            new(ManagedSetupRecoveryFactKind.Absent, transaction: null),
            () => CancelAt(ObservationBoundary.Marker));
        var experience = new ManagedInstallationRecoveryExperience(state, root, marker, lifetime);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await experience.DiagnoseAsync(ManagedRoot, cancellation.Token));

        Assert.Equal(expectedLifetimeCalls, lifetime.Calls.Count);
        Assert.Equal(expectedStateCalls, state.CallCount);
        Assert.Equal(expectedRootCalls, root.CallCount);
        Assert.Equal(expectedMarkerCalls, marker.CallCount);
        return;

        void CancelAt(ObservationBoundary current)
        {
            if (boundary == current)
            {
                cancellation.Cancel();
            }
        }
    }

    /// <summary>Any unavailable role makes complete health unavailable when no role is active.</summary>
    [Theory]
    [InlineData(ManagedProcessLifetimeKind.Bootstrap)]
    [InlineData(ManagedProcessLifetimeKind.Application)]
    [InlineData(ManagedProcessLifetimeKind.Launcher)]
    public async Task EveryUnavailableLifetimeRoleReturnsHealthUnavailable(
        ManagedProcessLifetimeKind unavailableRole)
    {
        var lifetime = new RecordingLifetimeProbe(kind =>
            kind == unavailableRole
                ? ManagedProcessLifetimeStatus.Unavailable
                : ManagedProcessLifetimeStatus.Exited);
        var state = new RecordingStateReader(StateResult(StateCase.Exact));
        var root = new RecordingRootProbe(ManagedInstallationRootStatus.Present);
        var marker = new RecordingMarkerProbe(
            new(ManagedSetupRecoveryFactKind.Absent, transaction: null));
        var experience = new ManagedInstallationRecoveryExperience(state, root, marker, lifetime);

        ManagedSetupRecoveryDiagnosis result = await experience.DiagnoseAsync(
            ManagedRoot,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryOutcome.HealthUnavailable, result.Outcome);
        Assert.Equal(3, lifetime.Calls.Count);
        Assert.Equal(0, state.CallCount);
        Assert.Equal(0, root.CallCount);
        Assert.Equal(0, marker.CallCount);
    }

    /// <summary>All three roles are always observed through the exact read-only lifetime port.</summary>
    [Fact]
    public async Task DiagnosisObservesAllLifetimeRolesExactlyOnce()
    {
        var lifetime = new RecordingLifetimeProbe(_ => ManagedProcessLifetimeStatus.Exited);
        ManagedInstallationRecoveryExperience experience = CreateExperience(
            StateResult(StateCase.Missing),
            ManagedInstallationRootStatus.Absent,
            new(ManagedSetupRecoveryFactKind.Absent, transaction: null),
            lifetime);

        ManagedSetupRecoveryDiagnosis result = await experience.DiagnoseAsync(
            ManagedRoot,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedSetupRecoveryOutcome.NoRecoveryNeeded, result.Outcome);
        Assert.Equal(
            [
                ManagedProcessLifetimeKind.Bootstrap,
                ManagedProcessLifetimeKind.Application,
                ManagedProcessLifetimeKind.Launcher,
            ],
            lifetime.Calls);
    }

    /// <summary>Facts cannot contradict their optional transaction.</summary>
    [Fact]
    public void FactRejectsContradictoryPayloads()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new ManagedSetupRecoveryFact(ManagedSetupRecoveryFactKind.Exact, transaction: null));
        _ = Assert.Throws<ArgumentException>(() =>
            new ManagedSetupRecoveryFact(
                ManagedSetupRecoveryFactKind.Absent,
                CreateTransaction(ManagedSetupRecoveryPhase.Staging)));
    }

    /// <summary>Builds the complete exact-marker phase/state/root decision table.</summary>
    public static TheoryData<ManagedSetupRecoveryPhase, StateCase,
        ManagedInstallationRootStatus, ManagedSetupRecoveryOutcome> ExactMarkerMatrix()
    {
        var data = new TheoryData<ManagedSetupRecoveryPhase, StateCase,
            ManagedInstallationRootStatus, ManagedSetupRecoveryOutcome>();
        foreach (ManagedSetupRecoveryPhase phase in Enum.GetValues<ManagedSetupRecoveryPhase>())
        {
            foreach (StateCase state in Enum.GetValues<StateCase>())
            {
                foreach (ManagedInstallationRootStatus root in
                    Enum.GetValues<ManagedInstallationRootStatus>())
                {
                    (ManagedSetupRecoveryPhase, StateCase, ManagedInstallationRootStatus) tuple =
                        (phase, state, root);
                    ManagedSetupRecoveryOutcome expected = ActionableExactTuples.Contains(tuple)
                        ? ManagedSetupRecoveryOutcome.ActionAvailable
                        : ExactHealthUnavailableTuples.Contains(tuple)
                            ? ManagedSetupRecoveryOutcome.HealthUnavailable
                            : ManagedSetupRecoveryOutcome.ManualInterventionRequired;
                    data.Add(phase, state, root, expected);
                }
            }
        }
        return data;
    }

    /// <summary>Builds the complete absent-marker state/root decision table.</summary>
    public static TheoryData<StateCase, ManagedInstallationRootStatus,
        ManagedSetupRecoveryOutcome> AbsentMarkerMatrix()
    {
        var data = new TheoryData<StateCase, ManagedInstallationRootStatus,
            ManagedSetupRecoveryOutcome>();
        foreach (StateCase state in Enum.GetValues<StateCase>())
        {
            foreach (ManagedInstallationRootStatus root in
                Enum.GetValues<ManagedInstallationRootStatus>())
            {
                (StateCase, ManagedInstallationRootStatus) tuple = (state, root);
                ManagedSetupRecoveryOutcome expected = HealthyAbsentTuples.Contains(tuple)
                    ? ManagedSetupRecoveryOutcome.NoRecoveryNeeded
                    : AbsentHealthUnavailableTuples.Contains(tuple)
                        ? ManagedSetupRecoveryOutcome.HealthUnavailable
                        : ManagedSetupRecoveryOutcome.ManualInterventionRequired;
                data.Add(state, root, expected);
            }
        }
        return data;
    }

    /// <summary>The ADR names exactly four actionable exact tuples and two healthy absences.</summary>
    [Fact]
    public void DeclarativeDecisionSetsContainOnlyAdrAuthorizedSuccesses()
    {
        Assert.Equal(4, ActionableExactTuples.Count);
        Assert.Equal(2, HealthyAbsentTuples.Count);
        Assert.DoesNotContain(ActionableExactTuples, ExactHealthUnavailableTuples.Contains);
        Assert.DoesNotContain(HealthyAbsentTuples, AbsentHealthUnavailableTuples.Contains);
    }

    /// <summary>Every ordered pair of distinct lifetime roles exercises Busy precedence.</summary>
    public static TheoryData<ManagedProcessLifetimeKind, ManagedProcessLifetimeKind>
        ActiveUnavailablePrecedenceCases()
    {
        var data = new TheoryData<ManagedProcessLifetimeKind, ManagedProcessLifetimeKind>();
        foreach (ManagedProcessLifetimeKind active in Enum.GetValues<ManagedProcessLifetimeKind>())
        {
            foreach (ManagedProcessLifetimeKind unavailable in
                Enum.GetValues<ManagedProcessLifetimeKind>().Where(value => value != active))
            {
                data.Add(active, unavailable);
            }
        }
        return data;
    }

    private static HashSet<(ManagedSetupRecoveryPhase, StateCase,
        ManagedInstallationRootStatus)> BuildExactHealthUnavailableTuples()
    {
        var tuples = new HashSet<(ManagedSetupRecoveryPhase, StateCase,
            ManagedInstallationRootStatus)>();
        foreach (ManagedSetupRecoveryPhase phase in Enum.GetValues<ManagedSetupRecoveryPhase>())
        {
            foreach (ManagedInstallationRootStatus root in
                Enum.GetValues<ManagedInstallationRootStatus>())
            {
                _ = tuples.Add((phase, StateCase.Unavailable, root));
            }
            foreach (StateCase state in Enum.GetValues<StateCase>().Where(
                value => value != StateCase.Unavailable))
            {
                _ = tuples.Add((phase, state, ManagedInstallationRootStatus.PermissionDenied));
                _ = tuples.Add((phase, state, ManagedInstallationRootStatus.Unavailable));
            }
        }
        return tuples;
    }

    private static HashSet<(StateCase, ManagedInstallationRootStatus)>
        BuildAbsentHealthUnavailableTuples()
    {
        var tuples = new HashSet<(StateCase, ManagedInstallationRootStatus)>();
        foreach (ManagedInstallationRootStatus root in
            Enum.GetValues<ManagedInstallationRootStatus>())
        {
            _ = tuples.Add((StateCase.Unavailable, root));
        }
        foreach (StateCase state in Enum.GetValues<StateCase>().Where(
            value => value != StateCase.Unavailable))
        {
            _ = tuples.Add((state, ManagedInstallationRootStatus.PermissionDenied));
            _ = tuples.Add((state, ManagedInstallationRootStatus.Unavailable));
        }
        return tuples;
    }

    private static ObservationBoundary LifetimeBoundary(ManagedProcessLifetimeKind kind)
    {
        return kind switch
        {
            ManagedProcessLifetimeKind.Bootstrap => ObservationBoundary.BootstrapLifetime,
            ManagedProcessLifetimeKind.Application => ObservationBoundary.ApplicationLifetime,
            ManagedProcessLifetimeKind.Launcher => ObservationBoundary.LauncherLifetime,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static ManagedInstallationRecoveryExperience CreateExperience(
        VersionManagerStateLoadResult state,
        ManagedInstallationRootStatus root,
        ManagedSetupRecoveryFact marker,
        RecordingLifetimeProbe? lifetime = null)
    {
        return new(
            new RecordingStateReader(state),
            new RecordingRootProbe(root),
            new RecordingMarkerProbe(marker),
            lifetime ?? new RecordingLifetimeProbe(_ => ManagedProcessLifetimeStatus.Exited));
    }

    private static VersionManagerStateLoadResult StateResult(StateCase value)
    {
        return value switch
        {
            StateCase.Missing => new(null, VersionManagerStateLoadIssue.Missing),
            StateCase.Exact => new(CreateState(ManagedRoot), VersionManagerStateLoadIssue.None),
            StateCase.Invalid => new(null, VersionManagerStateLoadIssue.Invalid),
            StateCase.ManagedRootMismatch =>
                new(null, VersionManagerStateLoadIssue.ManagedRootMismatch),
            StateCase.Unbound => new(CreateState(managedRoot: null), VersionManagerStateLoadIssue.None),
            StateCase.WrongBound =>
                new(CreateState(@"C:\managed\foreign"), VersionManagerStateLoadIssue.None),
            StateCase.Unavailable => new(null, VersionManagerStateLoadIssue.Unavailable),
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
    }

    private static VersionManagerState CreateState(string? managedRoot)
    {
        return VersionManagerState.Create(
            updateSource: null,
            activeVersion: null,
            lastKnownGoodVersion: null,
            admissions: [],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: managedRoot);
    }

    private static ManagedSetupRecoveryTransaction CreateTransaction(
        ManagedSetupRecoveryPhase phase)
    {
        return new(
            "0123456789abcdef0123456789abcdef",
            ManagedRoot,
            StatePath,
            phase,
            ["NVT FW Combiner", "NVT FW Combiner.managed-setup-transaction.v1.json", ".managed-setup-staging/0123456789abcdef0123456789abcdef"],
            new(123, new string('a', 64), 456, new string('b', 64), "NvtFwCombiner.Bootstrap.exe", 789, new string('c', 64)),
            new(4, new string('d', 64), 1, "1.0.6", new string('e', 64), @"G:\AUTO\catalog.json", "registry", @"G:\AUTO", "latest", "1.0.6", "packages/app.zip", 1024, new string('f', 64), new string('1', 64), "entry"));
    }

    /// <summary>Complete durable-state categories admitted by the recovery table.</summary>
    public enum StateCase
    {
        /// <summary>No state exists.</summary>
        Missing,
        /// <summary>Validated state is bound to the requested root.</summary>
        Exact,
        /// <summary>State bytes or shape are invalid.</summary>
        Invalid,
        /// <summary>The state adapter reported a managed-root mismatch.</summary>
        ManagedRootMismatch,
        /// <summary>Validated state remains an unbound seed.</summary>
        Unbound,
        /// <summary>Validated state is bound to another root.</summary>
        WrongBound,
        /// <summary>State could not be observed completely.</summary>
        Unavailable,
    }

    /// <summary>Ordered observation boundaries owned by the recovery experience.</summary>
    public enum ObservationBoundary
    {
        /// <summary>Bootstrap lifetime read.</summary>
        BootstrapLifetime,
        /// <summary>Application lifetime read.</summary>
        ApplicationLifetime,
        /// <summary>Launcher lifetime read.</summary>
        LauncherLifetime,
        /// <summary>Durable-state read.</summary>
        State,
        /// <summary>Managed-root read.</summary>
        Root,
        /// <summary>Setup-marker read.</summary>
        Marker,
    }

    private sealed class RecordingStateReader(
        VersionManagerStateLoadResult result,
        Action? beforeReturn = null,
        string statePathIdentity = StatePath)
        : IManagedSetupRecoveryStateReader
    {
        internal int CallCount { get; private set; }

        public string StatePathIdentity { get; } = Path.GetFullPath(statePathIdentity);

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            beforeReturn?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingRootProbe(
        ManagedInstallationRootStatus status,
        Action? beforeReturn = null)
        : IManagedInstallationRootProbe
    {
        internal int CallCount { get; private set; }

        public ValueTask<ManagedInstallationRootObservation> ObserveAsync(
            string managedRoot,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            beforeReturn?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new ManagedInstallationRootObservation(status));
        }
    }

    private sealed class RecordingMarkerProbe(
        ManagedSetupRecoveryFact result,
        Action? beforeReturn = null)
        : IManagedSetupRecoveryProbe
    {
        internal int CallCount { get; private set; }
        internal List<string> StatePaths { get; } = [];

        public ValueTask<ManagedSetupRecoveryFact> ObserveAsync(
            string managedRoot,
            string statePathIdentity,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            StatePaths.Add(statePathIdentity);
            beforeReturn?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingLifetimeProbe(
        Func<ManagedProcessLifetimeKind, ManagedProcessLifetimeStatus> observe,
        Action<ManagedProcessLifetimeKind>? beforeReturn = null)
        : IManagedProcessLifetimeProbe
    {
        internal List<ManagedProcessLifetimeKind> Calls { get; } = [];
        internal List<string> StatePaths { get; } = [];

        public ValueTask<ManagedProcessLifetimeStatus> ObserveAsync(
            string statePath,
            ManagedProcessLifetimeKind kind,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(kind);
            StatePaths.Add(statePath);
            beforeReturn?.Invoke(kind);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(observe(kind));
        }
    }
}
