namespace NvtFwCombiner.Application.VersionManagement;

public sealed partial class VersionManagementExperience
{
    private async ValueTask<VersionManagementSnapshot> RequireCurrentWithoutLockAsync(
        CancellationToken cancellationToken)
    {
        if (_current is not null)
        {
            return _current;
        }
        using VersionManagerWriteLeaseResult lease = await AcquireWriteLeaseAsync(
            TimeSpan.Zero,
            cancellationToken).ConfigureAwait(false);
        return lease.IsAcquired
            ? await ReloadDurableCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false)
            : PublishStateUnavailable();
    }

    private async ValueTask<VersionManagementSnapshot> ReloadDurableCurrentWithoutLockAsync(
        CancellationToken cancellationToken)
    {
        VersionManagementSnapshot? prior = _current;
        VersionManagerStateLoadResult loaded = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        bool managedRootMismatch = loaded.IsSuccess && !loaded.State!.IsBoundToManagedRoot(_managedRoot);
        VersionManagerState? state = loaded.IsSuccess && !managedRootMismatch
            ? loaded.State
            : loaded.Issue == VersionManagerStateLoadIssue.Missing
                ? EmptyState()
                : null;
        if (state?.PendingMutation is not null)
        {
            state = await ReconcilePendingMutationAsync(state, cancellationToken).ConfigureAwait(false);
        }
        ManagedVersionInventory inventory = state is null
            ? ManagedVersionInventory.Create([])
            : await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
        bool sameSource = prior?.State?.UpdateSource == state?.UpdateSource;
        _current = new(
            state,
            inventory,
            sameSource ? prior?.Catalog : null,
            sameSource ? prior?.VerifiedCandidate : null,
            sameSource && prior is not null
                ? prior.SourceStatus
                : state is null
                    ? VersionSourceStatus.Offline
                    : state.UpdateSource is null
                        ? VersionSourceStatus.NotConfigured
                        : VersionSourceStatus.Offline,
            sameSource ? prior?.CatalogIssue : null,
            sameSource ? prior?.Generation ?? 0 : 0,
            sameSource && prior?.ShouldPromptForUpdate == true,
            managedRootMismatch
                ? VersionManagerStateLoadIssue.ManagedRootMismatch
                : loaded.Issue == VersionManagerStateLoadIssue.Missing
                    ? VersionManagerStateLoadIssue.None
                    : loaded.Issue);
        return _current;
    }

    private ValueTask<VersionManagerWriteLeaseResult> AcquireWriteLeaseAsync(
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
    {
        return _stateStore.TryAcquireWriteLeaseAsync(
            waitTimeout,
            cancellationToken);
    }

    private VersionManagementSnapshot PublishStateUnavailable()
    {
        _current = _current is { } current
            ? current with { StateIssue = VersionManagerStateLoadIssue.Unavailable }
            : new(
                State: null,
                ManagedVersionInventory.Create([]),
                Catalog: null,
                VerifiedCandidate: null,
                VersionSourceStatus.Offline,
                CatalogIssue: null,
                Generation: 0,
                ShouldPromptForUpdate: false,
                VersionManagerStateLoadIssue.Unavailable);
        return _current;
    }

    private async ValueTask<ManagedVersionInventory> InventoryAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        ManagedVersionInventory observed = await _repository.InventoryAsync(
            _managedRoot,
            state.Admissions,
            state.ActiveVersion,
            state.LastKnownGoodVersion,
            state.FailedActivationVersion,
            cancellationToken).ConfigureAwait(false);
        ManagedVersionAdmission? recoverable =
            state.PendingMutation is { Kind: ManagedVersionMutationKind.Install } pending
            ? pending.Admission
            : null;
        return ManagedVersionInventory.Create(observed.Versions.Select(row =>
            row.AdmissionState == ManagedVersionAdmissionState.Admitted
                ? row
                : recoverable is not null &&
                  row.Integrity == ManagedVersionIntegrity.Healthy &&
                  row.ObservedAdmission == recoverable
                    ? row with { AdmissionState = ManagedVersionAdmissionState.RecoveryCandidate }
                    : row with { AdmissionState = ManagedVersionAdmissionState.Unadmitted }));
    }
}
