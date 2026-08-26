namespace NvtFwCombiner.Application.VersionManagement;

internal sealed partial class LauncherBootstrapCoordinator
{
    private async ValueTask<ActiveAttemptRecovery> RecoverActiveAttemptsAsync(
        VersionManagerState appState,
        LauncherBootstrapState launcherState,
        CancellationToken cancellationToken)
    {
        if (appState.PendingActivation is
            { Phase: VersionActivationPhase.ActiveLaunchRecorded } appGuard)
        {
            ManagedProcessLifetimeStatus lifetime = await _process.GetLifetimeStatusAsync(
                _statePath,
                ManagedProcessLifetimeKind.Application,
                cancellationToken).ConfigureAwait(false);
            if (lifetime != ManagedProcessLifetimeStatus.Exited)
            {
                return new(
                    LauncherBootstrapOutcome.TerminationUnconfirmed,
                    appState,
                    launcherState,
                    launcherState.Active);
            }
            appState = VersionActivationPolicy.ClearActiveLaunch(
                appState,
                appGuard.CandidateVersion);
            VersionManagerStateSaveResult appSaved = await _appStateStore.TrySaveAsync(
                appState,
                cancellationToken).ConfigureAwait(false);
            if (!appSaved.IsSuccess)
            {
                return new(
                    LauncherBootstrapOutcome.StateUnavailable,
                    appState,
                    launcherState,
                    Failed: null);
            }
        }
        if (launcherState.Pending is
            { Phase: LauncherActivationPhase.ActiveLaunchRecorded } launcherGuard)
        {
            ManagedProcessLifetimeStatus lifetime = await _process.GetLifetimeStatusAsync(
                _statePath,
                ManagedProcessLifetimeKind.Launcher,
                cancellationToken).ConfigureAwait(false);
            if (lifetime != ManagedProcessLifetimeStatus.Exited)
            {
                return new(
                    LauncherBootstrapOutcome.TerminationUnconfirmed,
                    appState,
                    launcherState,
                    launcherGuard.Candidate);
            }
            launcherState = launcherState.ClearActiveLaunch(launcherGuard.Candidate);
            if (!await TrySaveLauncherStateAsync(appState, launcherState, cancellationToken)
                .ConfigureAwait(false))
            {
                return new(
                    LauncherBootstrapOutcome.StateUnavailable,
                    appState,
                    launcherState,
                    Failed: null);
            }
        }
        return new(LauncherBootstrapOutcome.Ready, appState, launcherState, Failed: null);
    }

    private sealed record ActiveAttemptRecovery(
        LauncherBootstrapOutcome Outcome,
        VersionManagerState AppState,
        LauncherBootstrapState LauncherState,
        ManagedLauncherIdentity? Failed);
}
