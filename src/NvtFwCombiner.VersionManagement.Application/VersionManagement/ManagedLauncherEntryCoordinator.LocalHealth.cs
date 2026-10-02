namespace NvtFwCombiner.Application.VersionManagement;

public sealed partial class ManagedLauncherEntryCoordinator
{
    private async ValueTask<LocalHealthObservation> ObserveLocalHealthAsync(
        EntryStageTracker stages,
        CancellationToken cancellationToken)
    {
        ManagedDistributionPayloadEntryAdmissionResult payload = await _payloadSource
            .AdmitEntryAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!payload.IsSuccess)
        {
            return new(
                payload.Issue switch
                {
                    ManagedDistributionPayloadIssue.Unavailable =>
                        ManagedLauncherEntryOutcome.PayloadUnavailable,
                    ManagedDistributionPayloadIssue.Invalid or ManagedDistributionPayloadIssue.Changed =>
                        ManagedLauncherEntryOutcome.PayloadInvalid,
                    ManagedDistributionPayloadIssue.None => throw new InvalidOperationException(
                        "Successful payload admission returned no Bootstrap identity."),
                    _ => throw new InvalidOperationException(
                        "Payload admission returned an undefined issue."),
                },
                ManagedRoot: null,
                Bootstrap: null,
                payload.Issue == ManagedDistributionPayloadIssue.Unavailable
                    ? ManagedLauncherEntryReason.Unavailable
                    : ManagedLauncherEntryReason.PayloadInvalid,
                ManagedLauncherEntryStage.PayloadAdmission);
        }
        if (payload.LauncherVersion != _runningLauncherVersion)
        {
            return new(
                ManagedLauncherEntryOutcome.PayloadInvalid,
                ManagedRoot: null,
                Bootstrap: null,
                ManagedLauncherEntryReason.PayloadInvalid,
                ManagedLauncherEntryStage.PayloadAdmission);
        }
        ManagedImmutableBootstrapIdentity bootstrap = payload.Bootstrap!;

        stages.Stage = ManagedLauncherEntryStage.StateLoad;
        VersionManagerStateLoadResult loaded = await _stateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (loaded.Issue == VersionManagerStateLoadIssue.Missing)
        {
            stages.Stage = ManagedLauncherEntryStage.RootObservation;
            ManagedInstallationRootObservation root = await _rootProbe.ObserveAsync(
                _defaultManagedRoot,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new(
                root.Status switch
                {
                    ManagedInstallationRootStatus.Absent =>
                        ManagedLauncherEntryOutcome.SetupRequired,
                    ManagedInstallationRootStatus.Present or
                    ManagedInstallationRootStatus.Residue or
                    ManagedInstallationRootStatus.InvalidDestination =>
                        ManagedLauncherEntryOutcome.RecoveryRequired,
                    ManagedInstallationRootStatus.PermissionDenied or
                    ManagedInstallationRootStatus.Unavailable =>
                        ManagedLauncherEntryOutcome.HealthUnavailable,
                    _ => throw new InvalidOperationException(
                        "Root probe returned an undefined status."),
                },
                _defaultManagedRoot,
                bootstrap,
                RootReason(root.Status),
                ManagedLauncherEntryStage.RootObservation);
        }
        if (!loaded.IsSuccess)
        {
            return new(
                loaded.Issue is VersionManagerStateLoadIssue.Invalid or
                    VersionManagerStateLoadIssue.ManagedRootMismatch
                    ? ManagedLauncherEntryOutcome.RecoveryRequired
                    : ManagedLauncherEntryOutcome.HealthUnavailable,
                ManagedRoot: null,
                Bootstrap: bootstrap,
                loaded.Issue is VersionManagerStateLoadIssue.Invalid or
                    VersionManagerStateLoadIssue.ManagedRootMismatch
                    ? ManagedLauncherEntryReason.RecoveryRequired
                    : ManagedLauncherEntryReason.Unavailable,
                ManagedLauncherEntryStage.StateLoad);
        }

        VersionManagerState state = loaded.State!;
        if (state.ManagedRootIdentity is null)
        {
            return new(
                ManagedLauncherEntryOutcome.RecoveryRequired,
                ManagedRoot: null,
                Bootstrap: bootstrap,
                ManagedLauncherEntryReason.RecoveryRequired,
                ManagedLauncherEntryStage.StateLoad);
        }
        string rootIdentity = ManagedRootPathIdentity.Normalize(state.ManagedRootIdentity);
        stages.Stage = ManagedLauncherEntryStage.RootObservation;
        ManagedInstallationRootObservation installedRoot = await _rootProbe.ObserveAsync(
            rootIdentity,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return installedRoot.Status == ManagedInstallationRootStatus.Present
            ? new(
                Outcome: null,
                rootIdentity,
                bootstrap,
                ManagedLauncherEntryReason.Success,
                ManagedLauncherEntryStage.RootObservation)
            : new(
                installedRoot.Status is ManagedInstallationRootStatus.Absent or
                    ManagedInstallationRootStatus.Residue or
                    ManagedInstallationRootStatus.InvalidDestination
                    ? ManagedLauncherEntryOutcome.RecoveryRequired
                    : ManagedLauncherEntryOutcome.HealthUnavailable,
                rootIdentity,
                bootstrap,
                installedRoot.Status == ManagedInstallationRootStatus.Absent
                    ? ManagedLauncherEntryReason.RecoveryRequired
                    : RootReason(installedRoot.Status),
                ManagedLauncherEntryStage.RootObservation);
    }

    private static async ValueTask<T> AwaitIsolatedReadOnlyObservationAsync<T>(
        Func<CancellationToken, ValueTask<T>> observe,
        CancellationToken cancellationToken)
    {
        Task<T> pending = Task.Run(
            async () => await observe(cancellationToken).ConfigureAwait(false),
            CancellationToken.None);
        try
        {
            return await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = ObserveAbandonedReadOnlyTaskAsync(pending);
            throw;
        }
    }

    private static async Task ObserveAbandonedReadOnlyTaskAsync(Task pending)
    {
        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The caller has already returned a typed timeout/cancellation result.
        }
    }

    private sealed record LocalHealthObservation(
        ManagedLauncherEntryOutcome? Outcome,
        string? ManagedRoot,
        ManagedImmutableBootstrapIdentity? Bootstrap,
        ManagedLauncherEntryReason Reason,
        ManagedLauncherEntryStage Stage);

    private static ManagedLauncherEntryReason RootReason(ManagedInstallationRootStatus status)
    {
        return status switch
        {
            ManagedInstallationRootStatus.Absent => ManagedLauncherEntryReason.SetupRequired,
            ManagedInstallationRootStatus.Present or
            ManagedInstallationRootStatus.Residue or
            ManagedInstallationRootStatus.InvalidDestination => ManagedLauncherEntryReason.RecoveryRequired,
            ManagedInstallationRootStatus.PermissionDenied => ManagedLauncherEntryReason.PermissionDenied,
            ManagedInstallationRootStatus.Unavailable => ManagedLauncherEntryReason.Unavailable,
            _ => ManagedLauncherEntryReason.Unavailable,
        };
    }

    private sealed class EntryStageTracker
    {
        private int _stage = (int)ManagedLauncherEntryStage.PayloadAdmission;

        internal ManagedLauncherEntryStage Stage
        {
            get => (ManagedLauncherEntryStage)Volatile.Read(ref _stage);
            set => Volatile.Write(ref _stage, (int)value);
        }
    }
}
