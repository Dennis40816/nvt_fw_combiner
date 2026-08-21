namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Visible configured-source connection state.</summary>
public enum VersionSourceStatus
{
    /// <summary>No source folder is configured.</summary>
    NotConfigured,
    /// <summary>One current-generation discovery is running.</summary>
    Checking,
    /// <summary>The catalog loaded successfully.</summary>
    Connected,
    /// <summary>The source is missing or unavailable.</summary>
    Offline,
    /// <summary>The configured source exists but the current user cannot read it.</summary>
    PermissionDenied,
    /// <summary>The source catalog or package failed verification.</summary>
    Invalid,
}

/// <summary>Immutable state projected to desktop and launcher consumers.</summary>
public sealed record VersionManagementSnapshot(
    VersionManagerState? State,
    ManagedVersionInventory Inventory,
    UpdateCatalogSnapshot? Catalog,
    VerifiedUpdateCandidate? VerifiedCandidate,
    VersionSourceStatus SourceStatus,
    UpdateCatalogLoadIssue? CatalogIssue,
    long Generation,
    bool ShouldPromptForUpdate,
    VersionManagerStateLoadIssue StateIssue);

/// <summary>Install operation plus its refreshed version-management snapshot.</summary>
public sealed record VersionInstallOperationResult(
    ManagedVersionInstallResult Install,
    VersionManagementSnapshot Snapshot);

/// <summary>Delete operation plus the revalidated policy and refreshed snapshot.</summary>
public sealed record VersionDeleteOperationResult(
    ManagedVersionDeleteDecision Decision,
    VersionDeleteOperationIssue OperationIssue,
    ManagedVersionDeleteIssue? RepositoryIssue,
    VersionManagementSnapshot Snapshot);

/// <summary>Application-owned outcome for one guarded installed-version delete request.</summary>
public enum VersionDeleteOperationIssue
{
    /// <summary>The exact admitted non-active version was deleted.</summary>
    None,
    /// <summary>Application policy blocked the request before filesystem mutation.</summary>
    PolicyBlocked,
    /// <summary>The last-known-good target requires a separate explicit warning.</summary>
    RollbackConfirmationRequired,
    /// <summary>The repository rejected or could not complete the guarded deletion.</summary>
    RepositoryFailure,
}

/// <summary>Typed Application use cases for managed-version Settings and startup coordination.</summary>
public interface IVersionManagementExperience
{
    /// <summary>Loads state and installed inventory without contacting the update source.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The initial immutable snapshot.</returns>
    ValueTask<VersionManagementSnapshot> InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Runs one generation-keyed source check.</summary>
    /// <param name="isAutomatic">Whether a verified newer package may consume the session prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current generation snapshot.</returns>
    ValueTask<VersionManagementSnapshot> CheckAsync(
        bool isAutomatic,
        CancellationToken cancellationToken);

    /// <summary>Commits a source path once and starts one superseding check.</summary>
    /// <param name="sourceRoot">Confirmed source folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checked snapshot.</returns>
    ValueTask<VersionManagementSnapshot> CommitUpdateSourceAsync(
        string sourceRoot,
        CancellationToken cancellationToken);

    /// <summary>Explicitly installs one catalog version after UI consent.</summary>
    /// <param name="version">Exact catalog version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The install result and refreshed snapshot.</returns>
    ValueTask<VersionInstallOperationResult> InstallAsync(
        ManagedAppVersion version,
        CancellationToken cancellationToken);

    /// <summary>Deletes one revalidated non-active admitted version after UI confirmation.</summary>
    /// <param name="version">Exact installed version.</param>
    /// <param name="rollbackLossConfirmed">Whether the separate last-known-good warning was confirmed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The delete decision, adapter result, and refreshed snapshot.</returns>
    ValueTask<VersionDeleteOperationResult> DeleteAsync(
        ManagedAppVersion version,
        bool rollbackLossConfirmed,
        CancellationToken cancellationToken);

