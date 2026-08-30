using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Complete durable Application-state categories admitted by diagnosis.</summary>
public enum RecoveryStateCase
{
    /// <summary>No state exists.</summary>
    Missing,
    /// <summary>The canonical first-run state is bound to the requested root.</summary>
    Exact,
    /// <summary>State bytes or shape are invalid.</summary>
    Invalid,
    /// <summary>The adapter reported a managed-root mismatch.</summary>
    ManagedRootMismatch,
    /// <summary>A validated state remains unbound.</summary>
    Unbound,
    /// <summary>A validated state is bound to another root.</summary>
    WrongBound,
    /// <summary>State could not be observed completely.</summary>
    Unavailable,
}

/// <summary>Ordered 105A read-only observation boundaries.</summary>
public enum RecoveryObservationBoundary
{
    /// <summary>Bootstrap lifetime observation.</summary>
    BootstrapLifetime,
    /// <summary>Application lifetime observation.</summary>
    ApplicationLifetime,
    /// <summary>Launcher lifetime observation.</summary>
    LauncherLifetime,
    /// <summary>Application state observation.</summary>
    State,
    /// <summary>Managed root observation.</summary>
    Root,
    /// <summary>Setup marker observation.</summary>
    Marker,
    /// <summary>Candidate, Launcher, inventory, and restart-prefix observation.</summary>
    Evidence,
}

/// <summary>Launcher-state shapes used by the closed recovery table tests.</summary>
public enum RecoveryLauncherCase
{
    /// <summary>No Launcher state exists.</summary>
    Missing,
    /// <summary>The first candidate is requested.</summary>
    Requested,
    /// <summary>The first candidate launch is durably recorded.</summary>
    CandidateLaunchRecorded,
    /// <summary>The first candidate failed before READY.</summary>
    Failed,
    /// <summary>The first candidate is committed Active and LKG.</summary>
    Ready,
    /// <summary>A different candidate is pending.</summary>
    RequestedDifferentCandidate,
    /// <summary>An ordinary update retains an existing Active and LKG.</summary>
    OrdinaryUpdate,
    /// <summary>READY retains a foreign failure.</summary>
    ReadyWithFailure,
}

internal static class RecoveryTestData
{
    internal const string ManagedRoot = @"C:\managed\NVT FW Combiner";
    internal const string StatePath = @"C:\state\version-manager-state.json";
    internal static readonly ManagedAppVersion Version = ManagedAppVersion.Parse("1.0.6");
    internal static readonly ManagedVersionAdmission Admission =
        new(Version, "entry", new string('1', 64));
    internal static readonly ManagedLauncherIdentity Launcher = ManagedLauncherIdentity.Create(
        Version,
        Admission.AdmissionIdentity,
        Admission.ReleaseManifestSha256,
        Version,
        ManagedLauncherIdentity.SupportedProtocolVersion,
        ManagedLauncherIdentity.ExecutablePath,
        123,
        new string('a', 64));
    internal static readonly ManagedLauncherIdentity OtherLauncher = ManagedLauncherIdentity.Create(
        Version,
        Admission.AdmissionIdentity,
        Admission.ReleaseManifestSha256,
        Version,
        ManagedLauncherIdentity.SupportedProtocolVersion,
        ManagedLauncherIdentity.ExecutablePath,
        124,
        new string('b', 64));

    internal static VersionManagerStateLoadResult MissingAppState { get; } =
        new(null, VersionManagerStateLoadIssue.Missing);

    internal static VersionManagerStateLoadResult CanonicalAppState { get; } = new(
        ManagedVersionSeedPolicy.CreateCanonicalFirstRunSeed(Admission).BindToManagedRoot(ManagedRoot),
        VersionManagerStateLoadIssue.None);

    internal static VersionManagerStateLoadResult NonCanonicalBoundAppState { get; } = new(
        VersionManagerState.Create(
            updateSource: null,
            activeVersion: null,
            lastKnownGoodVersion: null,
            admissions: [],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: ManagedRoot),
        VersionManagerStateLoadIssue.None);

