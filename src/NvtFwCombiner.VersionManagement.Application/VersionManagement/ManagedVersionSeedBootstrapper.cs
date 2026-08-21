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
}

/// <summary>Creates first-run launcher state only from one explicit verified packaged seed.</summary>
public sealed class ManagedVersionSeedBootstrapper
{
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
        _managedRoot = managedRoot;
        _destinationStateStore = destinationStateStore ??
            throw new ArgumentNullException(nameof(destinationStateStore));
        _seedStateStore = seedStateStore ?? throw new ArgumentNullException(nameof(seedStateStore));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>Ensures state exists without replacing malformed or unavailable user state.</summary>
    public async ValueTask<ManagedVersionSeedOutcome> EnsureInitializedAsync(
        CancellationToken cancellationToken)
    {
        VersionManagerStateLoadResult existing = await _destinationStateStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.IsSuccess)
        {
            return ManagedVersionSeedOutcome.ExistingState;
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

        VersionManagerStateSaveResult saved = await _destinationStateStore.TrySaveAsync(
            state,
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
               state.ActiveVersion is { } active &&
               state.LastKnownGoodVersion == active &&
               admission?.Version == active &&
               state.PendingActivation is null &&
               state.PendingMutation is null &&
               state.FailedActivationVersion is null &&
               !state.RetentionReviewDue;
    }
}
