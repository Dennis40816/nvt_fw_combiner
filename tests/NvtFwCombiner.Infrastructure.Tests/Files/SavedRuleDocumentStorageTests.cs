using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Integration evidence for controlled Saved Rule document lifecycle writes.</summary>
public sealed class SavedRuleDocumentStorageTests
{
    /// <summary>Semantic Save-in-place overwrites local bytes and clears every prior gate.</summary>
    [Fact]
    public async Task LocalSemanticEditSavesInPlaceAndInvalidatesPublicationClaims()
    {
        using var workspace = TempWorkspace.Create("nfc-saved-rule-save");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        string rulePath = Path.Combine(authoringRoot, "rule.json");
        byte[] originalBytes = Document("before");
        await File.WriteAllBytesAsync(
            rulePath,
            originalBytes,
            TestContext.Current.CancellationToken);
        SavedRuleDocumentLifecycleUseCase useCase =
            CreateUseCase(authoringRoot, catalogRoot);
        var original = new SavedRuleLifecycleSnapshot(
            Identity(originalBytes),
            SavedRuleStorageKind.UserOwned,
            SavedRuleLifecycleState.Published,
            hasApproval: true,
            hasEvidence: true,
            isTrusted: true);
        byte[] editedBytes = Document("after");

        SavedRuleDocumentSaveResult result = await useCase.SaveAsync(
            new SavedRuleDocumentSaveRequest(
                original,
                rulePath,
                rulePath,
                editedBytes),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Decision.SemanticContentChanged);
        Assert.True(result.Decision.RequiresNewRuleVersionForPublication);
        Assert.Equal(editedBytes, await File.ReadAllBytesAsync(
            rulePath,
            TestContext.Current.CancellationToken));
        Assert.Equal(SavedRuleLifecycleState.Draft, result.Decision.Snapshot!.State);
        Assert.False(result.Decision.Snapshot.HasApproval);
        Assert.False(result.Decision.Snapshot.HasEvidence);
        Assert.False(result.Decision.Snapshot.IsTrusted);
        Assert.Equal("after", result.Decision.Snapshot.Identity.RuleId);
        Assert.Equal(DefaultParent, result.Decision.Snapshot.Identity.Parent);
    }

