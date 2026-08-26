using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Projects the strict launcher journal for app mutation policy without adding a writer lease.</summary>
public sealed class JsonLauncherMutationFence : ILauncherMutationFence
{
    private readonly JsonLauncherBootstrapStateStore _store;

    /// <summary>Creates a fence derived injectively from the exact app-state path.</summary>
    public JsonLauncherMutationFence(string statePath)
    {
        _store = new JsonLauncherBootstrapStateStore(statePath);
    }

    /// <inheritdoc />
    public async ValueTask<LauncherMutationProtection> LoadAsync(CancellationToken cancellationToken)
    {
        LauncherBootstrapStateLoadResult loaded = await _store.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Issue == LauncherBootstrapStateLoadIssue.Missing)
        {
            return Clear();
        }
        if (!loaded.IsSuccess)
        {
            return Failure(Map(loaded.Issue));
        }

        LauncherBootstrapState state = loaded.State!;
        ManagedVersionAdmission[] pending = state.Pending is null
            ? []
            :
            [..
            new[]
            {
                state.Pending.Candidate,
                state.Pending.PreviousActive,
                state.Pending.PreviousLastKnownGood,
            }
            .Where(identity => identity is not null)
            .Select(identity => ToAdmission(identity!))
            .Distinct()];
        return new(
            LauncherMutationFenceIssue.None,
            state.Pending is not null,
            state.Active is null ? null : ToAdmission(state.Active),
            state.LastKnownGood is null ? null : ToAdmission(state.LastKnownGood),
            pending);
    }

    /// <inheritdoc />
    public async ValueTask<LauncherMutationFenceIssue> RetireLastKnownGoodOwnerAsync(
        ManagedVersionAdmission expectedOwner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedOwner);
        LauncherBootstrapStateLoadResult loaded = await _store.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!loaded.IsSuccess || loaded.State is not { } state)
        {
            return loaded.Issue == LauncherBootstrapStateLoadIssue.Missing
                ? LauncherMutationFenceIssue.Invalid
                : Map(loaded.Issue);
        }
        if (state.Pending is not null ||
            state.Active is null ||
            state.LastKnownGood is null ||
            ToAdmission(state.Active) == expectedOwner ||
            ToAdmission(state.LastKnownGood) != expectedOwner)
        {
            return LauncherMutationFenceIssue.Invalid;
        }

        LauncherBootstrapState retired = LauncherBootstrapState.Create(
            state.ManagedRootIdentity,
            state.Active,
            state.Active,
            pending: null,
            state.Failed);
        LauncherBootstrapStateSaveResult saved = await _store.TrySaveAsync(retired, cancellationToken)
            .ConfigureAwait(false);
        return saved.IsSuccess
            ? LauncherMutationFenceIssue.None
            : LauncherMutationFenceIssue.Unavailable;
    }

    private static LauncherMutationProtection Clear()
    {
        return new(
            LauncherMutationFenceIssue.None,
            HasPendingActivation: false,
            ActiveOwner: null,
            LastKnownGoodOwner: null,
            PendingOwners: []);
    }

    private static LauncherMutationProtection Failure(LauncherMutationFenceIssue issue)
    {
        return new(issue, HasPendingActivation: false, null, null, []);
    }

    private static ManagedVersionAdmission ToAdmission(ManagedLauncherIdentity identity)
    {
        return new(identity.OwnerAppVersion, identity.OwnerAdmissionIdentity, identity.OwnerReleaseManifestSha256);
    }

    private static LauncherMutationFenceIssue Map(LauncherBootstrapStateLoadIssue issue)
    {
        return issue == LauncherBootstrapStateLoadIssue.Invalid
            ? LauncherMutationFenceIssue.Invalid
            : LauncherMutationFenceIssue.Unavailable;
    }
}
