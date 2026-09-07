using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.Capabilities;

public sealed partial class CanonicalCapabilityCatalogTests
{
    /// <summary>A dynamic AB count axis must retain a typed topology choice before publication.</summary>
    [Theory]
    [InlineData("1-ic")]
    [InlineData("2-plus-ic")]
    public void DynamicAbRequiresDeclaredTopologyChoice(string axis)
    {
        CapabilityRouteIdentity identity = CreateAbRoute("NT51950", axis, "synthetic-ab-map-set");
        _ = Assert.Throws<ArgumentException>(() => CreateDynamicAbDefinition(identity));
    }

    /// <summary>Disclosure values must be canonical projections, not independent count overrides.</summary>
    [Theory]
    [InlineData("1-ic", "cascade", 2)]
    [InlineData("2-plus-ic", "single", 1)]
    [InlineData("2-plus-ic", "cascade", 3)]
    [InlineData("selector-free", "single", 1)]
    [InlineData("unknown", "single", 1)]
    public void DynamicAbRejectsInconsistentTopologyDisclosure(string axis, string token, int count)
    {
        CapabilityRouteIdentity identity = CreateAbRoute("NT51950", axis, "synthetic-ab-map-set");
        _ = Assert.Throws<ArgumentException>(() => CreateDynamicAbDefinition(identity, Choice(token, count)));
    }

    /// <summary>AB disclosure cannot be attached to a different workflow.</summary>
    [Fact]
    public void DynamicAbRejectsTopologyOnNonAbRoute()
    {
        var identity = new CapabilityRouteIdentity("NT51950", ExperienceIds.StandardMerge, "1-ic", "ab-maps");
        _ = Assert.Throws<ArgumentException>(() => CreateDynamicAbDefinition(identity, Choice("single", 1)));
    }

