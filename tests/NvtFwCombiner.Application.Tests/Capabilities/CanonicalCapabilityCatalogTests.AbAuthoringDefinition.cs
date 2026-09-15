using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.Capabilities;

public sealed partial class CanonicalCapabilityCatalogTests
{
    /// <summary>A slot and its address-space alias cannot become two accepted inputs or reach compilation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbAliasCollisionIsRejectedBeforeCompilationAsync(bool inspection)
    {
        (CanonicalCapabilityCatalog catalog, _) = AbDeclarationCatalog();
        var adapter = new DeclarationOnlyAdapter(AbDeclaration("none"));
        var owner = new AbMergeAuthoringExperience(new CanonicalCapabilityCompilerAdapter(catalog, adapter), catalog, adapter);
        if (inspection)
        {
            AbMergeInspectionBatch result = await owner.InspectInputSlotsAsync("NT51951",
                [new("a", "a.bin", AbMergeAddressSpaceId: "input-a"), new("alias", "alias.bin", AbMergeAddressSpaceId: "space-a")],
                _ => new byte[16], TestContext.Current.CancellationToken);
            Assert.Equal("AB_FORMAT_INPUT_INVALID", Assert.Single(result.Issues).Code);
            Assert.Empty(result.Statuses);
            Assert.Empty(result.Facts);
        }
        else
        {
            var session = new AuthoringSessionState(ExperienceIds.AbMerge);
            CompiledAuthoringSessionPreparation result = await owner.PrepareSessionAsync(session, "NT51951", null,
                [new("input-a", "a.bin", new byte[16]), new("space-a", "alias.bin", new byte[16])],
                AbMergeDpMode.Normal, TestContext.Current.CancellationToken);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Issues, static issue => issue.Code == "AB_FORMAT_INPUT_INVALID");
            Assert.Null(session.CurrentSnapshot?.ExactCapability);
        }
    }

    /// <summary>Every actual-source identity and member bound must match the same publication.</summary>
    [Theory]
    [InlineData("none", true)]
    [InlineData("profile", false)]
    [InlineData("version", false)]
    [InlineData("hash", false)]
    [InlineData("member", false)]
    [InlineData("map", false)]
    [InlineData("group", false)]
    public void AbDeclarationRequiresExactCurrentContract(string mutation, bool accepted)
    {
        (CanonicalCapabilityCatalog catalog, ResolvedCapabilityRoute route) = AbDeclarationCatalog();
        var adapter = new DeclarationOnlyAdapter(AbDeclaration(mutation));
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, adapter);
        Assert.Equal(accepted, compiler.TryGetAbAuthoringDefinition(route, out CanonicalAbAuthoringDefinition? definition,
            out IReadOnlyList<CompositionIssue> issues));
        Assert.Equal(1, adapter.Calls);
        Assert.Same(route.Identity, adapter.Identity);
        if (accepted)
        {
            Assert.Same(adapter.Definition, definition);
            Assert.Empty(issues);
        }
        else
        {
            Assert.Null(definition);
            Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, Assert.Single(issues).Code);
        }
    }

    /// <summary>Neither a retained route nor a reload during the adapter call lends current authority.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbDeclarationRejectsPublicationRollover(bool duringQuery)
    {
        (CanonicalCapabilityCatalog catalog, ResolvedCapabilityRoute route) = AbDeclarationCatalog();
        void Reload()
        {
            Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        }
        var adapter = new DeclarationOnlyAdapter(AbDeclaration("none"), duringQuery ? Reload : null);
        if (!duringQuery) { Reload(); }
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, adapter);
        Assert.False(compiler.TryGetAbAuthoringDefinition(route, out CanonicalAbAuthoringDefinition? definition,
            out IReadOnlyList<CompositionIssue> issues));
        Assert.Null(definition);
        Assert.NotEmpty(issues);
        Assert.Equal(duringQuery ? 1 : 0, adapter.Calls);
    }

    /// <summary>Declaration lists retain their original facts and never silently repair duplicate members.</summary>
    [Fact]
    public void AbDeclarationSnapshotsInputsAndRejectsDuplicateGroupMembers()
    {
        CanonicalAbAuthoringDefinition source = AbDeclaration("none");
        CompiledAuthoringInputBinding[] bindings = [.. source.InputBindings];
        string[] members = ["input-a"];
        var result = new CanonicalAbAuthoringDefinition(source.ProfileId, source.ProfileVersion, source.BundleContentHash,
            source.Family, bindings, null, null, members);
        bindings[0] = new("changed", "changed");
        members[0] = "input-b";
        Assert.Equal("input-a", result.InputBindings[0].SlotId);
        Assert.Equal("input-a", Assert.Single(result.SelectionGroupMemberSlotIds));
        _ = Assert.Throws<ArgumentException>(() => new CanonicalAbAuthoringDefinition(source.ProfileId, source.ProfileVersion,
            source.BundleContentHash, source.Family, source.InputBindings, null, null, ["input-a", "input-a"]));
    }

    private static (CanonicalCapabilityCatalog Catalog, ResolvedCapabilityRoute Route) AbDeclarationCatalog()
    {
        var identity = new CapabilityRouteIdentity("NT51951", ExperienceIds.AbMerge, "selector-free", "definition-map-set");
        var contract = new CanonicalCapabilityCompilationContract("synthetic-ab", "1.0.0", new string('a', 64),
            ["synthetic-ab-map"], CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId, ["input-a"]);
        var candidate = new CanonicalCapabilityCatalogCandidate("ab-definition-test", "1.0.0", new string('a', 64), [],
            [CreateDynamicAbDefinition(identity, compilationContract: contract)]);
        var catalog = new CanonicalCapabilityCatalog(new QueueCapabilitySource(
            CapabilityCatalogLoadResult.Success(candidate), CapabilityCatalogLoadResult.Success(candidate)));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        return (catalog, Assert.Single(catalog.GetCurrentSnapshot().DynamicRoutes));
    }

    private static CanonicalAbAuthoringDefinition AbDeclaration(string mutation)
    {
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            mutation == "map" ? "different-map" : "synthetic-ab-map", "flash",
            new FirmwareMapApplicability([mutation == "member" ? "NT51950" : "NT51951"], [ExperienceIds.AbMerge],
                TopologyRequirement.NoTopologyConstraint(), 16), FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet("physical", "flash",
                [new FirmwareRegion("root", null, FirmwareRegionOwner.System, FirmwareRegionKind.Image,
                    new ByteRange(0, 16), FirmwareWriteConstraint.Forbidden)], ["synthetic-test"])], [], ["synthetic-test"]);
        var family = new FirmwareFamilyResolutionDefinition("synthetic-family", "1.0.0", new string('c', 64), [map], []);
        return new(mutation == "profile" ? "wrong-profile" : "synthetic-ab", mutation == "version" ? "2.0.0" : "1.0.0",
            new string(mutation == "hash" ? 'b' : 'a', 64), family,
            [new("input-a", "space-a"), new("input-b", "space-b")], null, null,
            [mutation == "group" ? "input-b" : "input-a"]);
    }

    private sealed class DeclarationOnlyAdapter(CanonicalAbAuthoringDefinition definition, Action? afterQuery = null)
        : ICanonicalDynamicCompilationAdapter, IRuntimeDependencyReadinessLeaseProvider
    {
        public RuntimeDependencyReadinessLease AcquireCurrent()
        {
            throw new InvalidOperationException("Declaration query must not acquire runtime dependencies.");
        }

        internal CanonicalAbAuthoringDefinition Definition { get; } = definition;
        internal int Calls { get; private set; }
        internal CapabilityRouteIdentity? Identity { get; private set; }
        public bool TryGetAbAuthoringDefinition(CapabilityRouteIdentity identity,
            out CanonicalAbAuthoringDefinition? result, out IReadOnlyList<CompositionIssue> issues)
        {
            Calls++;
            Identity = identity;
            result = Definition;
            issues = [];
            afterQuery?.Invoke();
            return true;
        }
        public IReadOnlyList<long> GetMapCapacities(string icId, string workflowId, out IReadOnlyList<CompositionIssue> issues)
        {
            throw new InvalidOperationException("Declaration query must not ask for output capacity.");
        }
        public void Compile(CapabilityRouteIdentity identity, long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds, out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan, out IReadOnlyList<CompositionIssue> issues,
            TopologySelection? requestedTopology = null)
        {
            throw new InvalidOperationException("Declaration query must not compile.");
        }
        public void CompileDefinition(string icId, string workflowId, long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds, out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues)
        {
            throw new InvalidOperationException("Declaration query must not compile.");
        }
    }
}
