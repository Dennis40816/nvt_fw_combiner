namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>The only actions selected by the managed Setup recovery policy.</summary>
public enum ManagedSetupRecoveryAction
{
    /// <summary>Delete only the exact incomplete transaction state and residue.</summary>
    RemoveIncompleteInstallation,
    /// <summary>Preserve the committed installation and remove exact Setup residue.</summary>
    ConvergeReady,
}

/// <summary>Stable issue from exact candidate, inventory, and launcher-state observation.</summary>
public enum ManagedSetupRecoveryEvidenceIssue
{
    /// <summary>All evidence and its deterministic restart-prefix proof are exact.</summary>
    None,
    /// <summary>State or inventory evidence could not be observed completely.</summary>
    StateUnavailable,
    /// <summary>The exact evidence could not be opened by the current user.</summary>
    PermissionDenied,
    /// <summary>Candidate or inventory authority changed since the marker was written.</summary>
    SourceChanged,
    /// <summary>Evidence is malformed, foreign, unsafe, or lacks complete prefix proof.</summary>
    Invalid,
}

/// <summary>
/// Opaque, immutable exact-object and deterministic-prefix evidence supplied by Infrastructure.
/// Every concrete token must be sealed, deeply immutable, and value-equal across equivalent
/// observations; reference identity, mutable collections, and replayable lease claims are forbidden.
/// </summary>
public abstract record ManagedSetupRecoveryExecutionToken;

/// <summary>
/// Action-safe observation whose raw Application and Launcher snapshots remain internal.
/// </summary>
public sealed class ManagedSetupRecoveryEvidenceObservation
{
    /// <summary>Creates one incomplete observation without destructive authority.</summary>
    public ManagedSetupRecoveryEvidenceObservation(ManagedSetupRecoveryEvidenceIssue issue)
    {
        if (issue == ManagedSetupRecoveryEvidenceIssue.None)
        {
            throw new ArgumentException(
                "Exact recovery evidence requires all typed snapshots and its opaque token.",
                nameof(issue));
        }
        Issue = issue;
    }

    /// <summary>Creates one exact observation for Application-owned policy classification.</summary>
    internal ManagedSetupRecoveryEvidenceObservation(
        ManagedVersionAdmission admission,
        ManagedLauncherIdentity? installedLauncher,
        LauncherBootstrapStateLoadResult launcherState,
        ManagedSetupRecoveryExecutionToken executionToken)
    {
        Admission = admission ?? throw new ArgumentNullException(nameof(admission));
        InstalledLauncher = installedLauncher;
        LauncherState = launcherState ?? throw new ArgumentNullException(nameof(launcherState));
        ExecutionToken = executionToken ?? throw new ArgumentNullException(nameof(executionToken));
        Issue = ManagedSetupRecoveryEvidenceIssue.None;
    }

    /// <summary>Gets the action-safe observation category.</summary>
    public ManagedSetupRecoveryEvidenceIssue Issue { get; }

    internal ManagedVersionAdmission? Admission { get; }
    internal ManagedLauncherIdentity? InstalledLauncher { get; }
    internal LauncherBootstrapStateLoadResult? LauncherState { get; }
    internal ManagedSetupRecoveryExecutionToken? ExecutionToken { get; }
}

/// <summary>
/// Exact recovery evidence probe. It verifies mechanics but never classifies a state pair or action.
/// </summary>
public interface IManagedSetupRecoveryEvidenceProbe
{
    /// <summary>Observes exact candidate, root inventory, Launcher state, and restart-prefix evidence.</summary>
    ValueTask<ManagedSetupRecoveryEvidenceObservation> ObserveAsync(
        ManagedSetupRecoveryTransaction transaction,
        CancellationToken cancellationToken);
}

/// <summary>
/// Canonical Application state store used for both recovery reads and the one writer lease.
/// </summary>
public interface IManagedSetupRecoveryStateStore
    : IManagedSetupRecoveryStateReader, IVersionManagerStateStore;

/// <summary>Immutable Application-owned action plan produced only by actionable diagnosis.</summary>
public sealed class ManagedSetupRecoveryPlan
{
    internal ManagedSetupRecoveryPlan(
        string managedRoot,
        ManagedSetupRecoveryTransaction transaction,
        ManagedSetupRecoveryAction action,
        VersionManagerStateLoadResult applicationState,
        ManagedSetupRecoveryEvidenceObservation evidence)
    {
        ManagedRoot = managedRoot;
        Transaction = transaction;
        Action = action;
        ApplicationStateWasMissing = applicationState.Issue == VersionManagerStateLoadIssue.Missing;
        ApplicationStateToken = applicationState.State?.CreateDurableSnapshotToken();
        Evidence = evidence;
    }

