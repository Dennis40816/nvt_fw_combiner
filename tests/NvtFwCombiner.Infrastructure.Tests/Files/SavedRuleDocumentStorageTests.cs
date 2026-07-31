using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
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
                Identity(editedBytes),
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
                Identity(catalogBytes),
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
                Identity(changedBytes),
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
                Identity(changedBytes),
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

    /// <summary>Serialized bytes cannot be written under a caller-forged canonical hash.</summary>
    [Fact]
    public async Task SaveRejectsIdentityThatDoesNotMatchSerializedContent()
    {
        using var workspace = TempWorkspace.Create("nfc-saved-rule-hash");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        string rulePath = Path.Combine(authoringRoot, "rule.json");
        byte[] originalBytes = Document("before");
        byte[] editedBytes = Document("after");
        await File.WriteAllBytesAsync(
            rulePath,
            originalBytes,
            TestContext.Current.CancellationToken);
        SavedRuleExecutionIdentity actual = Identity(editedBytes);
        var forged = new SavedRuleExecutionIdentity(
            actual.RuleId,
            actual.RuleVersion,
            new string('a', 64),
            actual.Parent);
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
                forged,
                rulePath,
                rulePath,
                editedBytes),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(
            "saved-rule.lifecycle.content-hash-mismatch",
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
                    Identity(editedBytes),
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

    private static SavedRuleDocumentLifecycleUseCase CreateUseCase(
        string authoringRoot,
        string catalogRoot)
    {
        return new SavedRuleDocumentLifecycleUseCase(
            new SavedRuleDocumentStorage([authoringRoot], [catalogRoot]));
    }

    private static string CreateDirectory(TempWorkspace workspace, string name)
    {
        string path = workspace.PathFor(name);
        _ = Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] Document(string ruleId)
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ruleId"] = ruleId,
            });
    }

    private static SavedRuleExecutionIdentity Identity(byte[] documentBytes)
    {
        using var document = JsonDocument.Parse(documentBytes);
        return new SavedRuleExecutionIdentity(
            "copy-display-window",
            "1.0.0",
            SavedCompositionRuleV2ContentHasher.Calculate(
                document.RootElement),
            new SavedRuleParentIdentity(
                "bundle",
                "1.0.0",
                new string('b', 64),
                "profile",
                "1.0.0",
                new string('c', 64),
                "family",
                "1.0.0",
                new string('d', 64),
                "map"));
    }
}
