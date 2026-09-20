using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Owner-approved DP-free AB compilation; not certified Dummy Golden evidence.</summary>
public sealed class AbDummyDpCompilationTests
{
    /// <summary>Only explicit Dummy mode removes DP from the public authoring contract.</summary>
    [Theory]
    [InlineData("NT51919", null)]
    [InlineData("NT51929", null)]
    [InlineData("NT51932", null)]
    [InlineData("NT51950", "single")]
    [InlineData("NT51950", "cascade")]
    [InlineData("NT51951", null)]
    public void ExplicitDummyModeExposesOnlyTpBindings(string icId, string? topology)
    {
        CompiledAuthoringSelectionSnapshot result = BootstrapTestHost.Services.AbMergeAuthoring
            .GetAuthoringSnapshot(icId, topology, [], new Dictionary<string, FileStamp>(),
                new AuthoringRevision(1), dpMode: AbMergeDpMode.Dummy);
        Assert.Equal(["tp-a-input", "tp-b-input"], result.InputBindings.Select(static binding => binding.SlotId));
    }

    /// <summary>The runtime never invents NT51950's operator topology selection.</summary>
    [Fact]
    public void PublishedAbRuntimeRejectsMissingTopologyForNt51950()
    {
        Assert.False(BootstrapTestHost.Canonical.Compiler.TryCompileAbMerge("NT51950",
            out CompiledComposition? composition, out _));
        Assert.Null(composition);
    }

    /// <summary>Input selection is dynamic within each existing IC/topology map subset.</summary>
    [Theory]
    [InlineData("NT51919", "selector-free", "nt51919-ab-merge-512k", "nt51919-ab-merge-512k", 0)]
    [InlineData("NT51929", "selector-free", "nt51929-ab-merge-512k", "nt51929-ab-merge-512k", 0)]
    [InlineData("NT51932", "selector-free", "nt51932-ab-merge-512k", "nt51932-ab-merge-512k", 0)]
    [InlineData("NT51950", "1-ic", "nt51950-ab-merge-maps", "nt51950-ab-merge-512k", 1)]
    [InlineData("NT51950", "2-plus-ic", "nt51950-ab-merge-maps", "nt51950-ab-merge-1024k", 2)]
    [InlineData("NT51951", "selector-free", "nt51951-ab-merge-1024k", "nt51951-ab-merge-1024k", 0)]
    public void DynamicAbInventoryRestrictsTopologyMapSubset(string icId, string axis, string mapSet, string map, int count)
    {
        var identity = new CapabilityRouteIdentity(icId, ExperienceIds.AbMerge, axis, mapSet);
        Assert.True(CanonicalDynamicRouteInventory.IsDynamic(identity));
        CanonicalDynamicRoute route = CanonicalDynamicRouteInventory.Resolve(identity);
        Assert.Equal(map, Assert.Single(route.CompilationContract.AllowedMapVariantIds));
        Assert.Equal("dp-ab-input", Assert.Single(route.CompilationContract.SemanticBindingIds));
        Assert.Equal<int?>(count == 0 ? null : count, route.AbMergeTopologyChoice?.Selection.ChipCount);
        TestContext.Current.TestOutputHelper!.WriteLine(
            $"AB_DEFINITION {identity.IcId} {identity.IcCountVariant} {identity.MapVariant} {identity.RouteId} {route.CapabilityFingerprint}");
    }

