namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Stable outcome from one supervised application start.</summary>
public enum ManagedProcessStartOutcome
{
    /// <summary>The process reported the authenticated expected ready version.</summary>
    Ready,
    /// <summary>The executable could not be started.</summary>
    StartFailed,
    /// <summary>The process exited before ready.</summary>
    ExitedBeforeReady,
    /// <summary>The ready deadline elapsed.</summary>
    ReadyTimeout,
    /// <summary>The inherited one-use ready message was invalid.</summary>
    InvalidReadySignal,
}

/// <summary>Stable process adapter result without command-line handshake material.</summary>
public sealed record ManagedProcessStartResult(
    ManagedProcessStartOutcome Outcome,
    int? ExitCode);

/// <summary>Starts one exact managed version through an inherited one-use ready channel.</summary>
public interface IManagedApplicationProcess
{
    /// <summary>Starts and supervises one exact managed payload.</summary>
    /// <param name="managedRoot">Stable launcher-owned managed root.</param>
    /// <param name="version">Exact verified target version.</param>
    /// <param name="readyDeadline">Bounded ready deadline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The supervised start result.</returns>
    ValueTask<ManagedProcessStartResult> StartUntilReadyAsync(
        string managedRoot,
        ManagedAppVersion version,
        TimeSpan readyDeadline,
        CancellationToken cancellationToken);
}

/// <summary>Stable application-side outcome from consuming the inherited ready context.</summary>
public enum ApplicationReadySignalOutcome
{
    /// <summary>No inherited launcher context was present.</summary>
    NotInherited,
    /// <summary>The exact inherited context accepted the one-use ready write.</summary>
    Reported,
    /// <summary>The inherited context was incomplete, mismatched, or malformed.</summary>
    InvalidInheritedContext,
    /// <summary>The exact inherited channel could not accept the ready write.</summary>
    WriteFailed,
}

/// <summary>Application-side one-use inherited ready-channel writer.</summary>
public interface IApplicationReadySignal
{
    /// <summary>Reports that the expected version reached the usable main-window boundary.</summary>
    /// <param name="version">Running application version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The typed inherited-context and write outcome.</returns>
    ValueTask<ApplicationReadySignalOutcome> ReportReadyAsync(
        ManagedAppVersion version,
        CancellationToken cancellationToken);
}

/// <summary>Hands a drained desktop shutdown back to the stable launcher.</summary>
public interface IStableLauncherHandoff
{
    /// <summary>Starts the stable launcher after pending activation was persisted.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the launcher process started.</returns>
    ValueTask<bool> TryStartLauncherAsync(CancellationToken cancellationToken);
}

/// <summary>Launcher run result category.</summary>
public enum ManagedLauncherOutcome
{
    /// <summary>The selected version reached ready.</summary>
    Ready,
    /// <summary>The pending version failed and last-known-good reached ready.</summary>
    RolledBack,
    /// <summary>Launcher state is missing, malformed, or unavailable.</summary>
    InvalidState,
    /// <summary>No active admitted version is available.</summary>
    NoActiveVersion,
    /// <summary>The selected installed payload is damaged.</summary>
    DamagedVersion,
    /// <summary>The selected version failed and no valid rollback completed.</summary>
    StartFailed,
    /// <summary>A required durable activation phase could not be committed.</summary>
    StateUnavailable,
    /// <summary>Another application or launcher owns the version-manager transaction.</summary>
    Busy,
}

/// <summary>Stable launcher outcome with selected and optional failed versions.</summary>
public sealed record ManagedLauncherResult(
    ManagedLauncherOutcome Outcome,
    ManagedAppVersion? RunningVersion,
    ManagedAppVersion? FailedVersion);

/// <summary>Application-owned launcher workflow for verification, ready commit, and one rollback.</summary>
public sealed class ManagedActivationCoordinator
{
    /// <summary>The bounded default main-window ready deadline.</summary>
    public static readonly TimeSpan DefaultReadyDeadline = TimeSpan.FromSeconds(20);
    /// <summary>Bounded wait for another process to release the exact writer lease.</summary>
    public static readonly TimeSpan DefaultWriterLeaseTimeout = TimeSpan.FromSeconds(5);