    /// <summary>Gets the one action selected from the closed Application policy.</summary>
    public ManagedSetupRecoveryAction Action { get; }

    internal string ManagedRoot { get; }
    internal ManagedSetupRecoveryTransaction Transaction { get; }
    internal bool ApplicationStateWasMissing { get; }
    internal VersionManagerState.DurableSnapshotToken? ApplicationStateToken { get; }
    internal ManagedSetupRecoveryEvidenceObservation Evidence { get; }
}

/// <summary>Closed terminal outcomes for one explicit recovery execution attempt.</summary>
public enum ManagedSetupRecoveryExecutionOutcome
{
    /// <summary>The selected sequence completed and removed the exact marker last.</summary>
    Completed,
    /// <summary>Rollback was invoked without selecting Remove incomplete installation.</summary>
    ConfirmationRequired,
    /// <summary>Another process owns the canonical writer lease.</summary>
    Busy,
    /// <summary>One managed process lifetime role remains active.</summary>
    LifetimeActive,
    /// <summary>A complete general health observation was unavailable.</summary>
    HealthUnavailable,
    /// <summary>State, root, or marker I/O could not produce complete stable evidence.</summary>
    StateUnavailable,
    /// <summary>An exact authorized object could not be opened for required access.</summary>
    PermissionDenied,
    /// <summary>Candidate or inventory authority no longer matches the plan.</summary>
    SourceChanged,
    /// <summary>Facts changed or a safe deterministic restart prefix remains.</summary>
    RecoveryRequired,
    /// <summary>Foreign, malformed, unsafe, or unprovable residue requires human handling.</summary>
    ManualInterventionRequired,
    /// <summary>Cancellation occurred before the first irreversible deletion.</summary>
    Cancelled,
}

/// <summary>Terminal typed result from one explicit execution attempt.</summary>
public sealed record ManagedSetupRecoveryExecutionResult(
    ManagedSetupRecoveryExecutionOutcome Outcome);

/// <summary>Mechanics-only outcome from the exact Infrastructure executor.</summary>
public enum ManagedSetupRecoveryExecutionPortOutcome
{
    /// <summary>The selected sequence completed and the marker was removed last.</summary>
    Completed,
    /// <summary>General health evidence became unavailable.</summary>
    HealthUnavailable,
    /// <summary>State or exact-object I/O became unavailable.</summary>
    StateUnavailable,
    /// <summary>An exact object could not be opened for required access.</summary>
    PermissionDenied,
    /// <summary>Candidate or inventory authority changed.</summary>
    SourceChanged,
    /// <summary>A safe deterministic restart prefix remains.</summary>
    RecoveryRequired,
    /// <summary>Foreign, unsafe, replacement, hole, or unprovable residue was found.</summary>
    ManualInterventionRequired,
    /// <summary>Cancellation occurred before the first irreversible deletion.</summary>
    Cancelled,
}

/// <summary>Selected action and current exact evidence passed to the mechanics-only executor.</summary>
public sealed class ManagedSetupRecoveryExecutionRequest
{
    internal ManagedSetupRecoveryExecutionRequest(
        ManagedSetupRecoveryAction action,
        ManagedSetupRecoveryExecutionToken executionToken)
    {
        Action = action;
        ExecutionToken = executionToken;
    }

    /// <summary>Gets the already-selected Application action.</summary>
    public ManagedSetupRecoveryAction Action { get; }

    /// <summary>Gets the current opaque exact-object and restart-prefix evidence.</summary>
    public ManagedSetupRecoveryExecutionToken ExecutionToken { get; }
}

/// <summary>Exact mutation port that neither acquires a writer lease nor selects recovery policy.</summary>
public interface IManagedSetupRecoveryExecutionPort
{
    /// <summary>
    /// Executes the supplied sequence while Application retains the supplied live canonical lease.
    /// The adapter must validate that lease against its exact state path and must not dispose it.
    /// Cancellation before the first irreversible delete returns Cancelled; after the first delete
    /// the adapter must retain the marker and return RecoveryRequired instead of throwing cancellation.
    /// </summary>
    ValueTask<ManagedSetupRecoveryExecutionPortOutcome> ExecuteAsync(
        ManagedSetupRecoveryExecutionRequest request,
        VersionManagerWriteLeaseResult writerLease,
        CancellationToken cancellationToken);
}