    /// <summary>Acknowledges the soft retention reminder without deleting any version.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refreshed snapshot with the reminder cleared.</returns>
    ValueTask<VersionManagementSnapshot> AcknowledgeRetentionReviewAsync(
        CancellationToken cancellationToken);

    /// <summary>Persists one pending activation after the target inventory row is healthy.</summary>
    /// <param name="version">Exact installed target.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pending state to hand to the stable launcher.</returns>
    ValueTask<VersionManagerState> PrepareActivationAsync(
        ManagedAppVersion version,
        CancellationToken cancellationToken);
}

/// <summary>Single Application owner for discovery, install, retention, delete, and activation preparation.</summary>
public sealed class VersionManagementExperience : IVersionManagementExperience, IDisposable
{
    private readonly ManagedAppVersion _currentAppVersion;
    private readonly IUpdateCatalogSource _catalogSource;
    private readonly string _managedRoot;
    private readonly IManagedVersionRepository _repository;
    private readonly VersionDiscoverySession _session = new();
    private readonly IVersionManagerStateStore _stateStore;
    private readonly SemaphoreSlim _mutation = new(1, 1);
    private readonly Lock _generationSync = new();
    private CancellationTokenSource? _checkCancellation;
    private VersionManagementSnapshot? _current;
    private bool _disposed;

