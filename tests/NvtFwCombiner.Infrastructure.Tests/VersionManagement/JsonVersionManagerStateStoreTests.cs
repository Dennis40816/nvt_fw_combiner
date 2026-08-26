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
            retentionReviewDue: true,
            managedRootIdentity: "X:\\managed-root");

        await store.SaveAsync(state, TestContext.Current.CancellationToken);
        VersionManagerStateLoadResult loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(loaded.IsSuccess);
        Assert.Equal(state.UpdateSource, loaded.State!.UpdateSource);
        Assert.Equal(state.ActiveVersion, loaded.State.ActiveVersion);
        Assert.Equal(state.LastKnownGoodVersion, loaded.State.LastKnownGoodVersion);
        Assert.Equal(state.Admissions, loaded.State.Admissions);
        Assert.Equal(Path.GetFullPath("X:\\managed-root"), loaded.State.ManagedRootIdentity);
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

    /// <summary>A genuinely absent state remains distinguishable from malformed or unreadable state.</summary>
    [Fact]
    public async Task MissingStateReturnsStableMissingIssue()
    {
        using var workspace = TempWorkspace.Create();

        VersionManagerStateLoadResult result = await new JsonVersionManagerStateStore(
            workspace.PathFor("state/version-manager.v1.json")).LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(VersionManagerStateLoadIssue.Missing, result.Issue);
        Assert.Null(result.State);
    }

    /// <summary>Legacy state remains readable only as an unbound template; durable writes require an explicit binding.</summary>
    [Fact]
    public async Task LegacyUnboundStateLoadsButCannotBeWrittenAsDurableState()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write(
            "version-manager.v1.json",
            System.Text.Encoding.UTF8.GetBytes(BaseState("[]", activeVersion: null)));
        var store = new JsonVersionManagerStateStore(path);

        VersionManagerStateLoadResult loaded = await store.LoadAsync(TestContext.Current.CancellationToken);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.SaveAsync(loaded.State!, TestContext.Current.CancellationToken));

        Assert.True(loaded.IsSuccess);
        Assert.Null(loaded.State!.ManagedRootIdentity);
    }

    /// <summary>Unknown fields, unsupported schema, and inconsistent active references all fail closed.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("schema")]
    [InlineData("unadmitted-active")]
    [InlineData("duplicate-admission")]
    [InlineData("duplicate-property")]
    [InlineData("mismatched-delete-journal")]
    [InlineData("unnormalized-root")]
    public async Task InvalidStateShapesNeverPublishPartialState(string shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        using var workspace = TempWorkspace.Create();
        string admission = """
            {
              "version": "0.10.6",
              "admissionIdentity": "identity-0.10.6",
              "releaseManifestSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            }
            """;
        string json = shape switch
        {
            "unknown" => BaseState($"[{admission}]", "0.10.6")
                .Replace("\"retentionReviewDue\": false", "\"retentionReviewDue\": false, \"unknown\": true", StringComparison.Ordinal),
            "schema" => BaseState($"[{admission}]", "0.10.6")
                .Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal),
            "unadmitted-active" => BaseState("[]", "0.10.6"),
            "duplicate-admission" => BaseState($"[{admission}, {admission}]", "0.10.6"),
            "duplicate-property" => BaseState($"[{admission}]", "0.10.6")
                .Replace(
                    "\"retentionReviewDue\": false",
                    "\"retentionReviewDue\": true, \"retentionReviewDue\": false",
                    StringComparison.Ordinal),
            "mismatched-delete-journal" => BaseState($"[{admission}]", "0.10.6")
                .Replace(
                    "\"retentionReviewDue\": false",
                    """
                    "retentionReviewDue": false,
                    "pendingMutation": {
                      "kind": "Delete",
                      "admission": {
                        "version": "0.10.6",
                        "admissionIdentity": "different-identity",
                        "releaseManifestSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                      }
                    }
                    """,
                    StringComparison.Ordinal),
            "unnormalized-root" => BaseState($"[{admission}]", "0.10.6")
                .Replace(
                    "\"retentionReviewDue\": false",
                    "\"retentionReviewDue\": false, \"managedRootIdentity\": \"relative-root\"",
                    StringComparison.Ordinal),
            _ => throw new InvalidOperationException($"Unknown state shape '{shape}'."),
        };
        string path = workspace.Write("version-manager.v1.json", System.Text.Encoding.UTF8.GetBytes(json));

        VersionManagerStateLoadResult result = await new JsonVersionManagerStateStore(path).LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(VersionManagerStateLoadIssue.Invalid, result.Issue);
        Assert.Null(result.State);
    }

    /// <summary>An oversized state is rejected before allocating its untrusted declared length.</summary>
    [Fact]
    public async Task OversizedStateFailsClosed()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("version-manager.v1.json", new byte[(1024 * 1024) + 1]);

        VersionManagerStateLoadResult result = await new JsonVersionManagerStateStore(path).LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionManagerStateLoadIssue.Invalid, result.Issue);
        Assert.Null(result.State);
    }

    /// <summary>A cancelled atomic replacement preserves the prior complete state and removes its temporary file.</summary>
    [Fact]
    public async Task CancelledSavePreservesPriorStateAndCleansTemporaryFile()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("state/version-manager.v1.json");
        var store = new JsonVersionManagerStateStore(path);
        ManagedAppVersion version = ManagedAppVersion.Parse("0.10.6");
        var admission = new ManagedVersionAdmission(version, "identity-0.10.6", new string('a', 64));
        VersionManagerState original = VersionManagerState.Create(
            "X:\\original",
            version,
            version,
            [admission],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            managedRootIdentity: "X:\\managed-root");
        VersionManagerState replacement = VersionManagerState.Create(
            "Y:\\replacement",
            version,
            version,
            [admission],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: true,
            managedRootIdentity: "X:\\managed-root");
        await store.SaveAsync(original, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await store.SaveAsync(replacement, cancellation.Token));
        VersionManagerStateLoadResult loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(original.UpdateSource, loaded.State!.UpdateSource);
        Assert.False(loaded.State.RetentionReviewDue);
        Assert.Empty(Directory.EnumerateFiles(
            Path.GetDirectoryName(path)!,
            ".version-manager.v1.json.*.tmp"));
    }

    /// <summary>Durable activation and filesystem transaction phases survive exact JSON round trips.</summary>
    [Fact]
    public async Task DurableTransactionPhasesRoundTrip()
    {
        using var workspace = TempWorkspace.Create();
        var store = new JsonVersionManagerStateStore(workspace.PathFor("state/version-manager.v1.json"));
        ManagedAppVersion active = ManagedAppVersion.Parse("0.10.5");
        ManagedAppVersion candidate = ManagedAppVersion.Parse("0.10.6");
        var activeAdmission = new ManagedVersionAdmission(active, "identity-0.10.5", new string('a', 64));
        var candidateAdmission = new ManagedVersionAdmission(candidate, "identity-0.10.6", new string('b', 64));
        VersionManagerState installPrepared = VersionManagerState.Create(
            "X:\\source",
            active,
            active,
            [activeAdmission],
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false,
            new(ManagedVersionMutationKind.Install, candidateAdmission),
            managedRootIdentity: "X:\\managed-root");

        await store.SaveAsync(installPrepared, TestContext.Current.CancellationToken);
        VersionManagerStateLoadResult loadedInstall = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(installPrepared.PendingMutation, loadedInstall.State!.PendingMutation);

        VersionManagerState activationRecorded = VersionActivationPolicy.RecordRollbackLaunch(
            VersionActivationPolicy.RecordCandidateLaunch(
                VersionActivationPolicy.BeginActivation(
                    VersionManagerState.Create(
                        null,
                        active,
                        active,
                        [activeAdmission, candidateAdmission],
                        null,
                        null,
                        false,
                        managedRootIdentity: "X:\\managed-root"),
                    candidate)),
            candidate).State;
        await store.SaveAsync(activationRecorded, TestContext.Current.CancellationToken);
        VersionManagerStateLoadResult loadedActivation = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            VersionActivationPhase.RollbackLaunchRecorded,
            loadedActivation.State!.PendingActivation?.Phase);
        Assert.Equal(candidate, loadedActivation.State.FailedActivationVersion);

        VersionManagerState activeRecorded = VersionActivationPolicy.RecordActiveLaunch(
            VersionManagerState.Create(
                null,
                active,
                active,
                [activeAdmission, candidateAdmission],
                null,
                null,
                false,
                managedRootIdentity: "X:\\managed-root"));
        await store.SaveAsync(activeRecorded, TestContext.Current.CancellationToken);
        VersionManagerStateLoadResult loadedActive = await store.LoadAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            VersionActivationPhase.ActiveLaunchRecorded,
            loadedActive.State!.PendingActivation?.Phase);
        Assert.Equal(active, loadedActive.State.PendingActivation?.CandidateVersion);
    }

    /// <summary>Idle and requested state retain the pre-journal wire shape for older installed apps.</summary>
    [Fact]
    public async Task OptionalTransactionFieldsAreAbsentUntilARecordedPhaseNeedsThem()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("state/version-manager.v1.json");
        var store = new JsonVersionManagerStateStore(path);
        ManagedAppVersion active = ManagedAppVersion.Parse("0.10.5");
        ManagedAppVersion candidate = ManagedAppVersion.Parse("0.10.6");
        VersionManagerState requested = VersionActivationPolicy.BeginActivation(
            VersionManagerState.Create(
                null,
                active,
                active,
                [
                    new(active, "identity-0.10.5", new string('a', 64)),
                    new(candidate, "identity-0.10.6", new string('b', 64)),
                ],
                pendingActivation: null,
                failedActivationVersion: null,
                retentionReviewDue: false,
                managedRootIdentity: "X:\\managed-root"),
            candidate);

        await store.SaveAsync(requested, TestContext.Current.CancellationToken);
        string json = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("pendingMutation", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"phase\"", json, StringComparison.Ordinal);
        Assert.True((await store.LoadAsync(TestContext.Current.CancellationToken)).IsSuccess);
    }

    private static string BaseState(string admissions, string? activeVersion)
    {
        string active = activeVersion is null ? "null" : $"\"{activeVersion}\"";
        return $$"""
            {
              "schemaVersion": 1,
              "updateSource": null,
              "activeVersion": {{active}},
              "lastKnownGoodVersion": {{active}},
              "admissions": {{admissions}},
              "pendingActivation": null,
              "failedActivationVersion": null,
              "retentionReviewDue": false
            }
            """;
    }
}
