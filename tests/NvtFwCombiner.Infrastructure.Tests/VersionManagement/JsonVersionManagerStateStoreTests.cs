using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Tests strict separate atomic persistence of launcher state.</summary>
public sealed class JsonVersionManagerStateStoreTests
{
    /// <summary>All managed identities survive an atomic round trip.</summary>
    [Fact]
    public async Task SaveAndLoadRoundTrip()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("state/version-manager.v1.json");
        var store = new JsonVersionManagerStateStore(path);
        ManagedAppVersion current = ManagedAppVersion.Parse("0.10.6");
        var admission = new ManagedVersionAdmission(current, "identity-0.10.6", new string('a', 64));
        VersionManagerState state = VersionManagerState.Create(
            "X:\\relocatable-update-source",
            current,
            current,
            [admission],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: true);

        await store.SaveAsync(state, TestContext.Current.CancellationToken);
        VersionManagerStateLoadResult loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(loaded.IsSuccess);
        Assert.Equal(state.UpdateSource, loaded.State!.UpdateSource);
        Assert.Equal(state.ActiveVersion, loaded.State.ActiveVersion);
        Assert.Equal(state.LastKnownGoodVersion, loaded.State.LastKnownGoodVersion);
        Assert.Equal(state.Admissions, loaded.State.Admissions);
        Assert.True(loaded.State.RetentionReviewDue);
        Assert.False(File.Exists(path + ".tmp"));
    }

    /// <summary>Malformed state never guesses an active version from directories.</summary>
    [Fact]
    public async Task MalformedStateFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("version-manager.v1.json", "{\"schemaVersion\":1,\"activeVersion\":\"0.10.6\"}"u8.ToArray());

        VersionManagerStateLoadResult result = await new JsonVersionManagerStateStore(path).LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(VersionManagerStateLoadIssue.Invalid, result.Issue);
        Assert.Null(result.State);
    }

    /// <summary>A JSON-null admission row is invalid data rather than a null-reference escape.</summary>
    [Fact]
    public async Task NullAdmissionEntryFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write(
            "version-manager.v1.json",
            """
            {
              "schemaVersion": 1,
              "updateSource": null,
              "activeVersion": null,
              "lastKnownGoodVersion": null,
              "admissions": [null],
              "pendingActivation": null,
              "failedActivationVersion": null,
              "retentionReviewDue": false
            }
            """u8.ToArray());

        VersionManagerStateLoadResult result = await new JsonVersionManagerStateStore(path).LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(VersionManagerStateLoadIssue.Invalid, result.Issue);
        Assert.Null(result.State);
    }
}
