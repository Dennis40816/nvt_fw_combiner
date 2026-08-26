using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies compatible v1/v2 version-manager state persistence.</summary>
public sealed class JsonVersionManagerStateStoreRegistryTests
{
    private const string Digest =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>Registry authority round-trips atomically using schema version two.</summary>
    [Fact]
    public async Task RegistryAuthorityRoundTripsAtomicallyAsStateSchemaTwo()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("state/version-manager.v1.json");
        var store = new JsonVersionManagerStateStore(path);
        ManagedAppVersion version = ManagedAppVersion.Parse("1.0.0");
        VersionManagerState state = VersionManagerState.Create(
            "X:\\manual-source",
            version,
            version,
            [new(version, "identity-1.0.0", new string('b', 64))],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "X:\\managed-root",
            sourceRegistryState: new(42, Digest, isManualPin: true));

        await store.SaveAsync(state, TestContext.Current.CancellationToken);
        VersionManagerStateLoadResult loaded = await store.LoadAsync(
            TestContext.Current.CancellationToken);
        string json = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        Assert.True(loaded.IsSuccess);
        Assert.Equal(new VersionSourceRegistryState(42, Digest, isManualPin: true),
            loaded.State!.SourceRegistryState);
        Assert.Contains("\"schemaVersion\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"sourceRegistryState\"", json, StringComparison.Ordinal);
    }

    /// <summary>Ordinary legacy-state writes remain version one until a registry mutation.</summary>
    [Fact]
    public async Task ExistingSchemaOneStateRemainsReadableAndUnchangedUntilRegistryMutation()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write(
            "version-manager.v1.json",
            """
            {
              "schemaVersion": 1,
              "updateSource": "X:\\source",
              "activeVersion": "1.0.0",
              "lastKnownGoodVersion": "1.0.0",
              "admissions": [{
                "version": "1.0.0",
                "admissionIdentity": "identity-1.0.0",
                "releaseManifestSha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
              }],
              "pendingActivation": null,
              "failedActivationVersion": null,
              "retentionReviewDue": false,
              "managedRootIdentity": "X:\\managed-root"
            }
            """u8.ToArray());
        var store = new JsonVersionManagerStateStore(path);

        VersionManagerStateLoadResult result = await store.LoadAsync(
            TestContext.Current.CancellationToken);
        await store.SaveAsync(result.State!, TestContext.Current.CancellationToken);
        string rewritten = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.State!.SourceRegistryState);
        Assert.Contains("\"schemaVersion\": 1", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceRegistryState", rewritten, StringComparison.Ordinal);
    }

    /// <summary>Invalid schema and registry-state combinations fail closed.</summary>
    [Theory]
    [InlineData(1, -1, Digest)]
    [InlineData(1, 1, Digest)]
    [InlineData(2, 1, null)]
    [InlineData(2, 1, "ABC")]
    [InlineData(2, 0, null)]
    [InlineData(2, 1, Digest)]
    public async Task InvalidSchemaRegistryCombinationsFailClosed(
        int schemaVersion,
        long revision,
        string? digest)
    {
        using var workspace = TempWorkspace.Create();
        string digestJson = digest is null ? "null" : $"\"{digest}\"";
        string path = workspace.Write(
            "version-manager.v1.json",
            System.Text.Encoding.UTF8.GetBytes($$"""
            {
              "schemaVersion": {{schemaVersion}},
              "updateSource": null,
              "activeVersion": null,
              "lastKnownGoodVersion": null,
              "admissions": [],
              "pendingActivation": null,
              "failedActivationVersion": null,
              "retentionReviewDue": false,
              "managedRootIdentity": "X:\\managed-root",
              "sourceRegistryState": {
                "acceptedRevision": {{revision}},
                "acceptedDigest": {{digestJson}},
                "isManualPin": false
              }
            }
            """));

        VersionManagerStateLoadResult result = await new JsonVersionManagerStateStore(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(VersionManagerStateLoadIssue.Invalid, result.Issue);
        Assert.Null(result.State);
    }

    /// <summary>Revision zero is accepted only for a manual pin with a normalized source.</summary>
    [Fact]
    public async Task FirstManualPinRoundTripsWithZeroRevisionAndNullDigest()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("state/version-manager.v1.json");
        var store = new JsonVersionManagerStateStore(path);
        VersionManagerState state = VersionManagerState.Create(
            "X:\\manual-source",
            activeVersion: null,
            lastKnownGoodVersion: null,
            admissions: [],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "X:\\managed-root",
            sourceRegistryState: new(0, null, isManualPin: true));

        await store.SaveAsync(state, TestContext.Current.CancellationToken);
        VersionManagerStateLoadResult loaded = await store.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.True(loaded.IsSuccess);
        Assert.Equal(new VersionSourceRegistryState(0, null, isManualPin: true),
            loaded.State!.SourceRegistryState);
        Assert.Equal("X:\\manual-source", loaded.State.UpdateSource);
    }

    /// <summary>Automatic revision zero is invalid even when an effective source is present.</summary>
    [Fact]
    public async Task AutomaticRegistryStateRejectsZeroRevisionWithNormalizedSource()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write(
            "version-manager.v1.json",
            """
            {
              "schemaVersion": 2,
              "updateSource": "X:\\source",
              "activeVersion": null,
              "lastKnownGoodVersion": null,
              "admissions": [],
              "pendingActivation": null,
              "failedActivationVersion": null,
              "retentionReviewDue": false,
              "managedRootIdentity": "X:\\managed-root",
              "sourceRegistryState": {
                "acceptedRevision": 0,
                "acceptedDigest": null,
                "isManualPin": false
              }
            }
            """u8.ToArray());

        VersionManagerStateLoadResult result = await new JsonVersionManagerStateStore(path)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionManagerStateLoadIssue.Invalid, result.Issue);
        Assert.Null(result.State);
    }
}
