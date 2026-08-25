namespace NvtFwCombiner.Application.VersionManagement;

public sealed partial class VersionManagementExperience
{
    private async ValueTask<VersionManagerState> ReconcilePendingMutationAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        PendingManagedVersionMutation pending = state.PendingMutation ??
            throw new InvalidOperationException("No managed-version mutation requires recovery.");
        VersionManagerState? converged = null;
        if (pending.Kind == ManagedVersionMutationKind.Install)
        {
            ManagedVersionInventory inventory = await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
            InstalledVersionSnapshot? row = inventory.Find(pending.Admission.Version);
            if (row is null)
            {
                converged = state.WithPendingMutation(null);
            }
            else if (row.AdmissionState == ManagedVersionAdmissionState.RecoveryCandidate &&
                     row.Integrity == ManagedVersionIntegrity.Healthy &&
                     row.ObservedAdmission == pending.Admission)
            {
                converged = CommitInstall(state, pending.Admission);
                ManagedVersionInventory committedInventory = await InventoryAsync(
                    converged,
                    cancellationToken).ConfigureAwait(false);
                converged = MarkRetentionReviewDue(converged, committedInventory, updateSucceeded: true);
            }
        }
        else
        {
            ManagedVersionDeleteIssue issue = await _repository.DeleteAsync(
                _managedRoot,
                pending.Admission,
                state.ActiveVersion,
                cancellationToken).ConfigureAwait(false);
            if (issue is ManagedVersionDeleteIssue.None or ManagedVersionDeleteIssue.NotInstalled)
            {
                converged = CommitDelete(state, pending.Admission);
                if (converged.RetentionReviewDue)
                {
                    ManagedVersionInventory committedInventory = await InventoryAsync(
                        converged,
                        cancellationToken).ConfigureAwait(false);
                    converged = ClearRetentionReviewIfAtOrBelowThreshold(converged, committedInventory);
                }
            }
        }

