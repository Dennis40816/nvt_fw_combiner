namespace NvtFwCombiner.Application.VersionManagement;

public sealed partial class VersionManagementExperience
{
    private async ValueTask<VersionManagementSnapshot> RequireCurrentWithoutLockAsync(
        CancellationToken cancellationToken)
    {
        if (_current is { } current && HasUsableAuthority(current))
        {
            return current;
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
        bool isRecoveringSourceAuthority = prior is null || !HasUsableAuthority(prior);
        VersionManagementSnapshot? sourcePrior = isRecoveringSourceAuthority
            ? _recoverableSourceAuthority
            : prior;
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
            ? ManagedVersionInventoryReadResult.Unavailable()
            : await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
        ManagedVersionInventory inventory = inventoryResult.Inventory ?? ManagedVersionInventory.Create([]);
        bool sameSource = state is not null && inventoryResult.IsSuccess &&
            sourcePrior?.State?.UpdateSource == state.UpdateSource;
        bool sameCandidateContext = sameSource &&
            sourcePrior?.State?.ActiveVersion == state?.ActiveVersion;
        _current = new(
            state,
            inventory,
            sameSource ? sourcePrior?.Catalog : null,
            sameCandidateContext && !isRecoveringSourceAuthority
                ? sourcePrior?.VerifiedCandidate
                : null,
            sameSource && sourcePrior is not null && !isRecoveringSourceAuthority
                ? sourcePrior.SourceStatus
                : state is null
                    ? VersionSourceStatus.Offline
                    : state.UpdateSource is null
                        ? VersionSourceStatus.NotConfigured
                        : VersionSourceStatus.Offline,
            sameSource && !isRecoveringSourceAuthority ? sourcePrior?.CatalogIssue : null,
            sameCandidateContext && !isRecoveringSourceAuthority
                ? sourcePrior?.Generation ?? 0
                : 0,
            sameCandidateContext && !isRecoveringSourceAuthority &&
                sourcePrior?.ShouldPromptForUpdate == true,
            managedRootMismatch
                ? VersionManagerStateLoadIssue.ManagedRootMismatch
                : loaded.Issue == VersionManagerStateLoadIssue.Missing
                    ? VersionManagerStateLoadIssue.None
                    : loaded.Issue,
            inventoryResult.Issue);
        _current = _current with
        {
            RegistryStatus = state?.SourceRegistryState?.IsManualPin == true
                ? VersionRegistryStatus.ManualPin
                : sameSource
                    ? sourcePrior?.RegistryStatus ?? VersionRegistryStatus.NotConfigured
                    : VersionRegistryStatus.NotConfigured,
            RegistryIssue = sameSource
                ? sourcePrior?.RegistryIssue ?? UpdateSourceRegistryIssue.None
                : UpdateSourceRegistryIssue.None,
        };
        if (state is not null && inventoryResult.IsSuccess)
        {
            _recoverableSourceAuthority = null;
        }
        else if (managedRootMismatch || loaded.Issue == VersionManagerStateLoadIssue.Invalid)
        {
            _recoverableSourceAuthority = null;
        }
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
        PreserveRecoverableSourceAuthority();
        _current = _current is { } current
            ? current with
            {
                Inventory = ManagedVersionInventory.Create([]),
                Catalog = null,
                VerifiedCandidate = null,
                SourceStatus = VersionSourceStatus.Offline,
                CatalogIssue = null,
                Generation = 0,
                ShouldPromptForUpdate = false,
                StateIssue = VersionManagerStateLoadIssue.Unavailable,
                InventoryIssue = ManagedVersionInventoryReadIssue.Unavailable,
            }
            : new(
                State: null,
                ManagedVersionInventory.Create([]),
                Catalog: null,
                VerifiedCandidate: null,
                VersionSourceStatus.Offline,
                CatalogIssue: null,
                Generation: 0,
                ShouldPromptForUpdate: false,
                VersionManagerStateLoadIssue.Unavailable,
                ManagedVersionInventoryReadIssue.Unavailable);
        return _current;
    }

    private VersionManagementSnapshot PublishInventoryUnavailable(VersionManagerState? state = null)
    {
        PreserveRecoverableSourceAuthority();
        _current = _current is { } current
            ? current with
            {
                State = state ?? current.State,
                Inventory = ManagedVersionInventory.Create([]),
                Catalog = null,
                VerifiedCandidate = null,
                SourceStatus = VersionSourceStatus.Offline,
                CatalogIssue = null,
                Generation = 0,
                ShouldPromptForUpdate = false,
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

    private VersionManagementSnapshot WithInventory(
        VersionManagementSnapshot snapshot,
        VersionManagerState state,
        ManagedVersionInventoryReadResult result)
    {
        VersionManagementSnapshot updated = snapshot with
        {
            State = state,
            Inventory = result.Inventory ?? ManagedVersionInventory.Create([]),
            InventoryIssue = result.Issue,
        };
        if (result.IsSuccess)
        {
            return updated;
        }

        if (HasUsableAuthority(snapshot))
        {
            _recoverableSourceAuthority = snapshot;
        }
        return updated with
        {
            Catalog = null,
            VerifiedCandidate = null,
            SourceStatus = VersionSourceStatus.Offline,
            CatalogIssue = null,
            Generation = 0,
            ShouldPromptForUpdate = false,
        };
    }

    private void PreserveRecoverableSourceAuthority()
    {
        if (_current is { } current && HasUsableAuthority(current))
        {
            _recoverableSourceAuthority = current;
        }
    }

    private static bool HasUsableAuthority(VersionManagementSnapshot snapshot)
    {
        return snapshot.StateIssue == VersionManagerStateLoadIssue.None &&
            snapshot.InventoryIssue == ManagedVersionInventoryReadIssue.None;
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