    /// <summary>Catalog Edit writes only a separate untrusted working copy.</summary>
    [Fact]
    public async Task CatalogEditLeavesInstalledBytesUntouchedAndCreatesWorkingCopy()
    {
        using var workspace = TempWorkspace.Create("nfc-saved-rule-copy");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        byte[] catalogBytes = Document("catalog");
        string catalogPath = Path.Combine(catalogRoot, "rule.json");
        await File.WriteAllBytesAsync(
            catalogPath,
            catalogBytes,
            TestContext.Current.CancellationToken);
        string workingCopyPath = Path.Combine(authoringRoot, "rule-working.json");
        SavedRuleDocumentLifecycleUseCase useCase =
            CreateUseCase(authoringRoot, catalogRoot);
        var original = new SavedRuleLifecycleSnapshot(
            Identity(catalogBytes),
            SavedRuleStorageKind.TrustedCatalog,
            SavedRuleLifecycleState.Published,
            hasApproval: true,
            hasEvidence: true,
            isTrusted: true);

        SavedRuleDocumentSaveResult result = await useCase.SaveAsync(
            new SavedRuleDocumentSaveRequest(
                original,
                catalogPath,
                workingCopyPath,
                catalogBytes),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(catalogBytes, await File.ReadAllBytesAsync(
            catalogPath,
            TestContext.Current.CancellationToken));
        Assert.Equal(catalogBytes, await File.ReadAllBytesAsync(
            workingCopyPath,
            TestContext.Current.CancellationToken));
        Assert.Equal(SavedRuleStorageKind.UserOwned, result.Decision.Snapshot!.StorageKind);
        Assert.Equal(SavedRuleLifecycleState.Draft, result.Decision.Snapshot.State);
        Assert.False(result.Decision.Snapshot.IsTrusted);
    }

    /// <summary>Host-resolved Catalog authority rejects overwrite even if a caller requests it.</summary>
    [Fact]
    public async Task CatalogSaveInPlaceIsRejectedWithoutChangingBytes()
    {
        using var workspace = TempWorkspace.Create("nfc-saved-rule-catalog-readonly");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        byte[] originalBytes = Document("installed");
        string catalogPath = Path.Combine(catalogRoot, "rule.json");
        await File.WriteAllBytesAsync(
            catalogPath,
            originalBytes,
            TestContext.Current.CancellationToken);
        SavedRuleDocumentLifecycleUseCase useCase =
            CreateUseCase(authoringRoot, catalogRoot);
        byte[] changedBytes = Document("changed");

        SavedRuleDocumentSaveResult result = await useCase.SaveAsync(
            new SavedRuleDocumentSaveRequest(
                new SavedRuleLifecycleSnapshot(
                    Identity(originalBytes),
                    SavedRuleStorageKind.TrustedCatalog,
                    SavedRuleLifecycleState.Published,
                    hasApproval: true,
                    hasEvidence: true,
                    isTrusted: true),
                catalogPath,
                catalogPath,
                changedBytes),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(
            "saved-rule.lifecycle.catalog-read-only",
            Assert.Single(result.Decision.Issues).Code);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(
            catalogPath,
            TestContext.Current.CancellationToken));
    }

    /// <summary>Caller lifecycle claims cannot reclassify a Catalog path as editable local storage.</summary>
    [Fact]
    public async Task HostResolvedStorageAuthorityRejectsCallerClassificationMismatch()
    {
        using var workspace = TempWorkspace.Create("nfc-saved-rule-authority");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        string catalogPath = Path.Combine(catalogRoot, "rule.json");
        byte[] originalBytes = Document("installed");
        await File.WriteAllBytesAsync(
            catalogPath,
            originalBytes,
            TestContext.Current.CancellationToken);
        SavedRuleDocumentLifecycleUseCase useCase =
            CreateUseCase(authoringRoot, catalogRoot);
        byte[] changedBytes = Document("changed");

        SavedRuleDocumentSaveResult result = await useCase.SaveAsync(
            new SavedRuleDocumentSaveRequest(
                new SavedRuleLifecycleSnapshot(
                    Identity(originalBytes),
                    SavedRuleStorageKind.UserOwned,
                    SavedRuleLifecycleState.Draft,
                    hasApproval: false,
                    hasEvidence: false,
                    isTrusted: false),
                catalogPath,
                catalogPath,
                changedBytes),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(
            "saved-rule.lifecycle.storage-authority-mismatch",
            Assert.Single(result.Decision.Issues).Code);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(
            catalogPath,
            TestContext.Current.CancellationToken));
    }

    /// <summary>Malformed or incomplete bytes cannot enter the Saved Rule lifecycle.</summary>
    [Fact]
    public async Task SaveRejectsDocumentThatDoesNotSatisfyTheV2Schema()
    {
        using var workspace = TempWorkspace.Create("nfc-saved-rule-hash");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        string rulePath = Path.Combine(authoringRoot, "rule.json");
        byte[] originalBytes = Document("before");
        byte[] editedBytes = JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ruleId"] = "after",
            });
        await File.WriteAllBytesAsync(
            rulePath,
            originalBytes,
            TestContext.Current.CancellationToken);
        SavedRuleDocumentLifecycleUseCase useCase =
            CreateUseCase(authoringRoot, catalogRoot);

