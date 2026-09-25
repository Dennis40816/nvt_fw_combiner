using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Pins the ADR 0075 preconditions that make bundle preloading race-free and result-neutral.</summary>
public sealed class BuiltInBundlePreloadTests
{
    private static readonly string[] GeneralMergeLogicalCandidates =
    [
        "nt51917-nt51927-general-merge-logical-candidate",
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "nt51923-nt51926-general-merge-logical-candidate",
        "nt51928-general-merge-logical-candidate",
        "nt51950-nt51951-general-merge-logical-candidate",
    ];

    /// <summary>The layer table names every opened trusted bundle once and excludes only logical candidates.</summary>
    [Fact]
    public void LayerTableMatchesTheTrustIndex()
    {
        ProfileBundlePackageTrustIndex trustIndex = BuiltInV2BundleRegistry.TrustIndex;
        string[] table = [.. BuiltInV2BundlePreload.Layers.SelectMany(static layer => layer)];

        Assert.True(BuiltInV2BundlePreload.MatchesTrustIndex(trustIndex));
        Assert.Equal(table.Length, table.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            GeneralMergeLogicalCandidates,
            trustIndex.Bundles
                .Select(static entry => entry.BundleDirectory)
                .Except(table, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Every metadata reference of a table bundle resolves to a bundle of a strictly earlier layer, so no
    /// worker can wait for a lazy initialization held by another worker, and the graph is acyclic.
    /// </summary>
    [Fact]
    public void EveryReferenceResolvesToAnEarlierLayer()
    {
        ProfileBundlePackageTrustIndex trustIndex = BuiltInV2BundleRegistry.TrustIndex;
        Dictionary<(string FamilyId, string FamilyVersion), string> providers = trustIndex.Bundles
            .SelectMany(static entry => entry.MetadataProviderFamilies.Select(provider =>
                (Key: (provider.FamilyId, provider.FamilyVersion), entry.BundleDirectory)))
            .ToDictionary(static item => item.Key, static item => item.BundleDirectory);
        Dictionary<string, int> layerOf = BuiltInV2BundlePreload.Layers
            .SelectMany(static (layer, index) => layer.Select(directory => (directory, index)))
            .ToDictionary(static item => item.directory, static item => item.index, StringComparer.Ordinal);
        string root = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in");
        int references = 0;

        foreach ((string directory, int layer) in layerOf)
        {
            foreach ((string familyId, string familyVersion) in ReferencesOf(Path.Combine(root, directory)))
            {
                references++;
                Assert.True(
                    providers.TryGetValue((familyId, familyVersion), out string? provider),
                    $"{directory} references {familyId}@{familyVersion}, which no trusted bundle provides.");
                Assert.True(
                    layerOf.TryGetValue(provider, out int providerLayer) && providerLayer < layer,
                    $"{directory} (layer {layer}) references {provider}, which is not in an earlier layer.");
            }
        }

        Assert.True(references > 0);
    }

    /// <summary>Preloading creates every table catalog on the built-in inputs.</summary>
    [Fact]
    public void PreloadCreatesEveryTableCatalog()
    {
        BuiltInV2BundlePreload.Run(CancellationToken.None);

        Assert.All(
            BuiltInV2BundlePreload.Layers.SelectMany(static layer => layer),
            directory => Assert.True(BuiltInV2BundleRegistry.All[directory].IsCatalogCreated, directory));
    }

    /// <summary>Cancellation before preloading propagates as the load's cancellation.</summary>
    [Fact]
    public void CancelledPreloadThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Assert.ThrowsAny<OperationCanceledException>(() => BuiltInV2BundlePreload.Run(cancellation.Token));
    }

    /// <summary>A preloaded load failure reaches its caller with the same issue as without preloading.</summary>
    [Fact]
    public void PreloadLeavesTheSameFailureForTheSerialPass()
    {
        BuiltInV2Bundle preloaded = MissingBundle();
        BuiltInV2Bundle untouched = MissingBundle();

        preloaded.Preload();
        preloaded.Preload();
        V2CompositionPlanCompileResult afterPreload = CompileAny(preloaded);
        V2CompositionPlanCompileResult withoutPreload = CompileAny(untouched);

        Assert.False(preloaded.IsCatalogCreated);
        CompositionIssue expected = Assert.Single(withoutPreload.Issues);
        CompositionIssue actual = Assert.Single(afterPreload.Issues);
        Assert.Equal("profile.v2.builtin-bundle-load-failed", expected.Code);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.Message, actual.Message);
    }

    private static BuiltInV2Bundle MissingBundle()
    {
        return new BuiltInV2Bundle(
            "nfc-preload-missing-bundle",
            "1.0.0",
            new string('a', 64),
            BuiltInV2BundleRegistry.TrustIndex.TrustAnchorBindingId);
    }

    private static V2CompositionPlanCompileResult CompileAny(BuiltInV2Bundle bundle)
    {
        return bundle.Compile("profile", "1.0.0", "NT51923", ExperienceIds.StandardMerge, null, []);
    }

    private static IEnumerable<(string FamilyId, string FamilyVersion)> ReferencesOf(string bundleRoot)
    {
        foreach (string file in Directory.EnumerateFiles(bundleRoot, "*.json", SearchOption.AllDirectories)
                     .Where(static file => !string.Equals(Path.GetFileName(file), "profile-bundle.json", StringComparison.Ordinal))
                     .Where(static file => !file.Split(Path.DirectorySeparatorChar).Contains("schemas", StringComparer.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            foreach ((string, string) reference in DefinitionReferences(document.RootElement))
            {
                yield return reference;
            }
        }
    }

    private static IEnumerable<(string FamilyId, string FamilyVersion)> DefinitionReferences(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.NameEquals("definitionReference") && property.Value.ValueKind == JsonValueKind.Object)
                {
                    yield return (
                        property.Value.GetProperty("familyId").GetString()!,
                        property.Value.GetProperty("familyVersion").GetString()!);
                }

                foreach ((string, string) nested in DefinitionReferences(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach ((string, string) nested in DefinitionReferences(item))
                {
                    yield return nested;
                }
            }
        }
    }
}