/// <summary>
/// Owns explicit recovery execution, canonical writer custody, and terminal execution outcomes.
/// </summary>
public sealed class ManagedSetupRecoveryExecutionCoordinator
{
    private readonly IManagedSetupRecoveryStateStore _stateStore;
    private readonly IManagedInstallationRootProbe _rootProbe;
    private readonly IManagedSetupRecoveryProbe _markerProbe;
    private readonly IManagedProcessLifetimeProbe _lifetimeProbe;
    private readonly IManagedSetupRecoveryEvidenceProbe _evidenceProbe;
    private readonly IManagedSetupRecoveryExecutionPort _executionPort;
    private readonly string _statePathIdentity;

    /// <summary>Creates the Application execution owner over the canonical state store.</summary>
    public ManagedSetupRecoveryExecutionCoordinator(
        IManagedSetupRecoveryStateStore stateStore,
        IManagedInstallationRootProbe rootProbe,
        IManagedSetupRecoveryProbe markerProbe,
        IManagedProcessLifetimeProbe lifetimeProbe,
        IManagedSetupRecoveryEvidenceProbe evidenceProbe,
        IManagedSetupRecoveryExecutionPort executionPort)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        string statePathIdentity = stateStore.StatePathIdentity;
        if (string.IsNullOrWhiteSpace(statePathIdentity) ||
            !Path.IsPathFullyQualified(statePathIdentity))
        {
            throw new ArgumentException(
                "Recovery state store identity must be an absolute path.",
                nameof(stateStore));
        }
        _statePathIdentity = Path.GetFullPath(statePathIdentity);
        _rootProbe = rootProbe ?? throw new ArgumentNullException(nameof(rootProbe));
        _markerProbe = markerProbe ?? throw new ArgumentNullException(nameof(markerProbe));
        _lifetimeProbe = lifetimeProbe ?? throw new ArgumentNullException(nameof(lifetimeProbe));
        _evidenceProbe = evidenceProbe ?? throw new ArgumentNullException(nameof(evidenceProbe));
        _executionPort = executionPort ?? throw new ArgumentNullException(nameof(executionPort));
    }

    /// <summary>Executes one immutable plan after its action is explicitly selected.</summary>
    public async ValueTask<ManagedSetupRecoveryExecutionResult> ExecuteAsync(
        ManagedSetupRecoveryPlan plan,
        ManagedSetupRecoveryAction? confirmedAction,
        TimeSpan writerLeaseTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(writerLeaseTimeout, TimeSpan.Zero);
        if (confirmedAction is null)
        {
            return Result(plan.Action == ManagedSetupRecoveryAction.RemoveIncompleteInstallation
                ? ManagedSetupRecoveryExecutionOutcome.ConfirmationRequired
                : ManagedSetupRecoveryExecutionOutcome.RecoveryRequired);
        }
        if (confirmedAction != plan.Action)
        {
            return Result(ManagedSetupRecoveryExecutionOutcome.RecoveryRequired);
        }

        bool handedToExecutor = false;
        try
        {
            using VersionManagerWriteLeaseResult lease = await _stateStore.TryAcquireWriteLeaseAsync(
                writerLeaseTimeout,
                cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                return Result(lease.Issue == VersionManagerWriteLeaseIssue.Busy
                    ? ManagedSetupRecoveryExecutionOutcome.Busy
                    : ManagedSetupRecoveryExecutionOutcome.StateUnavailable);
            }

            ManagedSetupRecoveryExecutionOutcome? lifetime = await ObserveExecutionLifetimesAsync(
                cancellationToken).ConfigureAwait(false);
            if (lifetime is not null)
            {
                return Result(lifetime.Value);
            }

            VersionManagerStateLoadResult state = await _stateStore.LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            ManagedInstallationRootObservation root = await _rootProbe.ObserveAsync(
                plan.ManagedRoot,
                cancellationToken).ConfigureAwait(false);
            ManagedSetupRecoveryFact marker = await _markerProbe.ObserveAsync(
                plan.ManagedRoot,
                _statePathIdentity,
                cancellationToken).ConfigureAwait(false);
            ManagedSetupRecoveryExecutionOutcome? observationIssue =
                ClassifyExecutionObservationIssue(state, root, marker);
            if (observationIssue is not null)
            {
                return Result(observationIssue.Value);
            }
            if (marker.Transaction is not { } transaction)
            {
                return Result(ManagedSetupRecoveryExecutionOutcome.RecoveryRequired);
            }

            ManagedSetupRecoveryEvidenceObservation evidence = await _evidenceProbe.ObserveAsync(
                transaction,
                cancellationToken).ConfigureAwait(false);
            ManagedSetupRecoveryExecutionOutcome? evidenceIssue =
                ClassifyExecutionEvidenceIssue(evidence);
            if (evidenceIssue is not null)
            {
                return Result(evidenceIssue.Value);
            }

            ManagedSetupRecoveryAction? currentAction = ManagedSetupRecoveryPolicy.SelectAction(
                state,
                transaction,
                evidence,
                plan.ManagedRoot);
            if (currentAction is null ||
                currentAction != plan.Action ||
                !ManagedSetupRecoveryPolicy.PlanMatches(plan, state, transaction, evidence))
            {
                return Result(ManagedSetupRecoveryExecutionOutcome.RecoveryRequired);
            }

            var request = new ManagedSetupRecoveryExecutionRequest(
                currentAction.Value,
                evidence.ExecutionToken!);
            handedToExecutor = true;
            ManagedSetupRecoveryExecutionPortOutcome executed = await _executionPort.ExecuteAsync(
                request,
                lease,
                cancellationToken).ConfigureAwait(false);
            return Result(Map(executed));
        }
        catch (OperationCanceledException) when (!handedToExecutor)
        {
            return Result(ManagedSetupRecoveryExecutionOutcome.Cancelled);
        }
        catch (OperationCanceledException)
        {
            return Result(ManagedSetupRecoveryExecutionOutcome.RecoveryRequired);
        }
    }

    private async ValueTask<ManagedSetupRecoveryExecutionOutcome?> ObserveExecutionLifetimesAsync(
        CancellationToken cancellationToken)
    {
        var lifetimes = new ManagedProcessLifetimeStatus[ManagedSetupRecoveryPolicy.LifetimeKinds.Length];
        for (int index = 0; index < ManagedSetupRecoveryPolicy.LifetimeKinds.Length; index++)
        {
            lifetimes[index] = await _lifetimeProbe.ObserveAsync(
                _statePathIdentity,
                ManagedSetupRecoveryPolicy.LifetimeKinds[index],
                cancellationToken).ConfigureAwait(false);
        }
        return lifetimes.Contains(ManagedProcessLifetimeStatus.Active)
            ? ManagedSetupRecoveryExecutionOutcome.LifetimeActive
            : lifetimes.Any(static status => status != ManagedProcessLifetimeStatus.Exited)
                ? ManagedSetupRecoveryExecutionOutcome.HealthUnavailable
                : null;
    }

    private static ManagedSetupRecoveryExecutionOutcome? ClassifyExecutionObservationIssue(
        VersionManagerStateLoadResult state,
        ManagedInstallationRootObservation root,
        ManagedSetupRecoveryFact marker)
    {
        return (state.Issue, root.Status, marker.Kind) switch
        {
            (VersionManagerStateLoadIssue.Unavailable, _, _) or
            (_, ManagedInstallationRootStatus.Unavailable, _) or
            (_, _, ManagedSetupRecoveryFactKind.Unavailable) =>
                ManagedSetupRecoveryExecutionOutcome.StateUnavailable,
            (_, ManagedInstallationRootStatus.PermissionDenied, _) or
            (_, _, ManagedSetupRecoveryFactKind.AccessDenied) =>
                ManagedSetupRecoveryExecutionOutcome.PermissionDenied,
            (_, _, ManagedSetupRecoveryFactKind.Changed) =>
                ManagedSetupRecoveryExecutionOutcome.RecoveryRequired,
            (VersionManagerStateLoadIssue.Invalid or
                VersionManagerStateLoadIssue.ManagedRootMismatch, _, _) or
            (_, ManagedInstallationRootStatus.InvalidDestination, _) or
            (_, _, ManagedSetupRecoveryFactKind.Malformed or
                ManagedSetupRecoveryFactKind.IdentityMismatch) =>
                ManagedSetupRecoveryExecutionOutcome.ManualInterventionRequired,
            (_, ManagedInstallationRootStatus.Residue, ManagedSetupRecoveryFactKind.Exact) => null,
            _ => ManagedSetupRecoveryExecutionOutcome.RecoveryRequired,
        };
    }

    private static ManagedSetupRecoveryExecutionOutcome? ClassifyExecutionEvidenceIssue(
        ManagedSetupRecoveryEvidenceObservation evidence)
    {
        return evidence.Issue switch
        {
            ManagedSetupRecoveryEvidenceIssue.None when
                evidence.LauncherState?.Issue == LauncherBootstrapStateLoadIssue.Unavailable =>
                    ManagedSetupRecoveryExecutionOutcome.StateUnavailable,
            ManagedSetupRecoveryEvidenceIssue.None when
                evidence.LauncherState?.Issue == LauncherBootstrapStateLoadIssue.Invalid =>
                    ManagedSetupRecoveryExecutionOutcome.ManualInterventionRequired,
            ManagedSetupRecoveryEvidenceIssue.None => null,
            ManagedSetupRecoveryEvidenceIssue.StateUnavailable =>
                ManagedSetupRecoveryExecutionOutcome.StateUnavailable,
            ManagedSetupRecoveryEvidenceIssue.PermissionDenied =>
                ManagedSetupRecoveryExecutionOutcome.PermissionDenied,
            ManagedSetupRecoveryEvidenceIssue.SourceChanged =>
                ManagedSetupRecoveryExecutionOutcome.SourceChanged,
            ManagedSetupRecoveryEvidenceIssue.Invalid =>
                ManagedSetupRecoveryExecutionOutcome.ManualInterventionRequired,
            _ => throw new InvalidOperationException(
                "Recovery evidence returned an undefined issue."),
        };
    }

    private static ManagedSetupRecoveryExecutionOutcome Map(
        ManagedSetupRecoveryExecutionPortOutcome outcome)
    {
        return outcome switch
        {
            ManagedSetupRecoveryExecutionPortOutcome.Completed =>
                ManagedSetupRecoveryExecutionOutcome.Completed,
            ManagedSetupRecoveryExecutionPortOutcome.HealthUnavailable =>
                ManagedSetupRecoveryExecutionOutcome.HealthUnavailable,
            ManagedSetupRecoveryExecutionPortOutcome.StateUnavailable =>
                ManagedSetupRecoveryExecutionOutcome.StateUnavailable,
            ManagedSetupRecoveryExecutionPortOutcome.PermissionDenied =>
                ManagedSetupRecoveryExecutionOutcome.PermissionDenied,
            ManagedSetupRecoveryExecutionPortOutcome.SourceChanged =>
                ManagedSetupRecoveryExecutionOutcome.SourceChanged,
            ManagedSetupRecoveryExecutionPortOutcome.RecoveryRequired =>
                ManagedSetupRecoveryExecutionOutcome.RecoveryRequired,
            ManagedSetupRecoveryExecutionPortOutcome.ManualInterventionRequired =>
                ManagedSetupRecoveryExecutionOutcome.ManualInterventionRequired,
            ManagedSetupRecoveryExecutionPortOutcome.Cancelled =>
                ManagedSetupRecoveryExecutionOutcome.Cancelled,
            _ => throw new InvalidOperationException(
                "Recovery executor returned an undefined outcome."),
        };
    }

    private static ManagedSetupRecoveryExecutionResult Result(
        ManagedSetupRecoveryExecutionOutcome outcome)
    {
        return new(outcome);
    }
}