    private readonly string _managedRoot;
    private readonly IManagedApplicationProcess _process;
    private readonly IManagedVersionRepository _repository;
    private readonly TimeSpan _readyDeadline;
    private readonly IVersionManagerStateStore _stateStore;

    /// <summary>Creates the stable launcher use case.</summary>
    /// <param name="managedRoot">Stable launcher-owned root.</param>
    /// <param name="stateStore">Atomic launcher state.</param>
    /// <param name="repository">Installed payload verifier.</param>
    /// <param name="process">Inherited ready-channel process adapter.</param>
    /// <param name="readyDeadline">Optional deterministic deadline.</param>
    public ManagedActivationCoordinator(
        string managedRoot,
        IVersionManagerStateStore stateStore,
        IManagedVersionRepository repository,
        IManagedApplicationProcess process,
        TimeSpan? readyDeadline = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        _managedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(managedRoot));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _readyDeadline = readyDeadline ?? DefaultReadyDeadline;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_readyDeadline, TimeSpan.Zero);
    }

    /// <summary>Runs one stable launcher selection and at most one automatic rollback.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stable launcher result.</returns>
    public async ValueTask<ManagedLauncherResult> RunAsync(CancellationToken cancellationToken)
    {
        using VersionManagerWriteLeaseResult lease = await _stateStore.TryAcquireWriteLeaseAsync(
            DefaultWriterLeaseTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            return new(
                lease.Issue == VersionManagerWriteLeaseIssue.Busy
                    ? ManagedLauncherOutcome.Busy
                    : ManagedLauncherOutcome.StateUnavailable,
                null,
                null);
        }
        VersionManagerStateLoadResult loaded = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            return new(ManagedLauncherOutcome.InvalidState, null, null);
        }
        VersionManagerState state = loaded.State!;
        if (!state.IsBoundToManagedRoot(_managedRoot))
        {
            return new(ManagedLauncherOutcome.InvalidState, null, null);
        }
        PendingVersionActivation? pending = state.PendingActivation;
        if (pending?.Phase == VersionActivationPhase.RollbackLaunchRecorded)
        {
            return await LaunchRecordedRollbackAsync(state, cancellationToken).ConfigureAwait(false);
        }
        if (pending?.Phase == VersionActivationPhase.CandidateLaunchRecorded)
        {
            return await RecordAndLaunchRollbackAsync(
                state,
                pending.CandidateVersion,
                cancellationToken).ConfigureAwait(false);
        }

        ManagedAppVersion? target = pending?.CandidateVersion ?? state.ActiveVersion;
        if (target is null)
        {
            return new(ManagedLauncherOutcome.NoActiveVersion, null, null);
        }

        if (!await IsHealthyAsync(state, target.Value, cancellationToken).ConfigureAwait(false))
        {
            return pending?.CandidateVersion == target
                ? await RecordAndLaunchRollbackAsync(state, target.Value, cancellationToken).ConfigureAwait(false)
                : new(ManagedLauncherOutcome.DamagedVersion, null, target);
        }

        if (pending?.CandidateVersion == target)
        {
            state = VersionActivationPolicy.RecordCandidateLaunch(state);
            if (!await TrySaveAsync(state, cancellationToken).ConfigureAwait(false))
            {
                return new(ManagedLauncherOutcome.StateUnavailable, null, target);
            }
        }

        ManagedProcessStartResult start = await _process.StartUntilReadyAsync(
            _managedRoot,
            target.Value,
            _readyDeadline,
            cancellationToken).ConfigureAwait(false);
        if (start.Outcome == ManagedProcessStartOutcome.Ready)
        {
            if (state.PendingActivation?.CandidateVersion == target)
            {
                state = VersionActivationPolicy.CommitReady(state, target.Value);
                if (!await TrySaveAsync(state, cancellationToken).ConfigureAwait(false))
                {
                    return new(ManagedLauncherOutcome.StateUnavailable, target, null);
                }
            }
            return new(ManagedLauncherOutcome.Ready, target, null);
        }

        return state.PendingActivation?.CandidateVersion == target
            ? await RecordAndLaunchRollbackAsync(state, target.Value, cancellationToken).ConfigureAwait(false)
            : new(ManagedLauncherOutcome.StartFailed, null, target);
    }

    private async ValueTask<ManagedLauncherResult> RecordAndLaunchRollbackAsync(
        VersionManagerState state,
        ManagedAppVersion failedVersion,
        CancellationToken cancellationToken)
    {
        ActivationRecoveryDecision recovery = VersionActivationPolicy.RecordRollbackLaunch(state, failedVersion);
        return await TrySaveAsync(recovery.State, cancellationToken).ConfigureAwait(false)
            ? await LaunchRecordedRollbackAsync(recovery.State, cancellationToken).ConfigureAwait(false)
            : new(ManagedLauncherOutcome.StateUnavailable, null, failedVersion);
    }

    private async ValueTask<ManagedLauncherResult> LaunchRecordedRollbackAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        PendingVersionActivation pending = state.PendingActivation is
        { Phase: VersionActivationPhase.RollbackLaunchRecorded } value
                ? value
                : throw new InvalidOperationException("Rollback launch was not durably recorded.");
        ManagedAppVersion failedVersion = pending.CandidateVersion;
        ManagedAppVersion? rollback = pending.PreviousLastKnownGoodVersion;
        if (rollback is null || rollback == failedVersion ||
            !await IsHealthyAsync(state, rollback.Value, cancellationToken).ConfigureAwait(false))
        {
            ActivationRecoveryDecision unavailableRollback = VersionActivationPolicy.FailActivation(
                state,
                failedVersion);
            return await TrySaveAsync(unavailableRollback.State, cancellationToken).ConfigureAwait(false)
                ? new(ManagedLauncherOutcome.StartFailed, null, failedVersion)
                : new(ManagedLauncherOutcome.StateUnavailable, null, failedVersion);
        }

        ManagedProcessStartResult fallback = await _process.StartUntilReadyAsync(
            _managedRoot,
            rollback.Value,
            _readyDeadline,
            cancellationToken).ConfigureAwait(false);
        if (fallback.Outcome == ManagedProcessStartOutcome.Ready)
        {
            VersionManagerState committed = VersionActivationPolicy.CommitRollback(state, rollback.Value);
            return await TrySaveAsync(committed, cancellationToken).ConfigureAwait(false)
                ? new(ManagedLauncherOutcome.RolledBack, rollback, failedVersion)
                : new(ManagedLauncherOutcome.StateUnavailable, rollback, failedVersion);
        }

        ActivationRecoveryDecision terminal = VersionActivationPolicy.FailActivation(state, failedVersion);
        return await TrySaveAsync(terminal.State, cancellationToken).ConfigureAwait(false)
            ? new(ManagedLauncherOutcome.StartFailed, null, failedVersion)
            : new(ManagedLauncherOutcome.StateUnavailable, null, failedVersion);
    }

    private async ValueTask<bool> TrySaveAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        VersionManagerStateSaveResult result = await _stateStore.TrySaveAsync(
            state,
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess;
    }

    private async ValueTask<bool> IsHealthyAsync(
        VersionManagerState state,
        ManagedAppVersion version,
        CancellationToken cancellationToken)
    {
        ManagedVersionInventory inventory = await _repository.InventoryAsync(
            _managedRoot,
            state.Admissions,
            state.ActiveVersion,
            state.LastKnownGoodVersion,
            failedActivationVersion: null,
            cancellationToken).ConfigureAwait(false);
        return inventory.Find(version)?.Integrity == ManagedVersionIntegrity.Healthy;
    }
}