    /// <summary>The registered adapter preserves explicit DP omission and active TP metadata.</summary>
    [Fact]
    public void RegisteredDynamicAdapterCompilesAbDummyWithoutDpMetadata()
    {
        var adapter = new BuiltInV2DynamicCompilationAdapter();
        adapter.Compile(new CapabilityRouteIdentity("NT51929", ExperienceIds.AbMerge,
            "selector-free", "nt51929-ab-merge-512k"), null, [],
            out CompiledComposition? composition, out MetadataPlanDefinition? metadata,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        Assert.NotNull(composition);
        Assert.NotNull(metadata);
        Assert.DoesNotContain(metadata.Entries, static entry => entry.SlotId == "dp-ab-input");
        Assert.Contains(metadata.Entries, static entry => entry.SlotId == "tp-a-input");
        Assert.Contains(metadata.Entries, static entry => entry.SlotId == "tp-b-input");
    }

    /// <summary>Optional DP changes only the seed and first-write policy, never the required TP inputs.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ab-merge-alias", 0x80000, 0, true)]
    [InlineData("NT51929", "nt51929-ab-merge", 0x80000, 0, true)]
    [InlineData("NT51932", "nt51932-ab-merge", 0x80000, 0, true)]
    [InlineData("NT51950", "nt51950-ab-merge", 0x80000, 1, true)]
    [InlineData("NT51950", "nt51950-ab-merge", 0x100000, 2, true)]
    [InlineData("NT51951", "nt51951-ab-merge", 0x100000, 0, true)]
    [InlineData("NT51919", "nt51919-ab-merge-alias", 0x80000, 0, false)]
    [InlineData("NT51929", "nt51929-ab-merge", 0x80000, 0, false)]
    [InlineData("NT51932", "nt51932-ab-merge", 0x80000, 0, false)]
    [InlineData("NT51950", "nt51950-ab-merge", 0x80000, 1, false)]
    [InlineData("NT51950", "nt51950-ab-merge", 0x100000, 2, false)]
    [InlineData("NT51951", "nt51951-ab-merge", 0x100000, 0, false)]
    public void SelectionPreservesTpAndUsesSeedOnlyWhenSelected(
        string icId, string profileId, int capacity, int chipCount, bool dummy)
    {
        ArgumentNullException.ThrowIfNull(icId);
        bool legacyProcessorFamily = icId is "NT51950" or "NT51951";
        string bundle = legacyProcessorFamily
            ? "nt51950-ab-merge"
            : "nt51919-nt51929-nt51932-ab-merge";
        string hash = legacyProcessorFamily
            ? "8503cab05cc6b038f4b4093924f75cf6ca38f0ec178d30732fd23608dbbd699f"
            : "5acf2fd4d0757d7b757bf7491ff2f268d07cf70a76588f528d36b616e1e5eed0";
        using var workspace = TempWorkspace.Create("nfc-ab-dummy-compilation");
        TrustedProfileBundleCatalog catalog = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(
            workspace, bundle, hash);
        TopologySelection? topology = chipCount == 0
            ? null
            : new TopologySelection(chipCount, "test", TopologySelectionSource.Requested, "test");

        V2CompositionPlanCompileResult result = catalog.Compile(
            profileId, legacyProcessorFamily ? "0.6.0" : "0.4.0",
            icId, ExperienceIds.AbMerge, capacity, topology, [],
            selectedInputSlotIds: dummy ? [] : ["dp-ab-input"]);

        Assert.True(result.IsCompiled, string.Join(" | ", result.Issues.Select(static issue => issue.Message)));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Equal(capacity, composition.Plan.OutputInitialization.Capacity);
        string[] expectedSlots = dummy ? ["tp-a-input", "tp-b-input"] : ["dp-ab-input", "tp-a-input", "tp-b-input"];
        Assert.Equal(
            expectedSlots,
            composition.V2Details.InputContract.Slots.Select(static slot => slot.SlotId));
        Assert.Equal(!dummy, composition.Plan.OrderedOperations.Any(
            static operation => operation.OperationId == "copy-dp-ab-image"));
        string[] firstTpWrites = legacyProcessorFamily
            ? ["overlay-tpa-into-output", "overlay-tpb-into-output"]
            : ["copy-tpa", "copy-tpb"];
        foreach (string operationId in firstTpWrites)
        {
            CompositionOperation operation = Assert.Single(composition.Plan.OrderedOperations,
                operation => operation.OperationId == operationId);
            Assert.Equal(dummy ? OverlapPolicy.Reject : OverlapPolicy.ReplaceExisting, operation.OverlapPolicy);
        }
        if (legacyProcessorFamily)
        {
            CompositionOperation[] imports = [.. composition.Plan.OrderedOperations.Where(
                static operation => operation.OperationId.StartsWith("import-postbuild-", StringComparison.Ordinal))];
            Assert.Equal(3, imports.Length);
            Assert.All(imports, static operation => Assert.Equal(OverlapPolicy.ReplaceExisting, operation.OverlapPolicy));
        }

        var adapter = new BuiltInV2DynamicCompilationAdapter();
        string mapSet = icId == "NT51950" ? "nt51950-ab-merge-maps"
            : icId == "NT51951" ? "nt51951-ab-merge-1024k" : $"{icId.ToLowerInvariant()}-ab-merge-512k";
        var identity = new CapabilityRouteIdentity(icId, ExperienceIds.AbMerge,
            chipCount == 0 ? "selector-free" : chipCount == 1 ? "1-ic" : "2-plus-ic", mapSet);
        adapter.Compile(identity, capacity,
            dummy ? [] : ["dp-ab-input"],
            out CompiledComposition? routed, out MetadataPlanDefinition? metadata,
            out IReadOnlyList<CompositionIssue> issues, topology);
        Assert.Empty(issues);
        Assert.NotNull(routed);
        Assert.NotNull(metadata);
        Assert.Equal(capacity, routed.Plan.OutputInitialization.Capacity);
        Assert.Equal(composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId,
            routed.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(expectedSlots, routed.V2Details.InputContract.Slots.Select(static slot => slot.SlotId));
        // AB metadata remains TP-owned in Normal and Dummy modes: 929/932
        // have header bindings; 950/951 have independent primary observations.
        Assert.DoesNotContain(metadata.Entries, static entry => entry.SlotId == "dp-ab-input");
        if (icId is "NT51929" or "NT51932")
        {
            Assert.Equal(5, metadata.Entries.Count);
            Assert.Contains(metadata.Entries, static entry => entry.SlotId == "tp-a-input");
            Assert.Contains(metadata.Entries, static entry => entry.SlotId == "tp-b-input");
        }
        else if (icId is "NT51950" or "NT51951")
        {
            Assert.Equal(["tp-a-input", "tp-b-input"], metadata.Entries.Select(static entry => entry.SlotId));
            Assert.All(metadata.Entries, static entry =>
                Assert.Equal([MetadataReferencePurpose.Inspection], entry.Purposes));
        }
        else
        {
            Assert.Empty(metadata.Entries);
        }
    }

    /// <summary>Explicit topology never overrides incompatible capacity or a non-AB registration.</summary>
    [Theory]
    [InlineData("NT51950", ExperienceIds.AbMerge, 0x80000, 2)]
    [InlineData("NT51950", ExperienceIds.AbMerge, 0x100000, 1)]
    [InlineData("NT51929", ExperienceIds.StandardMerge, 0x40000, 1)]
    public void RegisteredDynamicAdapterRejectsTopologyConflicts(
        string icId, string workflowId, int capacity, int chipCount)
    {
        var adapter = new BuiltInV2DynamicCompilationAdapter();
        var topology = new TopologySelection(chipCount, "test", TopologySelectionSource.Requested, "test");
        var identity = new CapabilityRouteIdentity(icId, workflowId,
            workflowId == ExperienceIds.AbMerge ? chipCount == 1 ? "1-ic" : "2-plus-ic" : "selector-free",
            workflowId == ExperienceIds.AbMerge ? "nt51950-ab-merge-maps" : "nt51929-standard-merge-256k");
        adapter.Compile(identity, capacity, [],
            out CompiledComposition? composition, out MetadataPlanDefinition? metadata,
            out IReadOnlyList<CompositionIssue> issues, topology);
        Assert.Null(composition);
        Assert.Null(metadata);
        Assert.NotEmpty(issues);
        if (workflowId != ExperienceIds.AbMerge)
        {
            Assert.Equal("profile.v2.builtin.topology-not-admitted", Assert.Single(issues).Code);
        }
    }

    /// <summary>Omitting optional selection at the AB adapter keeps the compatibility Normal default.</summary>
    [Fact]
    public void RegisteredDynamicAdapterNullSelectionKeepsDp()
    {
        var adapter = new BuiltInV2DynamicCompilationAdapter();
        adapter.Compile(new CapabilityRouteIdentity("NT51929", ExperienceIds.AbMerge,
            "selector-free", "nt51929-ab-merge-512k"), null, null,
            out CompiledComposition? composition, out MetadataPlanDefinition? metadata,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        Assert.NotNull(composition);
        Assert.NotNull(metadata);
        Assert.Contains(composition.V2Details.InputContract.Slots, static slot => slot.SlotId == "dp-ab-input");
        Assert.Equal(5, metadata.Entries.Count);
    }
}
