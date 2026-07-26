using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Tests the first canonical route and the remaining one-way migration seam.</summary>
public sealed class CanonicalCapabilityCatalogMigrationTests
{
    /// <summary>The trusted source joins policy references to the existing compiler output.</summary>
    [Fact]
    public void SourceMaterializesNt51929WithoutCopyingFirmwareFacts()
    {
        var source = new CanonicalCapabilityCatalogMigrationSource();

        CapabilityCatalogLoadResult loaded =
            source.Load(TestContext.Current.CancellationToken);
        CanonicalCapabilityDefinition definition =
            Assert.Single(loaded.Candidate!.Definitions);
        CompiledComposition composition = definition.CompiledComposition;

        Assert.True(loaded.Succeeded);
        Assert.Equal("NT51929", definition.Identity.IcId);
        Assert.Equal("nt51929-standard-merge-256k", definition.Identity.MapVariant);
        Assert.Equal(definition.CapabilityFingerprint, composition.CompilationFingerprint);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        Assert.Equal(0x40000, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(
            ["copy-tp", "copy-dp"],
            composition.Plan.OrderedOperations.Select(static operation => operation.OperationId));
        Assert.All(
            composition.Plan.OrderedOperations,
            static operation => Assert.Null(operation.ExternalProcessorInvocation));
    }

    /// <summary>A stale policy fingerprint rejects the complete candidate.</summary>
    [Fact]
    public void SourceRejectsStaleCapabilityFingerprint()
    {
        CanonicalCapabilityPolicySnapshot current =
            BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicyRoute route = Assert.Single(current.Routes);
        string staleFingerprint = new('0', 64);
        CanonicalCapabilityPolicyRoute staleRoute = route with
        {
            CapabilityFingerprint = staleFingerprint,
            Authoring = Rebind(route.Authoring, staleFingerprint),
            Publication = Rebind(route.Publication, staleFingerprint),
            Evidence = Rebind(route.Evidence, staleFingerprint),
        };
        var source = new CanonicalCapabilityCatalogMigrationSource(
            () => current with { Routes = [staleRoute] });

        CapabilityCatalogLoadResult loaded =
            source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.SourceInvalid,
            Assert.Single(loaded.Issues).Code);
    }

    /// <summary>Workbench availability and compilation consume the same published capability and token.</summary>
    [Fact]
    public void Nt51929CompilationUsesPublishedCanonicalSnapshot()
    {
        CapabilityCatalogReloadResult reload =
            WorkbenchCompositionService.ReloadCanonicalCapabilityCatalog(
                TestContext.Current.CancellationToken);
        CapabilityResolutionResult resolution =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(
                "NT51929");
        bool available = WorkbenchCompositionService.IsStandardMergeSupported(
            "NT51929");
        CapabilityResolutionResult afterAvailability =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(
                "NT51929");

        bool recognized = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT51929",
            dpInputLength: 0x40000,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        CapabilityResolutionResult afterCompile =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(
                "NT51929");

        Assert.True(reload.Succeeded);
        Assert.True(resolution.Succeeded);
        Assert.True(available);
        Assert.True(recognized);
        Assert.Empty(issues);
        Assert.Same(resolution.Capability!.CompiledComposition, composition);
        Assert.Equal(
            resolution.Capability.ResolutionToken,
            afterAvailability.Capability!.ResolutionToken);
        Assert.Equal(
            resolution.Capability.ResolutionToken,
            afterCompile.Capability!.ResolutionToken);
    }

    /// <summary>Non-pilot routes remain executable only through the named migration adapter.</summary>
    [Fact]
    public void OtherStandardMergeRoutesRemainBehindMigrationAdapter()
    {
        bool recognized = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT51930",
            dpInputLength: 0x40000,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        CapabilityResolutionResult canonical =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(
                "NT51930");

        Assert.True(recognized);
        Assert.NotNull(composition);
        Assert.Empty(issues);
        Assert.False(canonical.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, canonical.Issue!.Code);
    }

    private static PinnedCapabilityDecision<TValue> Rebind<TValue>(
        PinnedCapabilityDecision<TValue> decision,
        string fingerprint)
        where TValue : struct, Enum
    {
        return new PinnedCapabilityDecision<TValue>(
            decision.DecisionId,
            decision.RouteId,
            fingerprint,
            decision.Value,
            decision.SourceReference);
    }
}