    internal static ManagedSetupRecoveryTransaction Transaction(ManagedSetupRecoveryPhase phase)
    {
        return new(
            "0123456789abcdef0123456789abcdef",
            ManagedRoot,
            StatePath,
            phase,
            ["NVT FW Combiner", "NVT FW Combiner.managed-setup-transaction.v1.json",
                ".managed-setup-staging/0123456789abcdef0123456789abcdef"],
            new(123, new string('a', 64), 456, new string('b', 64),
                "NvtFwCombiner.Bootstrap.exe", 789, new string('c', 64)),
            new(4, new string('d', 64), 1, "1.0.6", new string('e', 64),
                @"G:\AUTO\catalog.json", "registry", @"G:\AUTO", "latest", "1.0.6",
                "packages/app.zip", 1024, new string('f', 64),
                Admission.ReleaseManifestSha256, Admission.AdmissionIdentity));
    }

    internal static ManagedSetupRecoveryEvidenceObservation Evidence(
        RecoveryLauncherCase launcherCase,
        string token = "evidence-1",
        bool includeInstalledLauncher = true)
    {
        LauncherBootstrapStateLoadResult state = launcherCase switch
        {
            RecoveryLauncherCase.Missing => new(null, LauncherBootstrapStateLoadIssue.Missing),
            RecoveryLauncherCase.Requested => Exact(
                LauncherBootstrapState.Create(ManagedRoot, null, null,
                    PendingLauncherActivation.Create(Launcher, null, null,
                        LauncherActivationPhase.Requested), null)),
            RecoveryLauncherCase.CandidateLaunchRecorded => Exact(
                LauncherBootstrapState.Create(ManagedRoot, null, null,
                    PendingLauncherActivation.Create(Launcher, null, null,
                        LauncherActivationPhase.CandidateLaunchRecorded), null)),
            RecoveryLauncherCase.Failed => Exact(
                LauncherBootstrapState.Create(ManagedRoot, null, null, null, Launcher)),
            RecoveryLauncherCase.Ready => Exact(
                LauncherBootstrapState.Create(ManagedRoot, Launcher, Launcher, null, null)),
            RecoveryLauncherCase.RequestedDifferentCandidate => Exact(
                LauncherBootstrapState.Create(ManagedRoot, null, null,
                    PendingLauncherActivation.Create(OtherLauncher, null, null,
                        LauncherActivationPhase.Requested), null)),
            RecoveryLauncherCase.OrdinaryUpdate => Exact(
                LauncherBootstrapState.Create(ManagedRoot, Launcher, Launcher,
                    PendingLauncherActivation.Create(OtherLauncher, Launcher, Launcher,
                        LauncherActivationPhase.Requested), null)),
            RecoveryLauncherCase.ReadyWithFailure => Exact(
                LauncherBootstrapState.Create(ManagedRoot, Launcher, Launcher, null,
                    OtherLauncher)),
            _ => throw new ArgumentOutOfRangeException(nameof(launcherCase)),
        };
        return new(
            Admission,
            includeInstalledLauncher ? Launcher : null,
            state,
            new TestRecoveryExecutionToken(token));

        static LauncherBootstrapStateLoadResult Exact(LauncherBootstrapState value)
        {
            return new(value, LauncherBootstrapStateLoadIssue.None);
        }
    }
}

internal sealed record TestRecoveryExecutionToken(string Value)
    : ManagedSetupRecoveryExecutionToken;