    /// <summary>Creates one version-management use-case graph.</summary>
    /// <param name="currentAppVersion">Running application version.</param>
    /// <param name="managedRoot">Stable launcher-owned root.</param>
    /// <param name="stateStore">Atomic launcher-state port.</param>
    /// <param name="catalogSource">Configured-folder catalog port.</param>
    /// <param name="repository">Managed payload repository port.</param>
    public VersionManagementExperience(
        ManagedAppVersion currentAppVersion,
        string managedRoot,
        IVersionManagerStateStore stateStore,
        IUpdateCatalogSource catalogSource,
        IManagedVersionRepository repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        _currentAppVersion = currentAppVersion;
        _managedRoot = managedRoot;
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _catalogSource = catalogSource ?? throw new ArgumentNullException(nameof(catalogSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async ValueTask<VersionManagementSnapshot> InitializeAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RequireCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _mutation.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<VersionManagementSnapshot> CheckAsync(
        bool isAutomatic,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        VersionManagementSnapshot current;
        string sourceRoot;
        long generation;
        CancellationTokenSource ownedCancellation;
        CancellationToken ownedToken;
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = await RequireCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            if (current.State?.UpdateSource is not { } configuredSource)
            {
                return current;
            }
            sourceRoot = configuredSource;
            lock (_generationSync)
            {
                generation = _session.BeginCheck();
                CancelRunningCheckUnderLock();
                _checkCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                ownedCancellation = _checkCancellation;
                ownedToken = ownedCancellation.Token;
                PublishChecking(current, generation);
            }
        }
        finally
        {
            _ = _mutation.Release();
        }
        try
        {
            UpdateCatalogLoadResult loaded = await _catalogSource.LoadAsync(
                sourceRoot,
                ownedToken).ConfigureAwait(false);
            VerifiedUpdateCandidate? verified = null;
            if (loaded.IsSuccess)
            {
                ManagedAppVersion currentVersion = current.State.ActiveVersion ?? _currentAppVersion;
                UpdateCatalogVersionSnapshot? newest = loaded.Snapshot!.FindNewestNewerThan(currentVersion);
                if (newest is not null)
                {
                    ManagedPackageVerificationResult verification = await _repository.VerifyPackageAsync(
                        sourceRoot,
                        newest,
                        ownedToken).ConfigureAwait(false);
                    verified = verification.Candidate;
                }
            }

            lock (_generationSync)
            {
                if (!ReferenceEquals(_checkCancellation, ownedCancellation))
                {
                    return RequirePublishedSnapshotUnderLock();
                }
                bool shouldPrompt = isAutomatic && verified is not null &&
                                    _session.TryPublishAutomaticPrompt(
                                        generation,
                                        current.State.ActiveVersion ?? _currentAppVersion,
                                        verified);
                VersionSourceStatus status = ResolveSourceStatus(loaded, verified, current);
                _current = new(
                    current.State,
                    current.Inventory,
                    loaded.Snapshot,
                    verified,
                    status,
                    loaded.Issue,
                    generation,
                    shouldPrompt,
                    current.StateIssue);
                _checkCancellation = null;
                ownedCancellation.Dispose();
                return _current;
            }
        }
        catch (OperationCanceledException) when (ownedToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            lock (_generationSync)
            {
                return RequirePublishedSnapshotUnderLock();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<VersionManagementSnapshot> CommitUpdateSourceAsync(
        string sourceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ThrowIfDisposed();
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SupersedeRunningCheck();
            VersionManagementSnapshot current = await RequireCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            VersionManagerState state = current.State ?? throw InvalidState();
            state = VersionManagerState.Create(
                sourceRoot,
                state.ActiveVersion,
                state.LastKnownGoodVersion,
                state.Admissions,
                state.PendingActivation,
                state.FailedActivationVersion,
                state.RetentionReviewDue);
            await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
            _current = current with
            {
                State = state,
                Catalog = null,
                VerifiedCandidate = null,
                SourceStatus = VersionSourceStatus.Offline,
                CatalogIssue = null,
                ShouldPromptForUpdate = false,
            };
        }
        finally
        {
            _ = _mutation.Release();
        }
        return await CheckAsync(isAutomatic: false, cancellationToken).ConfigureAwait(false);
    }

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
            VersionManagementSnapshot current = await RequireCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            VersionManagerState state = current.State ?? throw InvalidState();
            string sourceRoot = state.UpdateSource ?? throw new InvalidOperationException("No update source is configured.");
            UpdateCatalogVersionSnapshot package = current.Catalog?.Versions.SingleOrDefault(
                candidate => candidate.Version == version) ??
                throw new InvalidOperationException("The requested version is not in the current catalog generation.");
            ManagedVersionInstallResult result = await _repository.InstallAsync(
                _managedRoot,
                sourceRoot,
                package,
                cancellationToken).ConfigureAwait(false);
            if (result.Admission is not null)
            {
                ManagedVersionAdmission[] admissions =
                [.. state.Admissions.Where(admission => admission.Version != version), result.Admission];
                state = VersionManagerState.Create(
                    state.UpdateSource,
                    state.ActiveVersion,
                    state.LastKnownGoodVersion,
                    admissions,
                    state.PendingActivation,
                    failedActivationVersion: null,
                    state.RetentionReviewDue);
                await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
            }
            ManagedVersionInventory inventory = await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
            bool retentionReviewDue = state.RetentionReviewDue ||
                VersionManagementPolicy.ShouldOfferRetentionReview(
                    inventory,
                    result.IsSuccess && !result.WasAlreadyInstalled);
            if (retentionReviewDue != state.RetentionReviewDue)
            {
                state = state.WithRetentionReviewDue(retentionReviewDue);
                await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
            }
            _current = current with { State = state, Inventory = inventory };
            return new(result, _current);
        }
        finally
        {
            _ = _mutation.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<VersionDeleteOperationResult> DeleteAsync(
        ManagedAppVersion version,
        bool rollbackLossConfirmed,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SupersedeRunningCheck();
            VersionManagementSnapshot current = await RequireCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            VersionManagerState state = current.State ?? throw InvalidState();
            ManagedVersionInventory inventory = await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
            ManagedVersionDeleteDecision decision = VersionManagementPolicy.DecideDelete(inventory, version);
            if (!decision.IsAllowed)
            {
                return new(
                    decision,
                    VersionDeleteOperationIssue.PolicyBlocked,
                    RepositoryIssue: null,
                    current with { Inventory = inventory });
            }
            if (decision.RequiresRollbackLossWarning && !rollbackLossConfirmed)
            {
                return new(
                    decision,
                    VersionDeleteOperationIssue.RollbackConfirmationRequired,
                    RepositoryIssue: null,
                    current with { Inventory = inventory });
            }
            ManagedVersionAdmission admission = state.Admissions.Single(item => item.Version == version);
            ManagedVersionDeleteIssue deleteIssue = await _repository.DeleteAsync(
                _managedRoot,
                admission,
                state.ActiveVersion,
                cancellationToken).ConfigureAwait(false);
            if (deleteIssue == ManagedVersionDeleteIssue.None)
            {
                state = VersionManagerState.Create(
                    state.UpdateSource,
                    state.ActiveVersion,
                    state.LastKnownGoodVersion == version ? null : state.LastKnownGoodVersion,
                    state.Admissions.Where(item => item.Version != version),
                    state.PendingActivation,
                    state.FailedActivationVersion == version ? null : state.FailedActivationVersion,
                    state.RetentionReviewDue);
                await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
            }
            inventory = await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
            if (state.RetentionReviewDue &&
                inventory.HealthyCount <= VersionManagementPolicy.DefaultHealthyVersionReminderThreshold)
            {
                state = state.WithRetentionReviewDue(retentionReviewDue: false);
                await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
            }
            _current = current with { State = state, Inventory = inventory };
            return new(
                decision,
                deleteIssue == ManagedVersionDeleteIssue.None
                    ? VersionDeleteOperationIssue.None
                    : VersionDeleteOperationIssue.RepositoryFailure,
                deleteIssue,
                _current);
        }
        finally
        {
            _ = _mutation.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<VersionManagementSnapshot> AcknowledgeRetentionReviewAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SupersedeRunningCheck();
            VersionManagementSnapshot current = await RequireCurrentWithoutLockAsync(cancellationToken)
                .ConfigureAwait(false);
            VersionManagerState state = current.State ?? throw InvalidState();
            if (state.RetentionReviewDue)
            {
                state = state.WithRetentionReviewDue(retentionReviewDue: false);
                await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
                _current = current with { State = state };
            }
            return _current ?? current;
        }
        finally
        {
            _ = _mutation.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<VersionManagerState> PrepareActivationAsync(
        ManagedAppVersion version,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SupersedeRunningCheck();
            VersionManagementSnapshot current = await RequireCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            VersionManagerState state = current.State ?? throw InvalidState();
            ManagedVersionInventory inventory = await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
            InstalledVersionSnapshot target = inventory.Find(version) ??
                throw new InvalidOperationException("Activation target is not installed.");
            if (target.Integrity != ManagedVersionIntegrity.Healthy)
            {
                throw new InvalidOperationException("Activation target is damaged.");
            }
            state = VersionActivationPolicy.BeginActivation(state, version);
            await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
            _current = current with { State = state, Inventory = inventory };
            return state;
        }
        finally
        {
            _ = _mutation.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        lock (_generationSync)
        {
            _checkCancellation?.Cancel();
            _checkCancellation?.Dispose();
            _checkCancellation = null;
        }
        _mutation.Dispose();
        _disposed = true;
    }

    private async ValueTask<VersionManagementSnapshot> RequireCurrentWithoutLockAsync(
        CancellationToken cancellationToken)
    {
        if (_current is not null)
        {
            return _current;
        }
        VersionManagerStateLoadResult loaded = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        VersionManagerState? state = loaded.IsSuccess
            ? loaded.State
            : loaded.Issue == VersionManagerStateLoadIssue.Missing
                ? EmptyState()
                : null;
        ManagedVersionInventory inventory = state is null
            ? ManagedVersionInventory.Create([])
            : await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
        _current = new(
            state,
            inventory,
            null,
            null,
            state?.UpdateSource is null ? VersionSourceStatus.NotConfigured : VersionSourceStatus.Offline,
            null,
            0,
            false,
            loaded.Issue == VersionManagerStateLoadIssue.Missing
                ? VersionManagerStateLoadIssue.None
                : loaded.Issue);
        return _current;
    }

    private ValueTask<ManagedVersionInventory> InventoryAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        return _repository.InventoryAsync(
            _managedRoot,
            state.Admissions,
            state.ActiveVersion,
            state.LastKnownGoodVersion,
            state.FailedActivationVersion,
            cancellationToken);
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