    /// <summary>Unavailable dynamic routes cannot leak topology choices into the authoring selector.</summary>
    [Fact]
    public void DynamicAbUnavailableRouteHasNoSelectorChoice()
    {
        CanonicalDynamicCapabilityDefinition definition = CreateDynamicAbDefinition(
            CreateAbRoute("NT51950", "1-ic", "ab-maps"), Choice("single", 1), unavailable: true);
        var catalog = new CanonicalCapabilityCatalog(new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(
            new CanonicalCapabilityCatalogCandidate("test", "1", new string('b', 64), [], [definition]))));
        CapabilityCatalogReloadResult load = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded);
        Assert.Empty(load.Snapshot!.SelectorPublication.GetAbMergeTopologyChoices("NT51950"));
    }

    /// <summary>Dynamic AB routes disclose topology only through their current authorable publication.</summary>
    [Fact]
    public void DynamicAbPublicationRetainsSingleAndCascadeChoices()
    {
        CanonicalDynamicCapabilityDefinition single = CreateDynamicAbDefinition(
            CreateAbRoute("NT51950", "1-ic", "ab-maps"), Choice("single", 1));
        CanonicalDynamicCapabilityDefinition cascade = CreateDynamicAbDefinition(
            CreateAbRoute("NT51950", "2-plus-ic", "ab-maps"), Choice("cascade", 2));
        CanonicalDynamicCapabilityDefinition selectorFree = CreateDynamicAbDefinition(
            CreateAbRoute("NT51929", "selector-free", "ab-maps"));
        var catalog = new CanonicalCapabilityCatalog(new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(
            new CanonicalCapabilityCatalogCandidate("test", "1", new string('b', 64), [], [cascade, single, selectorFree]))));
        CapabilityCatalogReloadResult load = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, string.Join(" | ", load.Issues.Select(static issue => issue.Message)));
        CapabilitySelectorPublication selector = load.Snapshot!.SelectorPublication;
        Assert.Equal(["single", "cascade"], selector.GetAbMergeTopologyChoices("NT51950").Select(static choice => choice.Token));
        Assert.Empty(selector.GetAbMergeTopologyChoices("NT51929"));
        Assert.Equal(load.Snapshot.ResolutionToken, selector.ResolutionToken);
    }

    /// <summary>Compiled topology is checked by category, preserving generic cascade counts.</summary>
    [Theory]
    [InlineData("1-ic", 1, true)]
    [InlineData("1-ic", 2, false)]
    [InlineData("2-plus-ic", 1, false)]
    [InlineData("2-plus-ic", 2, true)]
    [InlineData("2-plus-ic", 3, true)]
    public void DynamicAbValidatesCompiledTopologyCategory(string axis, int count, bool accepted)
    {
        CapabilityRouteIdentity identity = CreateAbRoute("NT51950", axis, "ab-map");
        CompiledComposition composition = CreateAbCompiledComposition(identity, count);
        if (accepted)
        {
            AbMergeTopologyChoiceProjection.ValidateCompilation(identity, composition);
        }
        else
        {
            _ = Assert.Throws<ArgumentException>(() => AbMergeTopologyChoiceProjection.ValidateCompilation(identity, composition));
        }
    }

    private static CapabilityTopologyChoice Choice(string token, int count)
    {
        return new(token, new TopologySelection(count, "test", TopologySelectionSource.Requested, "test"));
    }

    /// <summary>The actual route binding checks both topology category and its pinned map subset.</summary>
    [Theory]
    [InlineData(2, false, true)]
    [InlineData(3, false, true)]
    [InlineData(1, false, false)]
    [InlineData(2, true, false)]
    public void DynamicAbBindingRetainsTopologyAndMapBounds(int count, bool wrongMap, bool accepted)
    {
        CapabilityRouteIdentity identity = CreateAbRoute("NT51950", "2-plus-ic", "ab-map");
        CompiledComposition reference = CreateAbCompiledComposition(identity, 2);
        CanonicalCapabilityCompilationContract contract = CanonicalCapabilityCompilationContract.FromCompiled(identity, reference);
        CanonicalDynamicCapabilityDefinition definition = CreateDynamicAbDefinition(identity, Choice("cascade", 2), contract);
        var route = new ResolvedCapabilityRoute(definition, new ResolutionToken("test:dynamic-ab"));
        CapabilityRouteIdentity compiledIdentity = wrongMap
            ? CreateAbRoute("NT51950", "2-plus-ic", "other-map")
            : identity;
        CompiledComposition candidate = CreateAbCompiledComposition(compiledIdentity, count);
        if (accepted)
        {
            ResolvedCapability bound = route.BindCompilation(candidate);
            Assert.Equal(route.ResolutionToken, bound.ResolutionToken);
        }
        else
        {
            _ = Assert.Throws<ArgumentException>(() => route.BindCompilation(candidate));
        }
    }

    private static CanonicalDynamicCapabilityDefinition CreateDynamicAbDefinition(
        CapabilityRouteIdentity identity, CapabilityTopologyChoice? choice = null,
        CanonicalCapabilityCompilationContract? compilationContract = null,
        bool unavailable = false)
    {
        CanonicalCapabilityCompilationContract contract = compilationContract ?? new CanonicalCapabilityCompilationContract(
            "synthetic-ab", "1.0.0", new string('a', 64), ["synthetic-ab-map"],
            CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId);
        string fingerprint = CapabilityDefinitionFingerprint.Compute(identity, contract.ProfileId,
            contract.ProfileVersion, contract.TrustedDefinitionSha256, contract.AllowedMapVariantIds,
            contract.CompilerSemanticId, contract.SemanticBindingIds);
        return new CanonicalDynamicCapabilityDefinition(identity, fingerprint, contract,
            Decision(identity, fingerprint, unavailable ? CapabilityAuthoringAvailability.Unavailable : CapabilityAuthoringAvailability.Available, "authoring", "test"),
            Decision(identity, fingerprint, CapabilityPublicationStatus.Supported, "publication", "test"),
            Decision(identity, fingerprint, CapabilityEvidenceStatus.Missing, "evidence", "test"),
            abMergeTopologyChoice: choice);
    }
}