internal sealed class RecoveryHarness
{
    internal RecoveryHarness(
        RecordingRecoveryStateStore state,
        RecordingRecoveryRootProbe root,
        RecordingRecoveryMarkerProbe marker,
        RecordingRecoveryLifetimeProbe lifetime,
        RecordingRecoveryEvidenceProbe evidence,
        RecordingRecoveryExecutionPort execution)
    {
        State = state;
        Root = root;
        Marker = marker;
        Lifetime = lifetime;
        Evidence = evidence;
        Execution = execution;
        Experience = new(state, root, marker, lifetime, evidence);
        Coordinator = new(state, root, marker, lifetime, evidence, execution);
    }

    internal ManagedInstallationRecoveryExperience Experience { get; }
    internal ManagedSetupRecoveryExecutionCoordinator Coordinator { get; }
    internal RecordingRecoveryStateStore State { get; }
    internal RecordingRecoveryRootProbe Root { get; }
    internal RecordingRecoveryMarkerProbe Marker { get; }
    internal RecordingRecoveryLifetimeProbe Lifetime { get; }
    internal RecordingRecoveryEvidenceProbe Evidence { get; }
    internal RecordingRecoveryExecutionPort Execution { get; }

    internal static RecoveryHarness Create(
        VersionManagerStateLoadResult appState,
        ManagedSetupRecoveryFact marker,
        ManagedSetupRecoveryEvidenceObservation evidence,
        ManagedInstallationRootStatus root = ManagedInstallationRootStatus.Residue)
    {
        return new(
            new RecordingRecoveryStateStore(appState),
            new RecordingRecoveryRootProbe(root),
            new RecordingRecoveryMarkerProbe(marker),
            new RecordingRecoveryLifetimeProbe(),
            new RecordingRecoveryEvidenceProbe(evidence),
            new RecordingRecoveryExecutionPort());
    }
}

internal sealed class RecordingRecoveryStateStore : IManagedSetupRecoveryStateStore
{
    private readonly Action? _beforeReturn;
    private readonly Queue<VersionManagerStateLoadResult> _results;

    internal RecordingRecoveryStateStore(params VersionManagerStateLoadResult[] results)
        : this(results, beforeReturn: null)
    {
    }

    internal RecordingRecoveryStateStore(
        VersionManagerStateLoadResult[] results,
        Action? beforeReturn = null,
        string statePathIdentity = RecoveryTestData.StatePath)
    {
        _results = new(results);
        _beforeReturn = beforeReturn;
        StatePathIdentity = Path.GetFullPath(statePathIdentity);
    }

    internal int LoadCount { get; private set; }
    internal int LeaseCount { get; private set; }
    internal VersionManagerWriteLeaseIssue LeaseIssue { get; set; }
    internal bool LeaseHeld { get; private set; }
    internal VersionManagerWriteLeaseResult? LastLease { get; private set; }
    public string StatePathIdentity { get; }

    public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LoadCount++;
        _beforeReturn?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        VersionManagerStateLoadResult result = _results.Count > 1 ? _results.Dequeue() : _results.Peek();
        return ValueTask.FromResult(result);
    }

    public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LeaseCount++;
        if (LeaseIssue != VersionManagerWriteLeaseIssue.None)
        {
#pragma warning disable CA2000 // Ownership transfers to the returned lease result.
            return ValueTask.FromResult(new VersionManagerWriteLeaseResult(LeaseIssue));
#pragma warning restore CA2000
        }
        LeaseHeld = true;
#pragma warning disable CA2000 // Ownership transfers through LastLease to the caller.
        LastLease = new VersionManagerWriteLeaseResult(
            VersionManagerWriteLeaseIssue.None,
            new CallbackDisposable(() => LeaseHeld = false));
#pragma warning restore CA2000
        return ValueTask.FromResult(LastLease);
    }

    public ValueTask SaveAsync(VersionManagerState state, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}

internal sealed class RecordingRecoveryRootProbe : IManagedInstallationRootProbe
{
    private readonly Action? _beforeReturn;

    internal RecordingRecoveryRootProbe(
        ManagedInstallationRootStatus status,
        Action? beforeReturn = null)
    {
        Status = status;
        _beforeReturn = beforeReturn;
    }

