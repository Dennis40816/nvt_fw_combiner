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
        ManagedVersionInventoryReadResult inventoryResult = state is null
            ? ManagedVersionInventoryReadResult.Success(ManagedVersionInventory.Create([]))
            : await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
        ManagedVersionInventory inventory = inventoryResult.Inventory ?? ManagedVersionInventory.Create([]);
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
                    : loaded.Issue,
            inventoryResult.Issue);
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

    private VersionManagementSnapshot PublishInventoryUnavailable(VersionManagerState? state = null)
    {
        _current = _current is { } current
            ? current with
            {
                State = state ?? current.State,
                Inventory = ManagedVersionInventory.Create([]),
                InventoryIssue = ManagedVersionInventoryReadIssue.Unavailable,
            }
            : new(
                State: state,
                ManagedVersionInventory.Create([]),
                Catalog: null,
                VerifiedCandidate: null,
                VersionSourceStatus.Offline,
                CatalogIssue: null,
                Generation: 0,
                ShouldPromptForUpdate: false,
                VersionManagerStateLoadIssue.None,
                ManagedVersionInventoryReadIssue.Unavailable);
        return _current;
    }

    private static VersionManagementSnapshot WithInventory(
        VersionManagementSnapshot snapshot,
        VersionManagerState state,
        ManagedVersionInventoryReadResult result)
    {
        return snapshot with
        {
            State = state,
            Inventory = result.Inventory ?? ManagedVersionInventory.Create([]),
            InventoryIssue = result.Issue,
        };
    }

    private async ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        ManagedVersionInventoryReadResult observedResult = await _repository.InventoryAsync(
            _managedRoot,
            state.Admissions,
            state.ActiveVersion,
            state.LastKnownGoodVersion,
            state.FailedActivationVersion,
            cancellationToken).ConfigureAwait(false);
        if (!observedResult.IsSuccess)
        {
            return ManagedVersionInventoryReadResult.Unavailable();
        }
        ManagedVersionInventory observed = observedResult.Inventory!;
        ManagedVersionAdmission? recoverable =
            state.PendingMutation is { Kind: ManagedVersionMutationKind.Install } pending
            ? pending.Admission
            : null;
        return ManagedVersionInventoryReadResult.Success(
            ManagedVersionInventory.Create(observed.Versions.Select(row =>
                row.AdmissionState == ManagedVersionAdmissionState.Admitted
                    ? row
                    : recoverable is not null &&
                      row.Integrity == ManagedVersionIntegrity.Healthy &&
                      row.ObservedAdmission == recoverable
                        ? row with { AdmissionState = ManagedVersionAdmissionState.RecoveryCandidate }
                        : row with { AdmissionState = ManagedVersionAdmissionState.Unadmitted })));
    }
}
