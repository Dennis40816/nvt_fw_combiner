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
    VersionManagerStateLoadIssue StateIssue,
    ManagedVersionInventoryReadIssue InventoryIssue = ManagedVersionInventoryReadIssue.None,
    VersionRegistryStatus RegistryStatus = VersionRegistryStatus.NotConfigured,
    UpdateSourceRegistryIssue RegistryIssue = UpdateSourceRegistryIssue.None);

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
    /// <summary>An activation or recovery transaction blocks deletion, writer/durable state is unavailable, or the mutation journal or commit cannot be saved.</summary>
    StateUnavailable,
}

/// <summary>Typed Application use cases for managed-version Settings and startup coordination.</summary>
public interface IVersionManagementExperience
{
    /// <summary>Loads state and installed inventory without contacting the update source.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The initial immutable snapshot.</returns>
    ValueTask<VersionManagementSnapshot> InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Loads durable state after an exact inherited READY write while the launcher commits.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The durable post-launcher snapshot.</returns>
    ValueTask<VersionManagementSnapshot> InitializeAfterManagedReadyAsync(
        CancellationToken cancellationToken);

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

    /// <summary>Resumes automatic registry resolution without clearing a durable pin on failure.</summary>
    ValueTask<VersionManagementSnapshot> ResumeRegistryAsync(CancellationToken cancellationToken)
    {
        return CheckAsync(isAutomatic: false, cancellationToken);
    }

    /// <summary>Verifies the fixed registry and every automatic source without mutating session or durable state.</summary>
    ValueTask<VersionEnvironmentSelfTestResult> RunEnvironmentSelfTestAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new VersionEnvironmentSelfTestResult(
            UpdateSourceRegistryLoadIssue.NotConfigured,
            []));
    }

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

    /// <summary>Clears an unlaunched activation after stable-launcher handoff fails.</summary>
    ValueTask<VersionManagementSnapshot> CancelPendingActivationAsync(
        CancellationToken cancellationToken);
}

/// <summary>Single Application owner for discovery, install, retention, delete, and activation preparation.</summary>
public sealed partial class VersionManagementExperience : IVersionManagementExperience, IDisposable
{
    private readonly ManagedAppVersion _currentAppVersion;
    private readonly IUpdateCatalogSource _catalogSource;
    private readonly string _managedRoot;
    private readonly IManagedVersionRepository _repository;
    private readonly IUpdateSourceRegistry? _sourceRegistry;
    private readonly IVersionManagementMutationFence _mutationFence;
    private readonly VersionDiscoverySession _session = new();
    private readonly IVersionManagerStateStore _stateStore;
    private readonly SemaphoreSlim _mutation = new(1, 1);
    private readonly Lock _generationSync = new();
    private CancellationTokenSource? _checkCancellation;
    private VersionManagementSnapshot? _current;
    private VersionManagementSnapshot? _recoverableSourceAuthority;
    private bool _disposed;

