namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Stable result from first-run managed-state seeding.</summary>
public enum ManagedVersionSeedOutcome
{
    /// <summary>A valid per-user managed state already exists.</summary>
    ExistingState,
    /// <summary>The explicit packaged seed was verified and persisted.</summary>
    Seeded,
    /// <summary>No per-user state or packaged seed exists.</summary>
    MissingSeed,
    /// <summary>Existing per-user state is invalid or unavailable and was not overwritten.</summary>
    InvalidExistingState,
    /// <summary>The packaged seed state is malformed or violates the first-run shape.</summary>
    InvalidSeed,
    /// <summary>The seeded payload is absent, damaged, or contains unexpected managed versions.</summary>
    DamagedSeedPayload,
    /// <summary>The verified seed could not be durably committed.</summary>
    StateUnavailable,
    /// <summary>The durable destination belongs to another root or lacks its required binding.</summary>
    ManagedRootMismatch,
}

/// <summary>Creates first-run launcher state only from one explicit verified packaged seed.</summary>
public sealed class ManagedVersionSeedBootstrapper
{
    private static readonly TimeSpan WriterLeaseTimeout = TimeSpan.FromSeconds(5);
    private readonly IVersionManagerStateStore _destinationStateStore;
    private readonly string _managedRoot;
    private readonly IManagedVersionRepository _repository;
    private readonly IVersionManagerStateStore _seedStateStore;

    /// <summary>Creates a bootstrapper over explicit destination and seed state ports.</summary>
    public ManagedVersionSeedBootstrapper(
        string managedRoot,
        IVersionManagerStateStore destinationStateStore,
        IVersionManagerStateStore seedStateStore,
        IManagedVersionRepository repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        _managedRoot = ManagedRootPathIdentity.Normalize(managedRoot);
        _destinationStateStore = destinationStateStore ??
            throw new ArgumentNullException(nameof(destinationStateStore));
        _seedStateStore = seedStateStore ?? throw new ArgumentNullException(nameof(seedStateStore));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>Ensures state exists without replacing malformed or unavailable user state.</summary>
    public async ValueTask<ManagedVersionSeedOutcome> EnsureInitializedAsync(
        CancellationToken cancellationToken)
    {
        using VersionManagerWriteLeaseResult lease = await _destinationStateStore.TryAcquireWriteLeaseAsync(
            WriterLeaseTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            return ManagedVersionSeedOutcome.StateUnavailable;
        }
        VersionManagerStateLoadResult existing = await _destinationStateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.IsSuccess)
        {
            return existing.State!.IsBoundToManagedRoot(_managedRoot)
                ? ManagedVersionSeedOutcome.ExistingState
                : ManagedVersionSeedOutcome.ManagedRootMismatch;
        }
        if (existing.Issue != VersionManagerStateLoadIssue.Missing)
        {
            return ManagedVersionSeedOutcome.InvalidExistingState;
        }

        VersionManagerStateLoadResult seed = await _seedStateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!seed.IsSuccess)
        {
            return seed.Issue == VersionManagerStateLoadIssue.Missing
                ? ManagedVersionSeedOutcome.MissingSeed
                : ManagedVersionSeedOutcome.InvalidSeed;
        }
        VersionManagerState state = seed.State!;
        if (!IsCanonicalFirstRunSeed(state))
        {
            return ManagedVersionSeedOutcome.InvalidSeed;
        }

        ManagedVersionInventory inventory = await _repository.InventoryAsync(
            _managedRoot,
            state.Admissions,
            state.ActiveVersion,
            state.LastKnownGoodVersion,
            state.FailedActivationVersion,
            cancellationToken).ConfigureAwait(false);
        if (inventory.Versions.Count != 1 ||
            inventory.HealthyCount != 1 ||
            inventory.DamagedCount != 0)
        {
            return ManagedVersionSeedOutcome.DamagedSeedPayload;
        }

        VersionManagerState bound = state.BindToManagedRoot(_managedRoot);
        VersionManagerStateSaveResult saved = await _destinationStateStore.TrySaveAsync(
            bound,
            cancellationToken).ConfigureAwait(false);
        return saved.IsSuccess
            ? ManagedVersionSeedOutcome.Seeded
            : ManagedVersionSeedOutcome.StateUnavailable;
    }

    private static bool IsCanonicalFirstRunSeed(VersionManagerState state)
    {
        ManagedVersionAdmission? admission = state.Admissions.Count == 1
            ? state.Admissions[0]
            : null;
        return state.UpdateSource is null &&
               state.ManagedRootIdentity is null &&
               state.ActiveVersion is { } active &&
               state.LastKnownGoodVersion == active &&
               admission?.Version == active &&
               state.PendingActivation is null &&
               state.PendingMutation is null &&
               state.FailedActivationVersion is null &&
               !state.RetentionReviewDue;
    }
}
