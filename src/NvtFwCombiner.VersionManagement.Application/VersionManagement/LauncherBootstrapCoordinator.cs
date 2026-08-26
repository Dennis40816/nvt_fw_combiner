namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Single Application owner for Bootstrap launcher selection, readiness, and exact rollback.</summary>
internal sealed class LauncherBootstrapCoordinator
{
    public static readonly TimeSpan DefaultReadyDeadline = TimeSpan.FromSeconds(20);
    private readonly IVersionManagerStateStore _appStateStore;
    private readonly ILauncherBootstrapStateStore _launcherStateStore;
    private readonly string _managedRoot;
    private readonly IManagedLauncherProcess _process;
    private readonly TimeSpan _readyDeadline;
    private readonly IInstalledLauncherRepository _repository;
    private readonly string _statePath;

    public LauncherBootstrapCoordinator(
        string managedRoot,
        string statePath,
        IVersionManagerStateStore appStateStore,
        ILauncherBootstrapStateStore launcherStateStore,
        IInstalledLauncherRepository repository,
        IManagedLauncherProcess process,
        TimeSpan? readyDeadline = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        _managedRoot = ManagedRootPathIdentity.Normalize(managedRoot);
        _statePath = Path.GetFullPath(statePath);
        _appStateStore = appStateStore ?? throw new ArgumentNullException(nameof(appStateStore));
        _launcherStateStore = launcherStateStore ?? throw new ArgumentNullException(nameof(launcherStateStore));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _readyDeadline = readyDeadline ?? DefaultReadyDeadline;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_readyDeadline, TimeSpan.Zero);
    }

    public async ValueTask<LauncherBootstrapResult> RunAsync(CancellationToken cancellationToken)
    {
        VersionManagerWriteLeaseResult lease = await _appStateStore.TryAcquireWriteLeaseAsync(
            ManagedActivationCoordinator.DefaultWriterLeaseTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            lease.Dispose();
            return Result(lease.Issue == VersionManagerWriteLeaseIssue.Busy
                ? LauncherBootstrapOutcome.Busy
                : LauncherBootstrapOutcome.StateUnavailable);
        }

        VersionManagerState? appState;
        LauncherBootstrapState? launcherState;
        try
        {
            (LauncherBootstrapOutcome issue, appState) = await LoadAppStateAsync(cancellationToken).ConfigureAwait(false);
            if (issue != LauncherBootstrapOutcome.Ready)
            {
                return Result(issue);
            }
            if (appState!.PendingMutation is not null)
            {
                return Result(LauncherBootstrapOutcome.AppMutationPending);
            }
            (issue, launcherState) = await LoadLauncherStateAsync(cancellationToken).ConfigureAwait(false);
            if (issue != LauncherBootstrapOutcome.Ready)
            {
                return Result(issue);
            }

            if (appState.PendingActivation is not null)
            {
                ManagedLauncherIdentity? active = launcherState!.Active;
                if (active is null || !TryFindAdmission(appState, active, out ManagedVersionAdmission? activeAdmission))
                {
                    return Result(LauncherBootstrapOutcome.InvalidState);
                }
                InstalledLauncherResult verified = await _repository.VerifyAsync(
                    _managedRoot,
                    activeAdmission!,
                    cancellationToken).ConfigureAwait(false);
                if (!Matches(verified, active))
                {
                    return Result(MapVerification(verified.Issue), failed: active);
                }
                lease.Dispose();
                return await LaunchExistingAsync(active, cancellationToken).ConfigureAwait(false);
            }

            if (launcherState!.Pending is { Phase: LauncherActivationPhase.RollbackLaunchRecorded })
            {
                lease.Dispose();
                return await LaunchRecordedRollbackAsync(appState, launcherState, cancellationToken)
                    .ConfigureAwait(false);
            }
            if (launcherState.Pending is { Phase: LauncherActivationPhase.CandidateLaunchRecorded })
            {
                LauncherBootstrapState rollbackRecorded = launcherState.RecordRollbackLaunch();
                if (!await SaveAsync(rollbackRecorded, cancellationToken).ConfigureAwait(false))
                {
                    return Result(LauncherBootstrapOutcome.StateUnavailable);
                }
                lease.Dispose();
                return await LaunchRecordedRollbackAsync(appState, rollbackRecorded, cancellationToken)
                    .ConfigureAwait(false);
            }

            ManagedVersionAdmission? desiredAdmission = FindActiveAdmission(appState);
            if (desiredAdmission is null)
            {
                return Result(LauncherBootstrapOutcome.InvalidState);
            }
            InstalledLauncherResult desiredResult = await _repository.VerifyAsync(
                _managedRoot,
                desiredAdmission,
                cancellationToken).ConfigureAwait(false);
            if (!desiredResult.IsVerified)
            {
                return Result(MapVerification(desiredResult.Issue));
            }
            ManagedLauncherIdentity desired = desiredResult.Identity!;
            if (!desired.MatchesOwner(desiredAdmission))
            {
                return Result(LauncherBootstrapOutcome.DamagedLauncher, failed: desired);
            }

            if (launcherState.Pending is { Phase: LauncherActivationPhase.Requested } requested)
            {
                if (requested.Candidate != desired)
                {
                    return Result(LauncherBootstrapOutcome.StateChanged, failed: requested.Candidate);
                }
            }
            else if (launcherState.Active == desired)
            {
                lease.Dispose();
                return await LaunchExistingAsync(desired, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                launcherState = launcherState.Begin(desired);
                if (!await SaveAsync(launcherState, cancellationToken).ConfigureAwait(false))
                {
                    return Result(LauncherBootstrapOutcome.StateUnavailable);
                }
            }

            launcherState = launcherState.RecordCandidateLaunch();
            if (!await SaveAsync(launcherState, cancellationToken).ConfigureAwait(false))
            {
                return Result(LauncherBootstrapOutcome.StateUnavailable);
            }
            lease.Dispose();
            return await LaunchCandidateAsync(desired, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lease.Dispose();
        }
    }

    private async ValueTask<LauncherBootstrapResult> LaunchExistingAsync(
        ManagedLauncherIdentity launcher,
        CancellationToken cancellationToken)
    {
        LauncherProcessStartResult start = await _process.StartUntilReadyAsync(
            _managedRoot,
            _statePath,
            launcher,
            _readyDeadline,
            cancellationToken).ConfigureAwait(false);
        return start.Outcome == LauncherProcessStartOutcome.Ready
            ? new(LauncherBootstrapOutcome.Ready, launcher, null)
            : Result(LauncherBootstrapOutcome.StartFailed, failed: launcher);
    }

    private async ValueTask<LauncherBootstrapResult> LaunchCandidateAsync(
        ManagedLauncherIdentity candidate,
        CancellationToken cancellationToken)
    {
        LauncherProcessStartResult start = await _process.StartUntilReadyAsync(
            _managedRoot,
            _statePath,
            candidate,
            _readyDeadline,
            cancellationToken).ConfigureAwait(false);
        return start.Outcome == LauncherProcessStartOutcome.Ready
            ? await CommitCandidateReadyAsync(candidate, cancellationToken).ConfigureAwait(false)
            : await RecordAndLaunchRollbackAsync(candidate, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<LauncherBootstrapResult> CommitCandidateReadyAsync(
        ManagedLauncherIdentity candidate,
        CancellationToken cancellationToken)
    {
        using VersionManagerWriteLeaseResult lease = await _appStateStore.TryAcquireWriteLeaseAsync(
            ManagedActivationCoordinator.DefaultWriterLeaseTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            return Result(LauncherBootstrapOutcome.StateUnavailable, failed: candidate);
        }
        (LauncherBootstrapOutcome appIssue, VersionManagerState? appState) =
            await LoadAppStateAsync(cancellationToken).ConfigureAwait(false);
        (LauncherBootstrapOutcome launcherIssue, LauncherBootstrapState? launcherState) =
            await LoadLauncherStateAsync(cancellationToken).ConfigureAwait(false);
        if (appIssue != LauncherBootstrapOutcome.Ready || launcherIssue != LauncherBootstrapOutcome.Ready)
        {
            return Result(LauncherBootstrapOutcome.StateUnavailable, failed: candidate);
        }
        if (appState!.PendingActivation is not null || appState.PendingMutation is not null ||
            !TryFindAdmission(appState, candidate, out _) ||
            appState.ActiveVersion != candidate.OwnerAppVersion ||
            launcherState!.Pending is not
            { Phase: LauncherActivationPhase.CandidateLaunchRecorded, Candidate: var pendingCandidate } ||
            pendingCandidate != candidate)
        {
            return Result(LauncherBootstrapOutcome.StateChanged, failed: candidate);
        }
        LauncherBootstrapState committed = launcherState.CommitReady();
        return await SaveAsync(committed, cancellationToken).ConfigureAwait(false)
            ? new(LauncherBootstrapOutcome.Ready, candidate, null)
            : Result(LauncherBootstrapOutcome.StateUnavailable, failed: candidate);
    }

    private async ValueTask<LauncherBootstrapResult> RecordAndLaunchRollbackAsync(
        ManagedLauncherIdentity candidate,
        CancellationToken cancellationToken)
    {
        using VersionManagerWriteLeaseResult lease = await _appStateStore.TryAcquireWriteLeaseAsync(
            ManagedActivationCoordinator.DefaultWriterLeaseTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            return Result(LauncherBootstrapOutcome.StateUnavailable, failed: candidate);
        }
        (LauncherBootstrapOutcome appIssue, VersionManagerState? appState) =
            await LoadAppStateAsync(cancellationToken).ConfigureAwait(false);
        (LauncherBootstrapOutcome launcherIssue, LauncherBootstrapState? launcherState) =
            await LoadLauncherStateAsync(cancellationToken).ConfigureAwait(false);
        if (appIssue != LauncherBootstrapOutcome.Ready || launcherIssue != LauncherBootstrapOutcome.Ready)
        {
            return Result(LauncherBootstrapOutcome.StateUnavailable, failed: candidate);
        }
        if (launcherState!.Pending is not
            { Phase: LauncherActivationPhase.CandidateLaunchRecorded, Candidate: var pendingCandidate } ||
            pendingCandidate != candidate)
        {
            return Result(LauncherBootstrapOutcome.StateChanged, failed: candidate);
        }
        if (launcherState.Pending.PreviousLastKnownGood is null)
        {
            LauncherBootstrapState failed = launcherState.FailCandidate();
            return await SaveAsync(failed, cancellationToken).ConfigureAwait(false)
                ? Result(LauncherBootstrapOutcome.StartFailed, failed: candidate)
                : Result(LauncherBootstrapOutcome.StateUnavailable, failed: candidate);
        }
        LauncherBootstrapState rollback = launcherState.RecordRollbackLaunch();
        if (!await SaveAsync(rollback, cancellationToken).ConfigureAwait(false))
        {
            return Result(LauncherBootstrapOutcome.StateUnavailable, failed: candidate);
        }
        lease.Dispose();
        return await LaunchRecordedRollbackAsync(appState!, rollback, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<LauncherBootstrapResult> LaunchRecordedRollbackAsync(
        VersionManagerState appState,
        LauncherBootstrapState launcherState,
        CancellationToken cancellationToken)
    {
        PendingLauncherActivation pending = launcherState.Pending is
        { Phase: LauncherActivationPhase.RollbackLaunchRecorded } value
                ? value
                : throw new InvalidOperationException("Launcher rollback is not recorded.");
        ManagedLauncherIdentity? rollback = pending.PreviousLastKnownGood;
        if (rollback is null || !TryFindAdmission(appState, rollback, out ManagedVersionAdmission? admission))
        {
            return Result(LauncherBootstrapOutcome.RollbackUnavailable, failed: pending.Candidate);
        }
        InstalledLauncherResult verified = await _repository.VerifyAsync(
            _managedRoot,
            admission!,
            cancellationToken).ConfigureAwait(false);
        if (!Matches(verified, rollback))
        {
            return Result(LauncherBootstrapOutcome.RollbackUnavailable, failed: pending.Candidate);
        }
        LauncherProcessStartResult start = await _process.StartUntilReadyAsync(
            _managedRoot,
            _statePath,
            rollback,
            _readyDeadline,
            cancellationToken).ConfigureAwait(false);
        if (start.Outcome != LauncherProcessStartOutcome.Ready)
        {
            return Result(LauncherBootstrapOutcome.RollbackUnavailable, failed: pending.Candidate);
        }
        using VersionManagerWriteLeaseResult lease = await _appStateStore.TryAcquireWriteLeaseAsync(
            ManagedActivationCoordinator.DefaultWriterLeaseTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            return Result(LauncherBootstrapOutcome.StateUnavailable, failed: pending.Candidate);
        }
        (LauncherBootstrapOutcome issue, LauncherBootstrapState? reloaded) =
            await LoadLauncherStateAsync(cancellationToken).ConfigureAwait(false);
        if (issue != LauncherBootstrapOutcome.Ready || reloaded!.Pending != pending)
        {
            return Result(LauncherBootstrapOutcome.StateChanged, failed: pending.Candidate);
        }
        LauncherBootstrapState committed = reloaded.CommitRollback();
        return await SaveAsync(committed, cancellationToken).ConfigureAwait(false)
            ? new(LauncherBootstrapOutcome.RolledBack, rollback, pending.Candidate)
            : Result(LauncherBootstrapOutcome.StateUnavailable, failed: pending.Candidate);
    }

    private async ValueTask<(LauncherBootstrapOutcome, VersionManagerState?)> LoadAppStateAsync(
        CancellationToken cancellationToken)
    {
        VersionManagerStateLoadResult loaded = await _appStateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!loaded.IsSuccess)
        {
            return (loaded.Issue == VersionManagerStateLoadIssue.Unavailable
                ? LauncherBootstrapOutcome.StateUnavailable
                : LauncherBootstrapOutcome.InvalidState, null);
        }
        return loaded.State!.IsBoundToManagedRoot(_managedRoot)
            ? (LauncherBootstrapOutcome.Ready, loaded.State)
            : (LauncherBootstrapOutcome.ManagedRootMismatch, null);
    }

    private async ValueTask<(LauncherBootstrapOutcome, LauncherBootstrapState?)> LoadLauncherStateAsync(
        CancellationToken cancellationToken)
    {
        LauncherBootstrapStateLoadResult loaded = await _launcherStateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Issue == LauncherBootstrapStateLoadIssue.Missing)
        {
            return (LauncherBootstrapOutcome.Ready,
                LauncherBootstrapState.Create(_managedRoot, null, null, null, null));
        }
        if (!loaded.IsSuccess)
        {
            return (loaded.Issue == LauncherBootstrapStateLoadIssue.Unavailable
                ? LauncherBootstrapOutcome.StateUnavailable
                : LauncherBootstrapOutcome.InvalidState, null);
        }
        return loaded.State!.IsBoundToManagedRoot(_managedRoot)
            ? (LauncherBootstrapOutcome.Ready, loaded.State)
            : (LauncherBootstrapOutcome.ManagedRootMismatch, null);
    }

    private ValueTask<bool> SaveAsync(
        LauncherBootstrapState state,
        CancellationToken cancellationToken)
    {
        return SaveCoreAsync(state, cancellationToken);
    }

    private async ValueTask<bool> SaveCoreAsync(
        LauncherBootstrapState state,
        CancellationToken cancellationToken)
    {
        LauncherBootstrapStateSaveResult saved = await _launcherStateStore.TrySaveAsync(
            state,
            cancellationToken).ConfigureAwait(false);
        return saved.IsSuccess;
    }

    private static ManagedVersionAdmission? FindActiveAdmission(VersionManagerState state)
    {
        return state.ActiveVersion is { } active
            ? state.Admissions.SingleOrDefault(candidate => candidate.Version == active)
            : null;
    }

    private static bool TryFindAdmission(
        VersionManagerState state,
        ManagedLauncherIdentity launcher,
        out ManagedVersionAdmission? admission)
    {
        admission = state.Admissions.SingleOrDefault(candidate => candidate.Version == launcher.OwnerAppVersion);
        return admission is not null && launcher.MatchesOwner(admission);
    }

    private static bool Matches(InstalledLauncherResult result, ManagedLauncherIdentity expected)
    {
        return result.IsVerified && result.Identity == expected;
    }

    private static LauncherBootstrapOutcome MapVerification(InstalledLauncherIssue issue)
    {
        return issue == InstalledLauncherIssue.ProtocolMismatch
            ? LauncherBootstrapOutcome.ProtocolMismatch
            : issue is InstalledLauncherIssue.Tampered or InstalledLauncherIssue.InvalidManifest or InstalledLauncherIssue.UnsafePath
                ? LauncherBootstrapOutcome.DamagedLauncher
                : LauncherBootstrapOutcome.StateUnavailable;
    }

    private static LauncherBootstrapResult Result(
        LauncherBootstrapOutcome outcome,
        ManagedLauncherIdentity? running = null,
        ManagedLauncherIdentity? failed = null)
    {
        return new(outcome, running, failed);
    }
}
