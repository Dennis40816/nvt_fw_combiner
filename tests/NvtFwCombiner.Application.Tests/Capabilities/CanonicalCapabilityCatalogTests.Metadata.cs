using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Tests.Capabilities;

public sealed partial class CanonicalCapabilityCatalogTests
{
    /// <summary>Read-only metadata lookup retains the publication plan without reopening authoring.</summary>
    [Fact]
    public void ResolveUniqueMetadataPlanIgnoresAuthoringWithoutReturningExecutionAuthority()
    {
        CanonicalCapabilityDefinition definition = CreateDefinition(
            CreateCompiledComposition(),
            authoringAvailability:
                CapabilityAuthoringAvailability.Unavailable);
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(
                    new CanonicalCapabilityCatalogCandidate(
                        "canonical-capability-catalog",
                        "1.0.0",
                        new string('a', 64),
                        [definition]))));
        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);

        CapabilityResolutionResult authoring = catalog.ResolveUniqueRoute(
            "NT51929",
            "standard-merge",
            "selector-free",
            outputCapacity: 8);
        MetadataPlanResolutionResult metadata = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "selector-free",
            outputCapacity: 8);

        Assert.False(authoring.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.AuthoringUnavailable,
            authoring.Issue!.Code);
        Assert.True(metadata.Succeeded);
        Assert.NotNull(metadata.MetadataPlan);
        Assert.Equal(
            reload.Snapshot!.ResolutionToken,
            metadata.MetadataPlan.ResolutionToken);
    }

    /// <summary>A cold metadata query loads one publication and reuses it for later queries.</summary>
    [Fact]
    public void ResolveUniqueMetadataPlanColdQueryLoadsAndCachesPublishedPlan()
    {
        var source = new QueueCapabilitySource(
            CapabilityCatalogLoadResult.Success(CreateCandidate()));
        var catalog = new CanonicalCapabilityCatalog(source);

        MetadataPlanResolutionResult first = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "selector-free",
            outputCapacity: 8);
        MetadataPlanResolutionResult second = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "selector-free",
            outputCapacity: 8);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Same(first.MetadataPlan, second.MetadataPlan);
        Assert.Equal(
            first.MetadataPlan!.ResolutionToken,
            second.MetadataPlan!.ResolutionToken);
        Assert.Equal(1, source.LoadCount);
    }

    /// <summary>Read-only metadata lookup uses exact capacity and fails closed on ambiguous axes.</summary>
    [Fact]
    public void ResolveUniqueMetadataPlanUsesCapacityAndRejectsAmbiguity()
    {
        var alternateRoute = new CapabilityRouteIdentity(
            "NT51929",
            "standard-merge",
            "selector-free",
            "nt51929-standard-merge-alternate");
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(
                    new CanonicalCapabilityCatalogCandidate(
                        "canonical-capability-catalog",
                        "1.0.0",
                        new string('a', 64),
                        [
                            CreateDefinition(CreateCompiledComposition()),
                            CreateDefinition(
                                CreateCompiledComposition(
                                    alternateRoute.MapVariant,
                                    outputCapacity: 16),
                                alternateRoute),
                        ]))));
        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);

        MetadataPlanResolutionResult ambiguous = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "selector-free");
        MetadataPlanResolutionResult exact = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "selector-free",
            outputCapacity: 16);
        MetadataPlanResolutionResult unavailable = catalog.ResolveUniqueMetadataPlan(
            "NT51950",
            "standard-merge",
            "selector-free",
            outputCapacity: 16);
        MetadataPlanResolutionResult wrongWorkflow = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "ab-merge",
            "selector-free",
            outputCapacity: 16);
        MetadataPlanResolutionResult wrongCount = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "2-ic",
            outputCapacity: 16);

        Assert.False(ambiguous.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.RouteAmbiguous,
            ambiguous.Issue!.Code);
        Assert.True(exact.Succeeded);
        Assert.Equal(
            reload.Snapshot!.ResolutionToken,
            exact.MetadataPlan!.ResolutionToken);
        Assert.False(unavailable.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.RouteUnavailable,
            unavailable.Issue!.Code);
        Assert.Equal(
            CapabilityCatalogIssueCodes.RouteUnavailable,
            wrongWorkflow.Issue!.Code);
        Assert.Equal(
            CapabilityCatalogIssueCodes.RouteUnavailable,
            wrongCount.Issue!.Code);
    }
}