    /// <summary>Creates one version-management use-case graph.</summary>
    /// <param name="currentAppVersion">Running application version.</param>
    /// <param name="managedRoot">Stable launcher-owned root.</param>
    /// <param name="stateStore">Atomic launcher-state port.</param>
    /// <param name="catalogSource">Configured-folder catalog port.</param>
    /// <param name="repository">Managed payload repository port.</param>
    /// <param name="sourceRegistry">Optional fixed-registry read port.</param>
    /// <param name="mutationFence">Optional launcher logical mutation fence.</param>
    public VersionManagementExperience(
        ManagedAppVersion currentAppVersion,
        string managedRoot,
        IVersionManagerStateStore stateStore,
        IUpdateCatalogSource catalogSource,
        IManagedVersionRepository repository,
        IUpdateSourceRegistry? sourceRegistry = null,
        IVersionManagementMutationFence? mutationFence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        _currentAppVersion = currentAppVersion;
        _managedRoot = ManagedRootPathIdentity.Normalize(managedRoot);
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _catalogSource = catalogSource ?? throw new ArgumentNullException(nameof(catalogSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sourceRegistry = sourceRegistry;
        _mutationFence = mutationFence ?? AllowVersionManagementMutationFence.Instance;
    }

    /// <inheritdoc />
    public async ValueTask<VersionManagementSnapshot> InitializeAsync(CancellationToken cancellationToken)
    {
        return await InitializeWithWriterLeaseTimeoutAsync(
            TimeSpan.Zero,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<VersionManagementSnapshot> InitializeAfterManagedReadyAsync(
        CancellationToken cancellationToken)
    {
        return await InitializeWithWriterLeaseTimeoutAsync(
            ManagedActivationCoordinator.DefaultWriterLeaseTimeout,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<VersionManagementSnapshot> InitializeWithWriterLeaseTimeoutAsync(
        TimeSpan writerLeaseTimeout,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using VersionManagerWriteLeaseResult lease = await AcquireWriteLeaseAsync(
                writerLeaseTimeout,
                cancellationToken).ConfigureAwait(false);
            return lease.IsAcquired
                ? await ReloadDurableCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false)
                : PublishStateUnavailable();
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
        if (_sourceRegistry is not null)
        {
            VersionManagementSnapshot? registryResult = await CheckRegistryAsync(
                isAutomatic,
                allowManualPin: false,
                cancellationToken).ConfigureAwait(false);
            if (registryResult is not null)
            {
                return registryResult;
            }
        }
        VersionManagementSnapshot current;
        string sourceRoot;
        long generation;
        CancellationTokenSource ownedCancellation;
        CancellationToken ownedToken;
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = await RequireCurrentWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            if (_sourceRegistry is null &&
                current.State?.SourceRegistryState is { IsManualPin: false })
            {
                lock (_generationSync)
                {
                    CancelRunningCheckUnderLock();
                    _current = current with
                    {
                        Catalog = null,
                        VerifiedCandidate = null,
                        SourceStatus = VersionSourceStatus.NotConfigured,
                        CatalogIssue = null,
                        ShouldPromptForUpdate = false,
                        RegistryStatus = VersionRegistryStatus.NotConfigured,
                        RegistryIssue = UpdateSourceRegistryIssue.NotConfigured,
                    };
                    return _current;
                }
            }
            if (current.StateIssue != VersionManagerStateLoadIssue.None ||
                current.InventoryIssue != ManagedVersionInventoryReadIssue.None ||
                current.State?.UpdateSource is not { } configuredSource)
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
                    verified = verification is { IsVerified: true, Candidate: { } candidate } &&
                               candidate.Version == newest.Version &&
                               string.Equals(
                                   candidate.AdmissionIdentity,
                                   newest.Identity,
                                   StringComparison.Ordinal)
                        ? candidate
                        : null;
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
                    current.StateIssue,
                    current.InventoryIssue,
                    current.State?.SourceRegistryState?.IsManualPin == true
                        ? VersionRegistryStatus.ManualPin
                        : current.RegistryStatus,
                    current.RegistryIssue);
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
            using VersionManagerWriteLeaseResult lease = await AcquireWriteLeaseAsync(
                TimeSpan.Zero,
                cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                return PublishStateUnavailable();
            }
            VersionManagementSnapshot current = await ReloadDurableCurrentWithoutLockAsync(cancellationToken)
                .ConfigureAwait(false);
            if (current.State is not { } state)
            {
                return current;
            }
            bool registryAware = _sourceRegistry is not null || state.SourceRegistryState is not null;
            if (registryAware &&
                !await _mutationFence.CanMutateAsync(cancellationToken).ConfigureAwait(false))
            {
                return PublishRegistryStateUnavailable(current);
            }
            VersionSourceRegistryState? registryState = registryAware
                ? new(
                    state.SourceRegistryState?.AcceptedRevision ?? 0,
                    state.SourceRegistryState?.AcceptedDigest,
                    isManualPin: true)
                : null;
            string committedSource = registryAware
                ? VersionManagerState.NormalizeRegistrySource(
                    sourceRoot,
                    requireAlreadyNormalized: false)
                : sourceRoot;
            state = state.WithUpdateSource(committedSource, registryState);
            if (!await TrySaveAsync(state, cancellationToken).ConfigureAwait(false))
            {
                return registryAware
                    ? PublishRegistryStateUnavailable(current)
                    : PublishStateUnavailable();
            }
            _current = current with
            {
                State = state,
                Catalog = null,
                VerifiedCandidate = null,
                SourceStatus = VersionSourceStatus.Offline,
                CatalogIssue = null,
                ShouldPromptForUpdate = false,
                RegistryStatus = registryAware
                    ? VersionRegistryStatus.ManualPin
                    : VersionRegistryStatus.NotConfigured,
                RegistryIssue = UpdateSourceRegistryIssue.None,
            };
        }
        finally
        {
            _ = _mutation.Release();
        }
        return await CheckAsync(isAutomatic: false, cancellationToken).ConfigureAwait(false);
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
            using VersionManagerWriteLeaseResult lease = await AcquireWriteLeaseAsync(
                TimeSpan.Zero,
                cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                VersionManagementSnapshot unavailable = PublishStateUnavailable();
                return new(
                    new(ManagedVersionDeleteBlock.RecoveryRequired, RequiresRollbackLossWarning: false),
                    VersionDeleteOperationIssue.StateUnavailable,
                    RepositoryIssue: null,
                    unavailable);
            }
            VersionManagementSnapshot current = await ReloadDurableCurrentWithoutLockAsync(cancellationToken)
                .ConfigureAwait(false);
            if (current.State is not { } state)
            {
                return new(
                    new(ManagedVersionDeleteBlock.RecoveryRequired, RequiresRollbackLossWarning: false),
                    VersionDeleteOperationIssue.StateUnavailable,
                    RepositoryIssue: null,
                    current);
            }
            if (current.InventoryIssue != ManagedVersionInventoryReadIssue.None)
            {
                return new(
                    new(ManagedVersionDeleteBlock.RecoveryRequired, RequiresRollbackLossWarning: false),
                    VersionDeleteOperationIssue.StateUnavailable,
                    RepositoryIssue: null,
                    current);
            }
            if (state.PendingActivation is not null)
            {
                return new(
                    new(ManagedVersionDeleteBlock.RecoveryRequired, RequiresRollbackLossWarning: false),
                    VersionDeleteOperationIssue.StateUnavailable,
                    RepositoryIssue: null,
                    current);
            }
            if (state.PendingMutation is not null)
            {
                state = await ReconcilePendingMutationAsync(state, cancellationToken).ConfigureAwait(false);
                ManagedVersionInventoryReadResult recoveredInventory = await InventoryAsync(
                    state,
                    cancellationToken).ConfigureAwait(false);
                current = WithInventory(current, state, recoveredInventory);
                _current = current;
                if (!recoveredInventory.IsSuccess || state.PendingMutation is not null)
                {
                    return new(
                        new(ManagedVersionDeleteBlock.RecoveryRequired, RequiresRollbackLossWarning: false),
                        VersionDeleteOperationIssue.StateUnavailable,
                        RepositoryIssue: null,
                        current);
                }
            }
            ManagedVersionInventoryReadResult inventoryResult = await InventoryAsync(
                state,
                cancellationToken).ConfigureAwait(false);
            if (!inventoryResult.IsSuccess)
            {
                VersionManagementSnapshot unavailable = PublishInventoryUnavailable(state);
                return new(
                    new(ManagedVersionDeleteBlock.RecoveryRequired, RequiresRollbackLossWarning: false),
                    VersionDeleteOperationIssue.StateUnavailable,
                    RepositoryIssue: null,
                    unavailable);
            }
            ManagedVersionInventory inventory = inventoryResult.Inventory!;
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
            VersionManagerState prepared = state.WithPendingMutation(
                new(ManagedVersionMutationKind.Delete, admission));
            if (!await TrySaveAsync(prepared, cancellationToken).ConfigureAwait(false))
            {
                return new(
                    decision,
                    VersionDeleteOperationIssue.StateUnavailable,
                    RepositoryIssue: null,
                    current with { Inventory = inventory });
            }
            state = prepared;
            ManagedVersionDeleteIssue deleteIssue = await _repository.DeleteAsync(
                _managedRoot,
                admission,
                state.ActiveVersion,
                cancellationToken).ConfigureAwait(false);
            bool filesystemDeleteCommitted = deleteIssue is
                ManagedVersionDeleteIssue.None or ManagedVersionDeleteIssue.NotInstalled;
            if (filesystemDeleteCommitted)
            {
                state = CommitDelete(state, admission);
            }
            else
            {
                VersionManagerState cleared = state.WithPendingMutation(null);
                if (!await TrySaveAsync(cleared, cancellationToken).ConfigureAwait(false))
                {
                    ManagedVersionInventoryReadResult preparedInventory = await InventoryAsync(
                        prepared,
                        cancellationToken).ConfigureAwait(false);
                    _current = WithInventory(current, prepared, preparedInventory);
                    return new(
                        decision,
                        VersionDeleteOperationIssue.StateUnavailable,
                        deleteIssue,
                        _current);
                }
                state = cleared;
                ManagedVersionInventoryReadResult clearedInventory = await InventoryAsync(
                    state,
                    cancellationToken).ConfigureAwait(false);
                _current = WithInventory(current, state, clearedInventory);
                return new(
                    decision,
                    clearedInventory.IsSuccess
                        ? VersionDeleteOperationIssue.RepositoryFailure
                        : VersionDeleteOperationIssue.StateUnavailable,
                    deleteIssue,
                    _current);
            }
            inventoryResult = await InventoryAsync(state, cancellationToken).ConfigureAwait(false);
            if (!inventoryResult.IsSuccess)
            {
                _current = WithInventory(current, prepared, inventoryResult);
                return new(
                    decision,
                    VersionDeleteOperationIssue.StateUnavailable,
                    deleteIssue,
                    _current);
            }
            inventory = inventoryResult.Inventory!;
            state = ClearRetentionReviewIfAtOrBelowThreshold(state, inventory);
            if (!await TrySaveAsync(state, cancellationToken).ConfigureAwait(false))
            {
                VersionManagerState durablePrepared = prepared;
                ManagedVersionInventoryReadResult durableInventory = await InventoryAsync(
                    durablePrepared,
                    cancellationToken).ConfigureAwait(false);
                _current = WithInventory(current, durablePrepared, durableInventory);
                return new(
                    decision,
                    VersionDeleteOperationIssue.StateUnavailable,
                    deleteIssue,
                    _current);
            }
            _current = current with
            {
                State = state,
                Inventory = inventory,
                InventoryIssue = ManagedVersionInventoryReadIssue.None,
            };
            return new(
                decision,
                filesystemDeleteCommitted
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
            using VersionManagerWriteLeaseResult lease = await AcquireWriteLeaseAsync(
                TimeSpan.Zero,
                cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                return PublishStateUnavailable();
            }
            VersionManagementSnapshot current = await ReloadDurableCurrentWithoutLockAsync(cancellationToken)
                .ConfigureAwait(false);
            if (current.InventoryIssue != ManagedVersionInventoryReadIssue.None ||
                current.State is not { } state)
            {
                return current;
            }
            if (state.RetentionReviewDue)
            {
                state = state.WithRetentionReviewDue(retentionReviewDue: false);
                if (!await TrySaveAsync(state, cancellationToken).ConfigureAwait(false))
                {
                    return PublishStateUnavailable();
                }
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
            using VersionManagerWriteLeaseResult lease = await AcquireWriteLeaseAsync(
                TimeSpan.Zero,
                cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                throw new InvalidOperationException("Another version-management writer is active.");
            }
            VersionManagementSnapshot current = await ReloadDurableCurrentWithoutLockAsync(cancellationToken)
                .ConfigureAwait(false);
            VersionManagerState state = current.State ?? throw InvalidState();
            if (current.InventoryIssue != ManagedVersionInventoryReadIssue.None)
            {
                throw new InvalidOperationException("Installed-version inventory is unavailable.");
            }
            if (state.PendingMutation is not null)
            {
                throw new InvalidOperationException(
                    "A managed-version filesystem mutation still requires recovery.");
            }
            ManagedVersionInventoryReadResult inventoryResult = await InventoryAsync(
                state,
                cancellationToken).ConfigureAwait(false);
            if (!inventoryResult.IsSuccess)
            {
                _ = PublishInventoryUnavailable(state);
                throw new InvalidOperationException("Installed-version inventory is unavailable.");
            }
            ManagedVersionInventory inventory = inventoryResult.Inventory!;
            InstalledVersionSnapshot target = inventory.Find(version) ??
                throw new InvalidOperationException("Activation target is not installed.");
            if (target.AdmissionState != ManagedVersionAdmissionState.Admitted ||
                target.Integrity != ManagedVersionIntegrity.Healthy)
            {
                throw new InvalidOperationException("Activation target is damaged.");
            }
            state = VersionActivationPolicy.BeginActivation(state, version);
            await SaveOrThrowAsync(state, cancellationToken).ConfigureAwait(false);
            _current = current with { State = state, Inventory = inventory };
            return state;
        }
        finally
        {
            _ = _mutation.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<VersionManagementSnapshot> CancelPendingActivationAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using VersionManagerWriteLeaseResult lease = await AcquireWriteLeaseAsync(
                TimeSpan.Zero,
                cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                throw new InvalidOperationException("Another version-management writer is active.");
            }
            VersionManagementSnapshot current = await ReloadDurableCurrentWithoutLockAsync(cancellationToken)
                .ConfigureAwait(false);
            VersionManagerState state = current.State ?? throw InvalidState();
            state = VersionActivationPolicy.CancelRequestedActivation(state);
            await SaveOrThrowAsync(state, cancellationToken).ConfigureAwait(false);
            ManagedVersionInventoryReadResult inventoryResult = await InventoryAsync(
                state,
                cancellationToken).ConfigureAwait(false);
            _current = WithInventory(current, state, inventoryResult);
            return _current;
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

}
