using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Pins the ADR 0075 preconditions that make bundle preloading race-free and result-neutral.</summary>
public sealed class BuiltInBundlePreloadTests
{
    /// <summary>The layer table names every opened trusted bundle once and excludes only logical candidates.</summary>
    [Fact]
    public void LayerTableMatchesTheTrustIndex()
    {
        ProfileBundlePackageTrustIndex trustIndex = BuiltInV2BundleRegistry.TrustIndex;
        string[] table = [.. BuiltInV2BundlePreload.Layers.SelectMany(static layer => layer)];

        Assert.True(BuiltInV2BundlePreload.MatchesTrustIndex(trustIndex.Bundles));
        Assert.Equal(table.Length, table.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            BuiltInV2BundlePreload.ExcludedBundles.Order(StringComparer.Ordinal),
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

    /// <summary>Any trusted bundle outside the table and the fixed exclusions leaves loading serial.</summary>
    [Fact]
    public void TrustIndexDriftDisablesPreloading()
    {
        IReadOnlyList<ProfileBundlePackageTrustEntry> bundles = BuiltInV2BundleRegistry.TrustIndex.Bundles;
        ProfileBundlePackageTrustEntry logicalCandidate = bundles.First(entry =>
            StringComparer.Ordinal.Equals(entry.BundleDirectory, BuiltInV2BundlePreload.ExcludedBundles[0]));

        Assert.True(BuiltInV2BundlePreload.MatchesTrustIndex(bundles));
        Assert.False(BuiltInV2BundlePreload.MatchesTrustIndex(
            [.. bundles, logicalCandidate with { BundleDirectory = "nt5199x-general-merge-logical-candidate" }]));
        Assert.False(BuiltInV2BundlePreload.MatchesTrustIndex([.. bundles.Skip(1)]));
        Assert.False(BuiltInV2BundlePreload.MatchesTrustIndex([.. bundles, bundles[0]]));
    }

    /// <summary>The warm-up entry runs alone, and each layer finishes before the next starts.</summary>
    [Fact]
    public void RunLayersRunsTheWarmUpAloneAndSeparatesLayers()
    {
        string[][] layers = [["w", "a", "b", "c", "d"], ["e", "f", "g"], ["h"]];
        var events = new List<(string Kind, string Entry)>();
        var gate = new Lock();

        BuiltInV2BundlePreload.RunLayers(layers, entry => Record(events, gate, entry), 4, CancellationToken.None);

        Assert.Equal(1, BuiltInV2BundlePreload.WarmUpBundleCount);
        Assert.Equal([("start", "w"), ("end", "w")], events.Take(2));
        for (int layer = 1; layer < layers.Length; layer++)
        {
            string[] previous = layers[layer - 1];
            string[] current = layers[layer];
            int lastEndOfPrevious = events.FindLastIndex(item => item.Kind == "end" && previous.Contains(item.Entry));
            int firstStartOfLayer = events.FindIndex(item => item.Kind == "start" && current.Contains(item.Entry));
            Assert.True(lastEndOfPrevious < firstStartOfLayer, $"layer {layer} started before layer {layer - 1} finished");
        }

        Assert.Equal(layers.Sum(static layer => layer.Length) * 2, events.Count);
    }

    /// <summary>Cancellation stops scheduling, lets started entries finish, and never reaches later layers.</summary>
    [Fact]
    public void RunLayersDrainsStartedEntriesOnCancellation()
    {
        string[][] layers = [["w", "a", "b", "c", "d", "e", "f"], ["later"]];
        var events = new List<(string Kind, string Entry)>();
        var gate = new Lock();
        using var cancellation = new CancellationTokenSource();

        _ = Assert.ThrowsAny<OperationCanceledException>(() => BuiltInV2BundlePreload.RunLayers(
            layers,
            entry =>
            {
                if (entry == "b")
                {
                    cancellation.Cancel();
                }

                Record(events, gate, entry);
            },
            2,
            cancellation.Token));

        string[] started = [.. events.Where(static item => item.Kind == "start").Select(static item => item.Entry)];
        string[] ended = [.. events.Where(static item => item.Kind == "end").Select(static item => item.Entry)];
        Assert.Equal(started.Order(StringComparer.Ordinal), ended.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("later", started);
    }

    /// <summary>Preloading failing dependencies in any order leaves the serial pass's first failure unchanged.</summary>
    [Fact]
    public void PreloadKeepsTheSerialFirstFailure()
    {
        CapabilityCatalogLoadResult withoutPreload = LoadWithTwoFailingDependencies(preload: false);
        CapabilityCatalogLoadResult withPreload = LoadWithTwoFailingDependencies(preload: true);

        Assert.False(withoutPreload.Succeeded);
        Assert.False(withPreload.Succeeded);
        CapabilityCatalogIssue expected = Assert.Single(withoutPreload.Issues);
        CapabilityCatalogIssue actual = Assert.Single(withPreload.Issues);
        Assert.Equal("first failing dependency", expected.Message);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.Message, actual.Message);
    }

    /// <summary>Cancellation during preloading propagates before any route is classified.</summary>
    [Fact]
    public void CancellationDuringPreloadStopsTheLoad()
    {
        using var cancellation = new CancellationTokenSource();
        int classifications = 0;
        var source = new CanonicalCapabilityCatalogSource(
            BuiltInCanonicalCapabilityPolicy.Load,
            identity =>
            {
                classifications++;
                return CanonicalDynamicRouteInventory.IsDynamic(identity);
            },
            CanonicalCompiledRouteInventory.Resolve,
            CanonicalDynamicRouteInventory.CreateResolver,
            CanonicalCapabilityDisclosureInventory.Create,
            CanonicalFullImageMetadataInventory.Create,
            _ => cancellation.Cancel());

        _ = Assert.ThrowsAny<OperationCanceledException>(() => source.Load(cancellation.Token));
        Assert.Equal(0, classifications);
    }

    private static CapabilityCatalogLoadResult LoadWithTwoFailingDependencies(bool preload)
    {
        var first = new Lazy<int>(static () => throw new InvalidDataException("first failing dependency"));
        var second = new Lazy<int>(static () => throw new InvalidDataException("second failing dependency"));
        CanonicalCapabilityPolicySnapshot policy = BuiltInCanonicalCapabilityPolicy.Load();
        string[] staticRoutes = [.. policy.Routes
            .Where(static route => !CanonicalDynamicRouteInventory.IsDynamic(route.Identity))
            .Select(static route => route.Identity.RouteId)];
        var source = new CanonicalCapabilityCatalogSource(
            () => policy,
            CanonicalDynamicRouteInventory.IsDynamic,
            identity =>
            {
                _ = identity.RouteId == staticRoutes[0] ? first.Value : 0;
                _ = identity.RouteId == staticRoutes[1] ? second.Value : 0;
                return CanonicalCompiledRouteInventory.Resolve(identity);
            },
            CanonicalDynamicRouteInventory.CreateResolver,
            CanonicalCapabilityDisclosureInventory.Create,
            CanonicalFullImageMetadataInventory.Create,
            preload
                ? _ => BuiltInV2BundlePreload.RunLayers(
                    [["second", "first"]],
                    entry => Force(entry == "first" ? first : second),
                    2,
                    CancellationToken.None)
                : null);
        return source.Load(CancellationToken.None);
    }

    private static void Force(Lazy<int> dependency)
    {
        try
        {
            _ = dependency.Value;
        }
        catch (InvalidDataException)
        {
        }
    }

    private static void Record(List<(string Kind, string Entry)> events, Lock gate, string entry)
    {
        lock (gate)
        {
            events.Add(("start", entry));
        }

        Thread.Sleep(5 + (entry.GetHashCode(StringComparison.Ordinal) & 7));
        lock (gate)
        {
            events.Add(("end", entry));
        }
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