        return converged is not null &&
               await TrySaveAsync(converged, cancellationToken).ConfigureAwait(false)
            ? converged
            : state;
    }

    private static VersionManagerState CommitInstall(
        VersionManagerState state,
        ManagedVersionAdmission admission)
    {
        _ = state.PendingMutation is
        { Kind: ManagedVersionMutationKind.Install, Admission: var pendingAdmission } &&
            pendingAdmission == admission
                ? true
                : throw new InvalidOperationException("Install commit differs from its durable journal.");
        return VersionManagerState.Create(
            state.UpdateSource,
            state.ActiveVersion,
            state.LastKnownGoodVersion,
            [.. state.Admissions, admission],
            state.PendingActivation,
            state.FailedActivationVersion,
            state.RetentionReviewDue,
            pendingMutation: null);
    }

    private static VersionManagerState MarkRetentionReviewDue(
        VersionManagerState state,
        ManagedVersionInventory inventory,
        bool updateSucceeded)
    {
        return !state.RetentionReviewDue &&
               VersionManagementPolicy.ShouldOfferRetentionReview(inventory, updateSucceeded)
            ? state.WithRetentionReviewDue(retentionReviewDue: true)
            : state;
    }

    private static VersionManagerState ClearRetentionReviewIfAtOrBelowThreshold(
        VersionManagerState state,
        ManagedVersionInventory inventory)
    {
        return state.RetentionReviewDue &&
               inventory.HealthyCount <= VersionManagementPolicy.DefaultHealthyVersionReminderThreshold
            ? state.WithRetentionReviewDue(retentionReviewDue: false)
            : state;
    }

    private static VersionManagerState CommitDelete(
        VersionManagerState state,
        ManagedVersionAdmission admission)
    {
        _ = state.PendingMutation is
        { Kind: ManagedVersionMutationKind.Delete, Admission: var pendingAdmission } &&
            pendingAdmission == admission
                ? true
                : throw new InvalidOperationException("Delete commit differs from its durable journal.");
        return VersionManagerState.Create(
            state.UpdateSource,
            state.ActiveVersion,
            state.LastKnownGoodVersion == admission.Version ? null : state.LastKnownGoodVersion,
            state.Admissions.Where(item => item.Version != admission.Version),
            state.PendingActivation,
            state.FailedActivationVersion == admission.Version ? null : state.FailedActivationVersion,
            state.RetentionReviewDue,
            pendingMutation: null);
    }

    private async ValueTask<bool> TrySaveAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        VersionManagerStateSaveResult saved = await _stateStore.TrySaveAsync(
            state,
            cancellationToken).ConfigureAwait(false);
        return saved.IsSuccess;
    }

    private async ValueTask SaveOrThrowAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        if (!await TrySaveAsync(state, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Version-manager state is unavailable.");
        }
    }

    private void PublishChecking(VersionManagementSnapshot current, long generation)
    {
        _current = current with
        {
            SourceStatus = VersionSourceStatus.Checking,
            Generation = generation,
            ShouldPromptForUpdate = false,
        };
    }

    private VersionSourceStatus ResolveSourceStatus(
        UpdateCatalogLoadResult loaded,
        VerifiedUpdateCandidate? verified,
        VersionManagementSnapshot current)
    {
        if (loaded.IsSuccess)
        {
            bool newerExists = loaded.Snapshot!.FindNewestNewerThan(
                current.State!.ActiveVersion ?? _currentAppVersion) is not null;
            return verified is null && newerExists
                ? VersionSourceStatus.Invalid
                : VersionSourceStatus.Connected;
        }
        return loaded.Issue switch
        {
            UpdateCatalogLoadIssue.SourceMissing or UpdateCatalogLoadIssue.SourceUnavailable =>
                VersionSourceStatus.Offline,
            UpdateCatalogLoadIssue.PermissionDenied => VersionSourceStatus.PermissionDenied,
            UpdateCatalogLoadIssue.None or
            UpdateCatalogLoadIssue.UnsafeSource or
            UpdateCatalogLoadIssue.CatalogTooLarge or
            UpdateCatalogLoadIssue.InvalidManifest or
            UpdateCatalogLoadIssue.UnstableRead => VersionSourceStatus.Invalid,
            _ => VersionSourceStatus.Invalid,
        };
    }

    private void SupersedeRunningCheck()
    {
        lock (_generationSync)
        {
            _ = _session.BeginCheck();
            CancelRunningCheckUnderLock();
            if (_current?.SourceStatus == VersionSourceStatus.Checking)
            {
                _current = _current with { SourceStatus = ResolveInterruptedSourceStatus(_current) };
            }
        }
    }

    private VersionSourceStatus ResolveInterruptedSourceStatus(VersionManagementSnapshot snapshot)
    {
        if (snapshot.State?.UpdateSource is null)
        {
            return VersionSourceStatus.NotConfigured;
        }
        if (snapshot.Catalog is not null)
        {
            bool newerExists = snapshot.Catalog.FindNewestNewerThan(
                snapshot.State.ActiveVersion ?? _currentAppVersion) is not null;
            return snapshot.VerifiedCandidate is null && newerExists
                ? VersionSourceStatus.Invalid
                : VersionSourceStatus.Connected;
        }
        return snapshot.CatalogIssue switch
        {
            UpdateCatalogLoadIssue.PermissionDenied => VersionSourceStatus.PermissionDenied,
            UpdateCatalogLoadIssue.SourceMissing or UpdateCatalogLoadIssue.SourceUnavailable =>
                VersionSourceStatus.Offline,
            UpdateCatalogLoadIssue.None or
            UpdateCatalogLoadIssue.UnsafeSource or
            UpdateCatalogLoadIssue.CatalogTooLarge or
            UpdateCatalogLoadIssue.InvalidManifest or
            UpdateCatalogLoadIssue.UnstableRead => VersionSourceStatus.Invalid,
            null => VersionSourceStatus.Offline,
            _ => VersionSourceStatus.Invalid,
        };
    }

    private void CancelRunningCheckUnderLock()
    {
        _checkCancellation?.Cancel();
        _checkCancellation?.Dispose();
        _checkCancellation = null;
    }

    private VersionManagementSnapshot RequirePublishedSnapshotUnderLock()
    {
        return _current ?? throw InvalidState();
    }

    private static VersionManagerState EmptyState()
    {
        return VersionManagerState.Create(
            updateSource: null,
            activeVersion: null,
            lastKnownGoodVersion: null,
            admissions: [],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false);
    }

    private static InvalidOperationException InvalidState()
    {
        return new("Version-manager state is invalid and requires recovery.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
