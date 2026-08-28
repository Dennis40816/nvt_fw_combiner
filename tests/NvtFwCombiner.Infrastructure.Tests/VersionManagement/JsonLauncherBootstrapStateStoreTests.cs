using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies strict launcher journal persistence without a second lease.</summary>
public sealed class JsonLauncherBootstrapStateStoreTests
{
    /// <summary>Exact launcher identities and power phase survive one atomic round trip.</summary>
    [Fact]
    public async Task ExactStateRoundTrips()
    {
        using var workspace = TempWorkspace.Create();
        string statePath = Path.Combine(workspace.Root, "custom-state.json");
        var store = new JsonLauncherBootstrapStateStore(statePath);
        ManagedLauncherIdentity active = Identity("1.0.0", "1.0.0", 'a');
        ManagedLauncherIdentity candidate = Identity("1.0.1", "1.1.0", 'b');
        LauncherBootstrapState expected = LauncherBootstrapState.Create(
            workspace.Root,
            active,
            active,
            PendingLauncherActivation.Create(
                candidate,
                active,
                active,
                LauncherActivationPhase.CandidateLaunchRecorded),
            failed: null);

        LauncherBootstrapStateSaveResult saved = await store.TrySaveAsync(
            expected,
            TestContext.Current.CancellationToken);
        LauncherBootstrapStateLoadResult loaded = await store.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.True(saved.IsSuccess);
        Assert.True(loaded.IsSuccess);
        Assert.Equal(expected.ManagedRootIdentity, loaded.State!.ManagedRootIdentity);
        Assert.Equal(expected.Active, loaded.State.Active);
        Assert.Equal(expected.LastKnownGood, loaded.State.LastKnownGood);
        Assert.Equal(expected.Pending, loaded.State.Pending);
        Assert.Null(loaded.State.Failed);
    }

    /// <summary>An ordinary active-attempt guard survives process restart exactly.</summary>
    [Fact]
    public async Task ActiveLaunchGuardRoundTrips()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JsonLauncherBootstrapStateStore(
            Path.Combine(workspace.Root, "custom-state.json"));
        ManagedLauncherIdentity active = Identity("1.0.0", "1.0.0", 'a');
        LauncherBootstrapState expected = LauncherBootstrapState.Create(
            workspace.Root,
            active,
            active,
            pending: null,
            failed: null).RecordActiveLaunch();

        LauncherBootstrapStateSaveResult saved = await store.TrySaveAsync(
            expected,
            TestContext.Current.CancellationToken);
        LauncherBootstrapStateLoadResult loaded = await store.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.True(saved.IsSuccess);
        Assert.True(loaded.IsSuccess);
        Assert.Equal(LauncherActivationPhase.ActiveLaunchRecorded, loaded.State!.Pending?.Phase);
        Assert.Equal(active, loaded.State.Pending?.Candidate);
    }

    /// <summary>Distinct custom app-state paths always derive distinct launcher journals.</summary>
    [Fact]
    public void StatePathMappingIsInjective()
    {
        using var workspace = TempWorkspace.Create();
        string first = Path.Combine(workspace.Root, "a.json");
        string second = Path.Combine(workspace.Root, "b.json");

        string firstLauncher = JsonLauncherBootstrapStateStore.DerivePath(first);
        string secondLauncher = JsonLauncherBootstrapStateStore.DerivePath(second);

        Assert.NotEqual(firstLauncher, secondLauncher);
        Assert.Equal(Path.GetFullPath(first) + ".launcher-bootstrap.v1.json", firstLauncher);
        Assert.Equal(Path.GetFullPath(second) + ".launcher-bootstrap.v1.json", secondLauncher);
    }

    /// <summary>Unknown, duplicate, whitespace admission, and unstable versions fail closed.</summary>
    [Theory]
    [InlineData("\"unknown\":true,")]
    [InlineData("\"schemaVersion\":1,")]
    [InlineData("")]
    public async Task NonCanonicalStateIsRejected(string injected)
    {
        ArgumentNullException.ThrowIfNull(injected);
        using var workspace = TempWorkspace.Create();
        string statePath = Path.Combine(workspace.Root, "state.json");
        string launcherPath = JsonLauncherBootstrapStateStore.DerivePath(statePath);
        string ownerAdmission = injected.Length == 0 ? "   " : "admission";
        string ownerVersion = injected.Length == 0 ? "1.0.0-beta.1" : "1.0.0";
        string json = $$"""
            {
              {{injected}}
              "schemaVersion": 1,
              "managedRootIdentity": {{System.Text.Json.JsonSerializer.Serialize(workspace.Root)}},
              "active": {
                "ownerAppVersion": "{{ownerVersion}}",
                "ownerAdmissionIdentity": "{{ownerAdmission}}",
                "ownerReleaseManifestSha256": "{{new string('c', 64)}}",
                "launcherVersion": "1.0.0",
                "protocolVersion": 1,
                "executableRelativePath": "launcher/NvtFwCombiner.Launcher.exe",
                "size": 123,
                "sha256": "{{new string('a', 64)}}"
              },
              "lastKnownGood": {
                "ownerAppVersion": "{{ownerVersion}}",
                "ownerAdmissionIdentity": "{{ownerAdmission}}",
                "ownerReleaseManifestSha256": "{{new string('c', 64)}}",
                "launcherVersion": "1.0.0",
                "protocolVersion": 1,
                "executableRelativePath": "launcher/NvtFwCombiner.Launcher.exe",
                "size": 123,
                "sha256": "{{new string('a', 64)}}"
              },
              "pending": null,
              "failed": null
            }
            """;
        await File.WriteAllTextAsync(launcherPath, json, TestContext.Current.CancellationToken);
        var store = new JsonLauncherBootstrapStateStore(statePath);

        LauncherBootstrapStateLoadResult loaded = await store.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.False(loaded.IsSuccess);
        Assert.Equal(LauncherBootstrapStateLoadIssue.Invalid, loaded.Issue);
    }

    private static ManagedLauncherIdentity Identity(string owner, string launcher, char hash)
    {
        return ManagedLauncherIdentity.Create(
            ManagedAppVersion.Parse(owner),
            $"admission-{owner}",
            new string('c', 64),
            ManagedAppVersion.Parse(launcher),
            1,
            ManagedLauncherIdentity.ExecutablePath,
            123,
            new string(hash, 64));
    }
}
