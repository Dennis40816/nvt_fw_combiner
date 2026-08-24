namespace NvtFwCombiner.Application.VersionManagement;

public sealed partial class VersionManagementExperience
{
    /// <inheritdoc />
    public async ValueTask<VersionInstallOperationResult> InstallAsync(
        ManagedAppVersion version,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SupersedeRunningCheck();
            using VersionManagerWriteLeaseResult lease = await AcquireWriteLeaseAsync(
                TimeSpan.Zero,
                cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                return new(
                    new(null, ManagedVersionInstallIssue.StateUnavailable, WasAlreadyInstalled: false),
                    PublishStateUnavailable());
            }
            VersionManagementSnapshot current = await ReloadDurableCurrentWithoutLockAsync(cancellationToken)
                .ConfigureAwait(false);
            VersionManagerState state = current.State ?? throw InvalidState();
            if (state.PendingMutation is not null)
            {
                state = await ReconcilePendingMutationAsync(state, cancellationToken).ConfigureAwait(false);
                current = current with
                {
                    State = state,
                    Inventory = await InventoryAsync(state, cancellationToken).ConfigureAwait(false),
                };
                _current = current;
                if (state.PendingMutation is not null)
                {
                    return new(
                        new(null, ManagedVersionInstallIssue.StateUnavailable, WasAlreadyInstalled: false),
                        current);
                }
            }
            if (state.UpdateSource is not { } sourceRoot || current.Catalog?.Versions.SingleOrDefault(candidate => candidate.Version == version) is not { } package)
            {
                return new(new(null, ManagedVersionInstallIssue.PackageUnavailable, WasAlreadyInstalled: false), current);
            }
            var expectedAdmission = new ManagedVersionAdmission(package.Version, package.Identity, package.ReleaseManifestSha256);
            ManagedVersionAdmission? existingAdmission = state.Admissions.SingleOrDefault(
                admission => admission.Version == version);
            if (existingAdmission is not null)
            {
                ManagedVersionInventory existingInventory = await InventoryAsync(
                    state,
                    cancellationToken).ConfigureAwait(false);
                _current = current with { State = state, Inventory = existingInventory };
                return existingAdmission == expectedAdmission
                    ? new(
                        new(existingAdmission, ManagedVersionInstallIssue.None, WasAlreadyInstalled: true),
                        _current)
                    : new(
                        new(null, ManagedVersionInstallIssue.IdentityConflict, WasAlreadyInstalled: false),
                        _current);
            }
            VersionManagerState prepared = state.WithPendingMutation(
                new(ManagedVersionMutationKind.Install, expectedAdmission));
            if (!await TrySaveAsync(prepared, cancellationToken).ConfigureAwait(false))
            {
                return new(
                    new(null, ManagedVersionInstallIssue.StateUnavailable, WasAlreadyInstalled: false),
                    current);
            }
            state = prepared;
            _current = current with { State = state };

            ManagedVersionInstallResult result = await _repository.InstallAsync(
                _managedRoot,
                sourceRoot,
                package,
                cancellationToken).ConfigureAwait(false);
            if (result.Admission is null || result.Admission != expectedAdmission)
            {
                VersionManagerState cleared = state.WithPendingMutation(null);
                if (!await TrySaveAsync(cleared, cancellationToken).ConfigureAwait(false))
                {
                    ManagedVersionInventory pendingInventory = await InventoryAsync(
                        state,
                        cancellationToken).ConfigureAwait(false);
                    _current = current with { State = state, Inventory = pendingInventory };
                    return new(
                        new(null, ManagedVersionInstallIssue.StateUnavailable, WasAlreadyInstalled: false),
                        _current);
                }
                state = cleared;
                ManagedVersionInventory failedInventory = await InventoryAsync(
                    state,
                    cancellationToken).ConfigureAwait(false);
                _current = current with { State = state, Inventory = failedInventory };
                return new(result, _current);
            }

            state = CommitInstall(state, expectedAdmission);
            ManagedVersionInventory inventory = await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
            bool retentionReviewDue = state.RetentionReviewDue ||
                VersionManagementPolicy.ShouldOfferRetentionReview(
                    inventory,
                    result.IsSuccess && !result.WasAlreadyInstalled);
            if (retentionReviewDue != state.RetentionReviewDue)
            {
                state = state.WithRetentionReviewDue(retentionReviewDue);
            }
            if (!await TrySaveAsync(state, cancellationToken).ConfigureAwait(false))
            {
                VersionManagerState durablePrepared = prepared;
                ManagedVersionInventory recoveryInventory = await InventoryAsync(
                    durablePrepared,
                    cancellationToken).ConfigureAwait(false);
                _current = current with { State = durablePrepared, Inventory = recoveryInventory };
                return new(
                    new(null, ManagedVersionInstallIssue.StateUnavailable, WasAlreadyInstalled: false),
                    _current);
            }
            _current = current with { State = state, Inventory = inventory };
            return new(result, _current);
        }
        finally
        {
            _ = _mutation.Release();
        }
    }
}
