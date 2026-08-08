namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>The pinned JSON loader keeps only its two live hash modes.</summary>
    [Fact]
    public void InfrastructureConvergenceRemovesUnusedPinnedJsonOverload()
    {
        string loader = ReadText("src/NvtFwCombiner.Infrastructure/PinnedJsonCatalogLoader.cs");

        Assert.Contains("internal static T LoadExact<T>(", loader, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(loader, "JsonTypeInfo<T> typeInfo"));
    }

    /// <summary>Both external processors use one best-effort staging-directory cleanup owner.</summary>
    [Fact]
    public void InfrastructureConvergenceSharesStagingCleanup()
    {
        string manifestStaging = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerProcessor.Staging.cs");
        string manifestProcessor = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerProcessor.cs")
            .ReplaceLineEndings("\n");
        string legacyProcessor = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.cs")
            .ReplaceLineEndings("\n");
        string stagingFileSystem = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/StagedArtifactFileVerifier.cs")
            .ReplaceLineEndings("\n");

        Assert.DoesNotContain("private static void TryDeleteDirectory", manifestStaging, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void TryDeleteDirectory", legacyProcessor, StringComparison.Ordinal);
        Assert.Contains("internal static class ExternalStagingDirectory", stagingFileSystem, StringComparison.Ordinal);
        const string cleanupFinally =
            "finally\n        {\n            ExternalStagingDirectory.TryDelete(runDirectory);\n        }";
        Assert.Contains(cleanupFinally, manifestProcessor, StringComparison.Ordinal);
        Assert.Contains(cleanupFinally, legacyProcessor, StringComparison.Ordinal);
        Assert.Contains("Directory.Delete(path, recursive: true);", stagingFileSystem, StringComparison.Ordinal);
        Assert.Contains("catch (IOException)\n        {\n        }", stagingFileSystem, StringComparison.Ordinal);
        Assert.Contains(
            "catch (UnauthorizedAccessException)\n        {\n        }",
            stagingFileSystem,
            StringComparison.Ordinal);
    }

    /// <summary>Embedded JSON schemas share resource loading while retaining separate lazy authorities.</summary>
    [Fact]
    public void InfrastructureConvergenceSharesEmbeddedSchemaLoading()
    {
        string validator = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleSchemaValidator.cs");
        string manifestSchema = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleManifestSchema.cs");
        string trustIndexSchema = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundlePackageTrustIndexSchema.cs");
        string savedRuleSchema = ReadText(
            "src/NvtFwCombiner.Infrastructure/Contracts/SavedCompositionRuleV2Schema.cs");

        Assert.Contains("internal static JsonSchema LoadEmbeddedSchema(", validator, StringComparison.Ordinal);
        Assert.DoesNotContain("private static JsonSchema LoadSchema()", manifestSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("private static JsonSchema LoadSchema()", trustIndexSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("private static JsonSchema LoadSchema()", savedRuleSchema, StringComparison.Ordinal);
        Assert.Contains("ProfileBundleSchemaValidator.LoadEmbeddedSchema(", manifestSchema, StringComparison.Ordinal);
        Assert.Contains("ProfileBundleSchemaValidator.LoadEmbeddedSchema(", trustIndexSchema, StringComparison.Ordinal);
        Assert.Contains("ProfileBundleSchemaValidator.LoadEmbeddedSchema(", savedRuleSchema, StringComparison.Ordinal);
    }

    /// <summary>Saved Rule identity parsing reuses the one strict duplicate-key reader.</summary>
    [Fact]
    public void InfrastructureConvergenceReusesStrictJsonReader()
    {
        string identityReader = ReadText(
            "src/NvtFwCombiner.Infrastructure/Files/SavedRuleDocumentIdentityReader.cs");

        Assert.Contains("StrictJsonDocumentReader.Parse(", identityReader, StringComparison.Ordinal);
        Assert.DoesNotContain("HasUniqueProperties", identityReader, StringComparison.Ordinal);
    }

    /// <summary>Bundle snapshots keep only state consumed after construction.</summary>
    [Fact]
    public void InfrastructureConvergenceRemovesConstructionOnlyBundleState()
    {
        string manifest = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleManifest.cs");
        string trustIndex = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundlePackageTrustIndex.cs");
        string projection = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/TrustedProfileBundleDocumentProjection.cs");
        string entrySnapshots = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleEntrySnapshotCollection.cs");
        string loader = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleLoader.cs");

        Assert.DoesNotContain("private readonly ProfileBundleEntry[] _entries;", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly ProfileBundlePackageTrustEntry[] _bundles;", trustIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly TrustedFirmwareFamilyDocumentEntry[] _families;", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly TrustedCompositionProfileDocumentEntry[] _profiles;", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("internal int TotalEntryBytes", entrySnapshots, StringComparison.Ordinal);
        Assert.DoesNotContain("Entries = entrySnapshots.Entries;", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("internal IReadOnlyList<ProfileBundleEntrySnapshot> Entries", loader, StringComparison.Ordinal);
    }

    /// <summary>Trusted document wrappers retain their types without handwritten forwarding constructors.</summary>
    [Fact]
    public void InfrastructureConvergenceCompactsTrustedDocumentWrappers()
    {
        string projection = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/TrustedProfileBundleDocumentProjection.cs");

        Assert.Contains(
            "internal sealed class TrustedProfileBundleDocumentIdentity(ProfileBundleEntry entry)",
            projection,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal sealed class TrustedFirmwareFamilyDocumentEntry(",
            projection,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal sealed class TrustedCompositionProfileDocumentEntry(",
            projection,
            StringComparison.Ordinal);
        Assert.DoesNotContain("internal TrustedFirmwareFamilyDocumentEntry(", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("internal TrustedCompositionProfileDocumentEntry(", projection, StringComparison.Ordinal);
    }
}
