using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>AB compilation retains the declared map-set axis instead of selecting by IC alone.</summary>
public sealed class AbMergeExactRouteTests
{
    /// <summary>An unknown map-set is not a registered dynamic route, even for a known IC.</summary>
    [Theory]
    [InlineData("NT51950", "1-ic")]
    [InlineData("NT51950", "2-plus-ic")]
    [InlineData("NT51951", "selector-free")]
    public void UnknownMapSetDoesNotAcquireDynamicRegistration(string icId, string countVariant)
    {
        var identity = new CapabilityRouteIdentity(
            icId, ExperienceIds.AbMerge, countVariant, "unregistered-map-set");

        Assert.False(CanonicalDynamicRouteInventory.IsDynamic(identity));
        _ = Assert.Throws<InvalidDataException>(() => CanonicalDynamicRouteInventory.Resolve(identity));

        var adapter = new BuiltInV2DynamicCompilationAdapter();
        adapter.Compile(identity, null, [], out CompiledComposition? composition,
            out MetadataPlanDefinition? metadata, out IReadOnlyList<CompositionIssue> issues);
        Assert.Null(composition);
        Assert.Null(metadata);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, Assert.Single(issues).Code);
    }

    /// <summary>Exact published identities reach the real compiler and retain strict profile/map binding.</summary>
    [Theory]
    [InlineData("NT51950", "1-ic")]
    [InlineData("NT51950", "2-plus-ic")]
    [InlineData("NT51951", "selector-free")]
    public void ExactPublishedRouteCompilesAndBindsItsOwnMapSubset(string icId, string countVariant)
    {
        var catalog = new CanonicalCapabilityCatalog(CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        ResolvedCapabilityRoute route = Assert.Single(catalog.GetCurrentSnapshot().DynamicRoutes, candidate =>
            candidate.Identity.IcId == icId && candidate.Identity.WorkflowId == ExperienceIds.AbMerge &&
            candidate.Identity.IcCountVariant == countVariant);
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, new BuiltInV2DynamicCompilationAdapter());

        Assert.True(compiler.TryCompilePublishedDynamicCapability(route.Identity, null, [],
            out CompiledComposition? composition, out ResolvedCapability? capability,
            out IReadOnlyList<CompositionIssue> issues, route.AbMergeTopologyChoice?.Selection));

        Assert.Empty(issues);
        Assert.NotNull(composition);
        Assert.NotNull(capability);
        Assert.Same(route.Identity, capability.Identity);
        Assert.Equal(route.ResolutionToken, capability.ResolutionToken);
        Assert.Equal(route.CompilationContract.ProfileId, composition.V2Details.ProfileId);
        Assert.Contains(composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId,
            route.CompilationContract.AllowedMapVariantIds);
    }

    /// <summary>The prepublication DP probe cannot be reused to bypass exact AB selection.</summary>
    [Fact]
    public void DefinitionProbeRejectsAbWithoutSynthesizingCommon()
    {
        var adapter = new BuiltInV2DynamicCompilationAdapter();
        adapter.CompileDefinition("NT51951", ExperienceIds.AbMerge, null, [],
            out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues);

        Assert.Null(composition);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, Assert.Single(issues).Code);
    }

    /// <summary>A static registration retains its exact individual map identity, not just matching capacity.</summary>
    [Theory]
    [InlineData("nt51929-standard-merge-256k", true)]
    [InlineData("unregistered-static-map", false)]
    public void StaticRegistrationValidatesExactMapAfterCompilation(string mapId, bool accepted)
    {
        var identity = new CapabilityRouteIdentity("NT51929", ExperienceIds.StandardMerge, "selector-free", mapId);
        var adapter = new BuiltInV2DynamicCompilationAdapter();
        adapter.Compile(identity, 0x40000, null, out CompiledComposition? composition,
            out MetadataPlanDefinition? metadata, out IReadOnlyList<CompositionIssue> issues);

        if (accepted)
        {
            Assert.Empty(issues);
            Assert.NotNull(composition);
            Assert.NotNull(metadata);
            Assert.Equal(mapId, composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        }
        else
        {
            Assert.Null(composition);
            Assert.Null(metadata);
            Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, Assert.Single(issues).Code);
        }
    }
}