    internal int CallCount { get; private set; }
    internal ManagedInstallationRootStatus Status { get; set; }

    public ValueTask<ManagedInstallationRootObservation> ObserveAsync(
        string managedRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        _beforeReturn?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ManagedInstallationRootObservation(Status));
    }
}

internal sealed class RecordingRecoveryMarkerProbe : IManagedSetupRecoveryProbe
{
    private readonly Action? _beforeReturn;
    private readonly Queue<ManagedSetupRecoveryFact> _facts;

    internal RecordingRecoveryMarkerProbe(params ManagedSetupRecoveryFact[] facts)
        : this(facts, beforeReturn: null)
    {
    }

    internal RecordingRecoveryMarkerProbe(
        ManagedSetupRecoveryFact[] facts,
        Action? beforeReturn)
    {
        _facts = new(facts);
        _beforeReturn = beforeReturn;
    }

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
        _beforeReturn?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        ManagedSetupRecoveryFact result = _facts.Count > 1 ? _facts.Dequeue() : _facts.Peek();
        return ValueTask.FromResult(result);
    }
}

internal sealed class RecordingRecoveryLifetimeProbe : IManagedProcessLifetimeProbe
{
    private readonly Action<ManagedProcessLifetimeKind>? _beforeReturn;

    internal RecordingRecoveryLifetimeProbe(
        Action<ManagedProcessLifetimeKind>? beforeReturn = null)
    {
        _beforeReturn = beforeReturn;
    }

    internal Dictionary<ManagedProcessLifetimeKind, ManagedProcessLifetimeStatus> Statuses { get; } =
        Enum.GetValues<ManagedProcessLifetimeKind>().ToDictionary(
            static kind => kind,
            static _ => ManagedProcessLifetimeStatus.Exited);
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
        _beforeReturn?.Invoke(kind);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Statuses[kind]);
    }
}

internal sealed class RecordingRecoveryEvidenceProbe : IManagedSetupRecoveryEvidenceProbe
{
    private readonly Action? _beforeReturn;
    private readonly Queue<ManagedSetupRecoveryEvidenceObservation> _results;

    internal RecordingRecoveryEvidenceProbe(
        params ManagedSetupRecoveryEvidenceObservation[] results)
        : this(results, beforeReturn: null)
    {
    }

    internal RecordingRecoveryEvidenceProbe(
        ManagedSetupRecoveryEvidenceObservation[] results,
        Action? beforeReturn)
    {
        _results = new(results);
        _beforeReturn = beforeReturn;
    }

    internal int CallCount { get; private set; }

    public ValueTask<ManagedSetupRecoveryEvidenceObservation> ObserveAsync(
        ManagedSetupRecoveryTransaction transaction,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        _beforeReturn?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        ManagedSetupRecoveryEvidenceObservation result =
            _results.Count > 1 ? _results.Dequeue() : _results.Peek();
        return ValueTask.FromResult(result);
    }
}

internal sealed class RecordingRecoveryExecutionPort : IManagedSetupRecoveryExecutionPort
{
    internal int CallCount { get; private set; }
    internal ManagedSetupRecoveryExecutionPortOutcome Outcome { get; set; } =
        ManagedSetupRecoveryExecutionPortOutcome.Completed;
    internal Func<ManagedSetupRecoveryExecutionRequest, ValueTask>? OnExecute { get; set; }
    internal VersionManagerWriteLeaseResult? WriterLease { get; private set; }

    public async ValueTask<ManagedSetupRecoveryExecutionPortOutcome> ExecuteAsync(
        ManagedSetupRecoveryExecutionRequest request,
        VersionManagerWriteLeaseResult writerLease,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        WriterLease = writerLease;
        if (OnExecute is not null)
        {
            await OnExecute(request);
        }
        return Outcome;
    }
}

internal sealed class CallbackDisposable(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;
    public void Dispose()
    {
        Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