/// <summary>Single pure owner of recovery lifetime ordering, state-pair policy, and plan matching.</summary>
internal static class ManagedSetupRecoveryPolicy
{
    internal static readonly ManagedProcessLifetimeKind[] LifetimeKinds =
    [
        ManagedProcessLifetimeKind.Bootstrap,
        ManagedProcessLifetimeKind.Application,
        ManagedProcessLifetimeKind.Launcher,
    ];

    internal static ManagedSetupRecoveryAction? SelectAction(
        VersionManagerStateLoadResult applicationState,
        ManagedSetupRecoveryTransaction transaction,
        ManagedSetupRecoveryEvidenceObservation evidence,
        string managedRoot)
    {
        if (evidence.Issue != ManagedSetupRecoveryEvidenceIssue.None ||
            evidence.Admission is not { } admission ||
            evidence.LauncherState is not { } launcherLoad ||
            evidence.ExecutionToken is null ||
            !CandidateMatches(transaction, admission))
        {
            return null;
        }

        bool appMissing = applicationState.State is null &&
            applicationState.Issue == VersionManagerStateLoadIssue.Missing;
        bool launcherMissing = launcherLoad.State is null &&
            launcherLoad.Issue == LauncherBootstrapStateLoadIssue.Missing;
        if (appMissing && launcherMissing)
        {
            return ManagedSetupRecoveryAction.RemoveIncompleteInstallation;
        }
        if (evidence.InstalledLauncher is not { } launcher ||
            !launcher.MatchesOwner(admission))
        {
            return null;
        }
        if (!applicationState.IsSuccess ||
            transaction.Phase != ManagedSetupRecoveryPhase.BootstrapLaunchRecorded ||
            !ManagedVersionSeedPolicy.IsCanonicalBoundFirstRunState(
                applicationState.State!,
                managedRoot,
                admission) ||
            (!launcherMissing &&
                (!launcherLoad.IsSuccess ||
                    !launcherLoad.State!.IsBoundToManagedRoot(managedRoot))))
        {
            return null;
        }
        if (launcherMissing)
        {
            return ManagedSetupRecoveryAction.RemoveIncompleteInstallation;
        }

        LauncherBootstrapState launcherState = launcherLoad.State!;
        bool preReadyPending = launcherState is
        {
            Active: null,
            LastKnownGood: null,
            Failed: null,
            Pending:
            {
                Candidate: var pendingCandidate,
                PreviousActive: null,
                PreviousLastKnownGood: null,
                Phase: LauncherActivationPhase.Requested or
                    LauncherActivationPhase.CandidateLaunchRecorded,
            },
        } && pendingCandidate == launcher;
        bool failedFirstCandidate = launcherState is
        {
            Active: null,
            LastKnownGood: null,
            Pending: null,
            Failed: var failed,
        } && failed == launcher;
        if (preReadyPending || failedFirstCandidate)
        {
            return ManagedSetupRecoveryAction.RemoveIncompleteInstallation;
        }
        bool ready = launcherState is
        {
            Active: var active,
            LastKnownGood: var lastKnownGood,
            Pending: null,
            Failed: null,
        } && active == launcher && lastKnownGood == launcher;
        return ready ? ManagedSetupRecoveryAction.ConvergeReady : null;
    }

