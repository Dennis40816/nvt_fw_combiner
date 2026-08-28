using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies launcher-owner projection and durable rollback retirement.</summary>
public sealed class JsonLauncherMutationFenceTests
{
    /// <summary>Read-only protection returns exact admissions without changing journal bytes.</summary>
    [Fact]
    public async Task LoadProjectsExactOwnersWithoutMutation()
    {
        using var workspace = TempWorkspace.Create();
        string statePath = Path.Combine(workspace.Root, "version-state.json");
        var store = new JsonLauncherBootstrapStateStore(statePath);
        ManagedLauncherIdentity active = Identity("1.0.0", 'a');
        ManagedLauncherIdentity candidate = Identity("1.0.1", 'b');
        LauncherBootstrapState state = LauncherBootstrapState.Create(
            workspace.Root,
            active,
            active,
            PendingLauncherActivation.Create(
                candidate,
                active,
                active,
                LauncherActivationPhase.Requested),
            failed: null);
        Assert.True((await store.TrySaveAsync(state, TestContext.Current.CancellationToken)).IsSuccess);
        string journalPath = JsonLauncherBootstrapStateStore.DerivePath(statePath);
        byte[] before = await File.ReadAllBytesAsync(journalPath, TestContext.Current.CancellationToken);

        LauncherMutationProtection result = await new JsonLauncherMutationFence(statePath)
            .LoadAsync(TestContext.Current.CancellationToken);
        byte[] after = await File.ReadAllBytesAsync(journalPath, TestContext.Current.CancellationToken);

        Assert.True(result.HasPendingActivation);
        Assert.Equal(ToAdmission(active), result.ActiveOwner);
        Assert.Contains(ToAdmission(candidate), result.PendingOwners);
        Assert.Equal(before, after);
    }

    /// <summary>Confirmed LKG retirement rehomes rollback to active under the same journal.</summary>
    [Fact]
    public async Task RetireLastKnownGoodRehomesToActive()
    {
        using var workspace = TempWorkspace.Create();
        string statePath = Path.Combine(workspace.Root, "version-state.json");
        var store = new JsonLauncherBootstrapStateStore(statePath);
        ManagedLauncherIdentity active = Identity("1.0.1", 'b');
        ManagedLauncherIdentity rollback = Identity("1.0.0", 'a');
        Assert.True((await store.TrySaveAsync(
            LauncherBootstrapState.Create(workspace.Root, active, rollback, pending: null, failed: null),
            TestContext.Current.CancellationToken)).IsSuccess);
        var fence = new JsonLauncherMutationFence(statePath);

        LauncherMutationFenceIssue result = await fence.RetireLastKnownGoodOwnerAsync(
            ToAdmission(rollback),
            TestContext.Current.CancellationToken);
        LauncherBootstrapStateLoadResult reloaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LauncherMutationFenceIssue.None, result);
        Assert.Equal(active, reloaded.State!.Active);
        Assert.Equal(active, reloaded.State.LastKnownGood);
    }

    /// <summary>Active or pending ownership cannot be retired through the LKG-only command.</summary>
    [Fact]
    public async Task RetireRejectsActiveOwner()
    {
        using var workspace = TempWorkspace.Create();
        string statePath = Path.Combine(workspace.Root, "version-state.json");
        var store = new JsonLauncherBootstrapStateStore(statePath);
        ManagedLauncherIdentity active = Identity("1.0.0", 'a');
        Assert.True((await store.TrySaveAsync(
            LauncherBootstrapState.Create(workspace.Root, active, active, pending: null, failed: null),
            TestContext.Current.CancellationToken)).IsSuccess);

        LauncherMutationFenceIssue result = await new JsonLauncherMutationFence(statePath)
            .RetireLastKnownGoodOwnerAsync(ToAdmission(active), TestContext.Current.CancellationToken);

        Assert.Equal(LauncherMutationFenceIssue.Invalid, result);
    }

    private static ManagedLauncherIdentity Identity(string version, char hash)
    {
        ManagedAppVersion owner = ManagedAppVersion.Parse(version);
        return ManagedLauncherIdentity.Create(
            owner,
            $"admission-{version}",
            new string('c', 64),
            ManagedAppVersion.Parse("1.0.0"),
            1,
            ManagedLauncherIdentity.ExecutablePath,
            123,
            new string(hash, 64));
    }

    private static ManagedVersionAdmission ToAdmission(ManagedLauncherIdentity identity)
    {
        return new(identity.OwnerAppVersion, identity.OwnerAdmissionIdentity, identity.OwnerReleaseManifestSha256);
    }
}