        SavedRuleDocumentSaveResult result = await useCase.SaveAsync(
            new SavedRuleDocumentSaveRequest(
                new SavedRuleLifecycleSnapshot(
                    Identity(originalBytes),
                    SavedRuleStorageKind.UserOwned,
                    SavedRuleLifecycleState.Draft,
                    hasApproval: false,
                    hasEvidence: false,
                    isTrusted: false),
                rulePath,
                rulePath,
                editedBytes),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(
            "saved-rule.lifecycle.document-invalid",
            Assert.Single(result.Decision.Issues).Code);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(
            rulePath,
            TestContext.Current.CancellationToken));
    }

    /// <summary>A canceled atomic save preserves the original and removes staging bytes.</summary>
    [Fact]
    public async Task CanceledSavePreservesOriginalWithoutTemporaryArtifact()
    {
        using var workspace = TempWorkspace.Create("nfc-saved-rule-atomic");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        string rulePath = Path.Combine(authoringRoot, "rule.json");
        byte[] originalBytes = Document("before");
        await File.WriteAllBytesAsync(
            rulePath,
            originalBytes,
            TestContext.Current.CancellationToken);
        SavedRuleDocumentLifecycleUseCase useCase =
            CreateUseCase(authoringRoot, catalogRoot);
        byte[] editedBytes = Document("after");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await useCase.SaveAsync(
                new SavedRuleDocumentSaveRequest(
                    new SavedRuleLifecycleSnapshot(
                        Identity(originalBytes),
                        SavedRuleStorageKind.UserOwned,
                        SavedRuleLifecycleState.Draft,
                        hasApproval: false,
                        hasEvidence: false,
                        isTrusted: false),
                    rulePath,
                    rulePath,
                    editedBytes),
                cancellation.Token));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(
            rulePath,
            TestContext.Current.CancellationToken));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(authoringRoot),
            path => Path.GetFileName(path).EndsWith(
                ".tmp",
                StringComparison.Ordinal));
    }

    /// <summary>The complete rule identity is derived from strict document bytes, never caller fields.</summary>
    [Fact]
    public void IdentityReaderDerivesRuleVersionAndEveryExactParentFact()
    {
        var parent = new SavedRuleParentIdentity(
            "other-bundle",
            "2.0.0",
            new string('1', 64),
            "other-profile",
            "3.0.0",
            new string('2', 64),
            "other-family",
            "4.0.0",
            new string('3', 64),
            "other-map");

        SavedRuleExecutionIdentity identity = Identity(
            Document("derived-rule", "5.0.0", parent));

        Assert.Equal("derived-rule", identity.RuleId);
        Assert.Equal("5.0.0", identity.RuleVersion);
        Assert.Equal(parent, identity.Parent);
    }

    /// <summary>Malformed, missing, unknown, and duplicate v2 properties all fail closed.</summary>
    [Fact]
    public void IdentityReaderRejectsEveryNonCanonicalDocumentShape()
    {
        var reader = new SavedRuleDocumentIdentityReader();
        byte[] valid = Document("valid");
        JsonObject unknown = Assert.IsType<JsonObject>(
            JsonNode.Parse(valid));
        unknown["unknown"] = true;
        JsonObject missing = Assert.IsType<JsonObject>(
            JsonNode.Parse(valid));
        _ = missing.Remove("parentBinding");
        string duplicateParent = Encoding.UTF8.GetString(valid)
            .Replace(
                """
                "profileId": "profile",
                """,
                """
                "profileId": "profile",
                "profileId": "forged-profile",
                """,
                StringComparison.Ordinal);

        Assert.Null(reader.TryReadIdentity(
            Encoding.UTF8.GetBytes("{")));
        Assert.Null(reader.TryReadIdentity(
            Encoding.UTF8.GetBytes(unknown.ToJsonString())));
        Assert.Null(reader.TryReadIdentity(
            Encoding.UTF8.GetBytes(missing.ToJsonString())));
        Assert.Null(reader.TryReadIdentity(
            Encoding.UTF8.GetBytes(duplicateParent)));
    }

    /// <summary>Caller mutation cannot change the admitted snapshot before or during async write.</summary>
    [Fact]
    public async Task RequestOwnsOneDefensiveSnapshotThroughDelayedWrite()
    {
        byte[] originalBytes = Document("before");
        byte[] callerBytes = Document("after");
        byte[] expectedBytes = [.. callerBytes];
        var request = new SavedRuleDocumentSaveRequest(
            new SavedRuleLifecycleSnapshot(
                Identity(originalBytes),
                SavedRuleStorageKind.UserOwned,
                SavedRuleLifecycleState.Draft,
                hasApproval: false,
                hasEvidence: false,
                isTrusted: false),
            "rule",
            "rule",
            callerBytes);
        callerBytes[0] ^= 0xFF;
        var storage = new DelayedStorage();
        var useCase = new SavedRuleDocumentLifecycleUseCase(
            storage,
            new SavedRuleDocumentIdentityReader());

        Task<SavedRuleDocumentSaveResult> save = useCase.SaveAsync(
                request,
                TestContext.Current.CancellationToken)
            .AsTask();
        await storage.WriteStarted.Task.WaitAsync(
            TestContext.Current.CancellationToken);
        Array.Fill(callerBytes, (byte)0);
        _ = storage.AllowWrite.TrySetResult();
        SavedRuleDocumentSaveResult result = await save;

        Assert.True(result.Succeeded);
        Assert.Equal(expectedBytes, storage.WrittenBytes);
    }

    /// <summary>Configured roots and resolved targets remain fail-closed against path replacement.</summary>
    [Fact]
    public async Task StorageRejectsOverlapEscapeAndResolvedTargetReparse()
    {
        using var workspace = TempWorkspace.Create("nfc-saved-rule-path-guard");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        _ = Assert.Throws<ArgumentException>(() =>
            new SavedRuleDocumentStorage([authoringRoot], [authoringRoot]));
        var storage = new SavedRuleDocumentStorage(
            [authoringRoot],
            [catalogRoot]);
        _ = Assert.Throws<UnauthorizedAccessException>(() =>
            storage.ResolveTarget(workspace.PathFor("outside.json")));

        string targetPath = Path.Combine(authoringRoot, "target.json");
        SavedRuleDocumentStorageLocation target =
            storage.ResolveTarget(targetPath);
        string outsidePath = workspace.PathFor("outside-target.json");
        await File.WriteAllBytesAsync(
            outsidePath,
            Document("outside"),
            TestContext.Current.CancellationToken);
        try
        {
            _ = File.CreateSymbolicLink(targetPath, outsidePath);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or
                PlatformNotSupportedException or IOException)
        {
            return;
        }

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await storage.WriteAsync(
                target,
                Document("after"),
                TestContext.Current.CancellationToken));
        Assert.Equal(
            Document("outside"),
            await File.ReadAllBytesAsync(
                outsidePath,
                TestContext.Current.CancellationToken));
    }

    /// <summary>A failed atomic promotion removes its staging file and preserves the destination.</summary>
    [Fact]
    public async Task FailedWindowsPromotionRemovesTemporaryArtifact()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-saved-rule-move-failure");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        var storage = new SavedRuleDocumentStorage(
            [authoringRoot],
            [catalogRoot]);
        string targetPath = Path.Combine(authoringRoot, "rule.json");
        SavedRuleDocumentStorageLocation target =
            storage.ResolveTarget(targetPath);
        byte[] original = Document("before");
        await File.WriteAllBytesAsync(
            targetPath,
            original,
            TestContext.Current.CancellationToken);
        await using (FileStream lockStream = new(
                         targetPath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.None))
        {
            _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await storage.WriteAsync(
                    target,
                    Document("after"),
                    TestContext.Current.CancellationToken));
        }

        Assert.Equal(original, await File.ReadAllBytesAsync(
            targetPath,
            TestContext.Current.CancellationToken));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(authoringRoot),
            path => Path.GetFileName(path).EndsWith(
                ".tmp",
                StringComparison.Ordinal));
    }

    private static SavedRuleDocumentLifecycleUseCase CreateUseCase(
        string authoringRoot,
        string catalogRoot)
    {
        return new SavedRuleDocumentLifecycleUseCase(
            new SavedRuleDocumentStorage([authoringRoot], [catalogRoot]),
            new SavedRuleDocumentIdentityReader());
    }

    private static string CreateDirectory(TempWorkspace workspace, string name)
    {
        string path = workspace.PathFor(name);
        _ = Directory.CreateDirectory(path);
        return path;
    }

    private static readonly SavedRuleParentIdentity DefaultParent = new(
        "bundle",
        "1.0.0",
        new string('b', 64),
        "profile",
        "1.0.0",
        new string('c', 64),
        "family",
        "1.0.0",
        new string('d', 64),
        "map");

    private static byte[] Document(
        string ruleId,
        string ruleVersion = "1.0.0",
        SavedRuleParentIdentity? parent = null)
    {
        parent ??= DefaultParent;
        return Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion": "2.0",
              "ruleId": "{{ruleId}}",
              "ruleVersion": "{{ruleVersion}}",
              "displayName": "Saved Rule {{ruleId}}",
              "compositionKind": "merge",
              "sourceExperienceId": "general-merge",
              "imageInitialization": {
                "kind": "blank",
                "capacity": 16,
                "fillByte": 0
              },
              "parentBinding": {
                "bundleId": "{{parent.BundleId}}",
                "bundleVersion": "{{parent.BundleVersion}}",
                "bundleContentHash": "{{parent.BundleContentHash}}",
                "profileId": "{{parent.ProfileId}}",
                "profileVersion": "{{parent.ProfileVersion}}",
                "profileContentHash": "{{parent.ProfileContentHash}}",
                "familyId": "{{parent.FamilyId}}",
                "familyVersion": "{{parent.FamilyVersion}}",
                "familyContentHash": "{{parent.FamilyContentHash}}",
                "mapId": "{{parent.MapId}}"
              },
              "promotion": {
                "stage": "known",
                "blockers": []
              },
              "slotTemplates": [],
              "mappingFragments": [
                {
                  "fragmentId": "copy",
                  "operationKind": "copy-range",
                  "sourceSlot": {
                    "kind": "parent-slot",
                    "slotId": "source"
                  },
                  "sourceRange": {
                    "start": 0,
                    "length": 1
                  },
                  "targetRegionId": "region",
                  "targetOffset": 0,
                  "overlapPolicy": "reject",
                  "reason": "Lifecycle identity fixture."
                }
              ],
              "accessEnvelope": {
                "allowedRegionIds": [ "region" ],
                "maximumMappingCount": 1,
                "maximumTotalWriteBytes": 1,
                "protectedRangePolicy": "parent-profile"
              },
              "validationRuleIds": [],
              "processorStageIds": [],
              "owner": "owner",
              "reviewers": [],
              "evidenceRefs": [ "evidence" ]
            }
            """);
    }

    private static SavedRuleExecutionIdentity Identity(byte[] documentBytes)
    {
        return Assert.IsType<SavedRuleExecutionIdentity>(
            new SavedRuleDocumentIdentityReader()
                .TryReadIdentity(documentBytes));
    }

    private sealed class DelayedStorage : ISavedRuleDocumentStorage
    {
        internal TaskCompletionSource WriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource AllowWrite { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal byte[]? WrittenBytes { get; private set; }

        public SavedRuleDocumentStorageLocation ResolveSource(
            string documentReference)
        {
            return new SavedRuleDocumentStorageLocation(
                documentReference,
                isTrustedCatalog: false);
        }

        public SavedRuleDocumentStorageLocation ResolveTarget(
            string documentReference)
        {
            return new SavedRuleDocumentStorageLocation(
                documentReference,
                isTrustedCatalog: false);
        }

        public bool RepresentsSameDocument(
            SavedRuleDocumentStorageLocation left,
            SavedRuleDocumentStorageLocation right)
        {
            return StringComparer.Ordinal.Equals(
                left.DocumentId,
                right.DocumentId);
        }

        public async ValueTask WriteAsync(
            SavedRuleDocumentStorageLocation target,
            ReadOnlyMemory<byte> documentBytes,
            CancellationToken cancellationToken)
        {
            _ = WriteStarted.TrySetResult();
            await AllowWrite.Task.WaitAsync(cancellationToken);
            WrittenBytes = documentBytes.ToArray();
        }
    }
}
