using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

internal static class VersionManagementExperienceTestFactory
{
    internal static VersionManagementExperience Create(
        ManagedAppVersion currentAppVersion,
        string managedRoot,
        IVersionManagerStateStore stateStore,
        IUpdateCatalogSource catalogSource,
        IManagedVersionRepository repository,
        ILauncherMutationFence launcherFence)
    {
        return Create(
            currentAppVersion,
            managedRoot,
            stateStore,
            catalogSource,
            repository,
            sourceRegistry: null,
            launcherFence);
    }

    internal static VersionManagementExperience Create(
        ManagedAppVersion currentAppVersion,
        string managedRoot,
        IVersionManagerStateStore stateStore,
        IUpdateCatalogSource catalogSource,
        IManagedVersionRepository repository,
        IUpdateSourceRegistry? sourceRegistry = null,
        ILauncherMutationFence? launcherFence = null)
    {
        return new VersionManagementExperience(
            currentAppVersion,
            managedRoot,
            stateStore,
            catalogSource,
            repository,
            launcherFence ?? ClearLauncherMutationFence.Instance,
            sourceRegistry);
    }

    private sealed class ClearLauncherMutationFence : ILauncherMutationFence
    {
        internal static ClearLauncherMutationFence Instance { get; } = new();

        public ValueTask<LauncherMutationProtection> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new LauncherMutationProtection(
                LauncherMutationFenceIssue.None,
                HasPendingActivation: false,
                ActiveOwner: null,
                LastKnownGoodOwner: null,
                PendingOwners: []));
        }

        public ValueTask<LauncherMutationFenceIssue> RetireLastKnownGoodOwnerAsync(
            ManagedVersionAdmission expectedOwner,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(expectedOwner);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(LauncherMutationFenceIssue.None);
        }
    }
}