    internal static bool PlanMatches(
        ManagedSetupRecoveryPlan plan,
        VersionManagerStateLoadResult state,
        ManagedSetupRecoveryTransaction transaction,
        ManagedSetupRecoveryEvidenceObservation evidence)
    {
        bool stateMissing = state.State is null && state.Issue == VersionManagerStateLoadIssue.Missing;
        bool stateMatches = plan.ApplicationStateWasMissing
            ? stateMissing
            : state.IsSuccess && plan.ApplicationStateToken is { } expected &&
                expected.Matches(state.State!.CreateDurableSnapshotToken());
        return stateMatches &&
            TransactionMatches(plan.Transaction, transaction) &&
            Equals(plan.Evidence.ExecutionToken, evidence.ExecutionToken);
    }

    private static bool CandidateMatches(
        ManagedSetupRecoveryTransaction transaction,
        ManagedVersionAdmission admission)
    {
        return ManagedAppVersion.TryParse(transaction.Candidate.Version, out ManagedAppVersion version) &&
            admission.Version == version &&
            string.Equals(
                admission.AdmissionIdentity,
                transaction.Candidate.EntryIdentity,
                StringComparison.Ordinal) &&
            string.Equals(
                admission.ReleaseManifestSha256,
                transaction.Candidate.ReleaseManifestSha256,
                StringComparison.Ordinal);
    }

    private static bool TransactionMatches(
        ManagedSetupRecoveryTransaction expected,
        ManagedSetupRecoveryTransaction actual)
    {
        return string.Equals(expected.TransactionId, actual.TransactionId, StringComparison.Ordinal) &&
            string.Equals(
                expected.ManagedRootIdentity,
                actual.ManagedRootIdentity,
                StringComparison.Ordinal) &&
            string.Equals(
                expected.StatePathIdentity,
                actual.StatePathIdentity,
                StringComparison.Ordinal) &&
            expected.Phase == actual.Phase &&
            expected.OwnedPaths.SequenceEqual(actual.OwnedPaths, StringComparer.Ordinal) &&
            expected.Payload == actual.Payload &&
            expected.Candidate == actual.Candidate;
    }
}
