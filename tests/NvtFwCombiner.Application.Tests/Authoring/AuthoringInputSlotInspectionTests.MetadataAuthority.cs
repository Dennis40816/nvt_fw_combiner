using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class AuthoringInputSlotInspectionTests
{
    /// <summary>Fixed plans stay terminal while a CtrlRAM base without DPCMI uses one bounded read-only query.</summary>
    [Fact]
    public void FirmwareMetadataAuthorityHasOneApplicationOwner()
    {
        ResolvedMetadataPlan genericPlan = MetadataPlanDefinition.Empty.Resolve(
            new ResolutionToken("generic-metadata-publication"));
        var query = new RecordingMetadataQuery(genericPlan);
        var resolver = new FirmwareMetadataPlanAuthorityResolver(query, query);
        ResolvedCapability standard = CreateCapability(ExperienceIds.StandardMerge);
        ResolvedCapability ctrlRam = CreateCapability(ExperienceIds.CtrlRamReplace);
        FirmwareInspectionStatusBatch standardBatch = Batch(standard);
        FirmwareInspectionStatusBatch ctrlRamBatch = Batch(ctrlRam);

        FirmwareMetadataPlanAuthority exactStandard = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "standard",
                "dp.bin",
                StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
            inputLength: 8,
            standardBatch,
            FirmwareInspectionStatusBatch.Empty);
        Assert.Same(standard.MetadataPlan, exactStandard.Plan);
        Assert.Empty(query.Calls);

        FirmwareMetadataPlanAuthority rejectedRetained = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "rejected",
                "dp.bin",
                StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput,
                ExactCapability: standard),
            inputLength: 8,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.True(rejectedRetained.IsApplicable);
        Assert.Null(rejectedRetained.Plan);
        Assert.Empty(query.Calls);

        FirmwareMetadataPlanAuthority exactCtrlRamBase = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "ctrlram-base",
                "base.bin",
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
            inputLength: 8,
            FirmwareInspectionStatusBatch.Empty,
            ctrlRamBatch);
        Assert.Same(genericPlan, exactCtrlRamBase.Plan);
        Assert.Equal(
            [new MetadataQueryCall("NT-HEADLESS", "full-image", "none", 8)],
            query.Calls);
        query.Calls.Clear();

        FirmwareMetadataPlanAuthority ctrlRamReplacement = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "ctrlram-replacement",
                "nf.bin",
                CtrlRamReplaceAddressSpaceId: "replace-ctrlram-nf"),
            inputLength: 8,
            FirmwareInspectionStatusBatch.Empty,
            ctrlRamBatch);
        Assert.Same(FirmwareMetadataPlanAuthority.NotApplicable, ctrlRamReplacement);
        Assert.Empty(query.Calls);

        FirmwareMetadataPlanAuthority ab = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "ab",
                "ab.bin",
                AbMergeAddressSpaceId: CompositionAddressSpaceIds.DpAbInput),
            inputLength: 8,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.Same(FirmwareMetadataPlanAuthority.NotApplicable, ab);
        Assert.Empty(query.Calls);

        FirmwareMetadataPlanAuthority uncompiledCtrlRamBase = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "uncompiled-ctrlram-base",
                "base.bin",
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
            inputLength: 7,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.Same(genericPlan, uncompiledCtrlRamBase.Plan);
        Assert.Equal(
            [new MetadataQueryCall("NT-HEADLESS", "full-image", "none", 7)],
            query.Calls);

        FirmwareMetadataPlanAuthority generic = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput("generic", "base.bin"),
            inputLength: 8,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.Same(genericPlan, generic.Plan);
        Assert.Equal(
            [
                new MetadataQueryCall("NT-HEADLESS", "full-image", "none", 7),
                new MetadataQueryCall("NT-HEADLESS", "full-image", "none", 8),
            ],
            query.Calls);

        var ambiguity = new CapabilityCatalogIssue(
            CapabilityCatalogIssueCodes.RouteAmbiguous,
            "Synthetic metadata ambiguity.");
        query.Result = new MetadataPlanResolutionResult(null, ambiguity);
        FirmwareMetadataPlanAuthority ambiguous = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput("ambiguous", "base.bin"),
            inputLength: 9,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.Null(ambiguous.Plan);
        Assert.Same(ambiguity, ambiguous.Issue);
        Assert.Equal(
            [
                new MetadataQueryCall("NT-HEADLESS", "full-image", "none", 7),
                new MetadataQueryCall("NT-HEADLESS", "full-image", "none", 8),
                new MetadataQueryCall("NT-HEADLESS", "full-image", "none", 9),
            ],
            query.Calls);
    }

    /// <summary>An unavailable metadata-only result stays terminal for a compiled CtrlRAM Base.</summary>
    [Fact]
    public void CtrlRamMetadataOnlyFailureIsNotReinterpreted()
    {
        var issue = new CapabilityCatalogIssue(CapabilityCatalogIssueCodes.RouteAmbiguous, "Ambiguous Base metadata.");
        var query = new RecordingMetadataQuery(MetadataPlanDefinition.Empty.Resolve(new ResolutionToken("metadata")))
        {
            Result = new MetadataPlanResolutionResult(null, issue),
        };
        var resolver = new FirmwareMetadataPlanAuthorityResolver(query, query);
        FirmwareMetadataPlanAuthority result = resolver.Resolve("NT-HEADLESS",
            new FirmwareInspectionSnapshotInput("base", "base.bin",
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase), 262144,
            FirmwareInspectionStatusBatch.Empty,
            Batch(CreateCapability(ExperienceIds.CtrlRamReplace)));
        Assert.True(result.IsApplicable);
        Assert.Null(result.Plan);
        Assert.Same(issue, result.Issue);
        Assert.Equal(
            [new MetadataQueryCall("NT-HEADLESS", "full-image", "none", 262144)],
            query.Calls);
    }

    /// <summary>A declared DPCMI plan remains the sole interpretation even when its data would fail.</summary>
    [Fact]
    public void CtrlRamDeclaredDpcmiPlanDoesNotUseGenericMetadata()
    {
        ResolvedMetadataPlan source = Metadata.FirmwareMetadataInspectorTests.CreateDpcmiPlan(expectedFirstByte: 0xAA);
        ResolvedCapability capability = CreateCapability(ExperienceIds.CtrlRamReplace, metadataPlan: source.Definition);
        var query = new RecordingMetadataQuery(MetadataPlanDefinition.Empty.Resolve(new ResolutionToken("unused")));
        var resolver = new FirmwareMetadataPlanAuthorityResolver(query, query);
        FirmwareMetadataPlanAuthority result = resolver.Resolve("NT-HEADLESS",
            new FirmwareInspectionSnapshotInput("base", "base.bin",
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase), 8,
            FirmwareInspectionStatusBatch.Empty, Batch(capability));
        Assert.True(result.IsApplicable);
        Assert.Same(capability.MetadataPlan, result.Plan);
        Assert.Empty(query.Calls);
        MetadataInspectionSnapshot inspection = FirmwareMetadataInspector.Inspect(result.Plan!,
            [new FirmwareArtifactPayload(CompositionAddressSpaceIds.DpReplacement, new byte[0x80])]);
        Assert.Equal(MetadataInspectionState.Invalid, Assert.Single(inspection.Results).State);
        Assert.False(DpcmiMetadataProjector.TryProject(inspection, out _));
        Assert.Empty(query.Calls);
    }

    /// <summary>A separately supplied TP keeps the established Standard plan instead of a full-image view.</summary>
    [Fact]
    public void DistinctTpMetadataKeepsStandardPlanQuery()
    {
        var query = new RecordingMetadataQuery(MetadataPlanDefinition.Empty.Resolve(new ResolutionToken("standard")));
        var resolver = new FirmwareMetadataPlanAuthorityResolver(query, query);
        FirmwareMetadataPlanAuthority result = resolver.Resolve("NT-HEADLESS",
            new FirmwareInspectionSnapshotInput("dp", "dp.bin", "tp.bin"), 8,
            FirmwareInspectionStatusBatch.Empty, FirmwareInspectionStatusBatch.Empty);
        Assert.Same(query.Result.MetadataPlan, result.Plan);
        Assert.Equal([new MetadataQueryCall("NT-HEADLESS", "source-envelope", "selector-free", 8)], query.DynamicCalls);
        Assert.Equal([new MetadataQueryCall("NT-HEADLESS", ExperienceIds.StandardMerge, "selector-free", 8)], query.Calls);
    }

    /// <summary>A dynamic Standard decision remains terminal and never changes to a full-image or static interpretation.</summary>
    [Fact]
    public void DistinctTpDynamicMetadataFailureDoesNotFallBack()
    {
        var query = new RecordingMetadataQuery(MetadataPlanDefinition.Empty.Resolve(new ResolutionToken("unused")));
        var issue = new CapabilityCatalogIssue(CapabilityCatalogIssueCodes.RouteAmbiguous,
            "The exact Standard map is ambiguous.");
        query.DynamicResult = new MetadataPlanResolutionResult(null, issue);
        var resolver = new FirmwareMetadataPlanAuthorityResolver(query, query);

        FirmwareMetadataPlanAuthority result = resolver.Resolve("NT-HEADLESS",
            new FirmwareInspectionSnapshotInput("dp", "dp.bin", "tp.bin"), 8,
            FirmwareInspectionStatusBatch.Empty, FirmwareInspectionStatusBatch.Empty);

        Assert.True(result.IsApplicable);
        Assert.Null(result.Plan);
        Assert.Same(issue, result.Issue);
        _ = Assert.Single(query.DynamicCalls);
        Assert.Empty(query.Calls);
    }

    private static FirmwareInspectionStatusBatch Batch(ResolvedCapability capability)
    {
        return new FirmwareInspectionStatusBatch(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(capability),
            new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal),
            []);
    }

    private sealed record MetadataQueryCall(
        string IcId,
        string WorkflowId,
        string IcCountVariant,
        long? OutputCapacity);

    private sealed class RecordingMetadataQuery(ResolvedMetadataPlan plan)
        : ICanonicalCapabilityQuery, IStandardMergeMetadataPlanQuery
    {
        internal List<MetadataQueryCall> Calls { get; } = [];

        internal List<MetadataQueryCall> DynamicCalls { get; } = [];

        internal MetadataPlanResolutionResult? DynamicResult { get; set; }

        internal MetadataPlanResolutionResult Result { get; set; } =
            new(plan, null);

        public MetadataPlanResolutionResult? ResolveSourceEnvelopeMetadataPlan(string icId, long dpInputLength)
        {
            DynamicCalls.Add(new MetadataQueryCall(icId, "source-envelope", "selector-free", dpInputLength));
            return DynamicResult;
        }

        public MetadataPlanResolutionResult ResolveFullImageMetadataPlan(string icId, long inputLength)
        {
            Calls.Add(new MetadataQueryCall(icId, "full-image", "none", inputLength));
            return Result;
        }

        public MetadataPlanResolutionResult ResolveUniqueMetadataPlan(
            string icId,
            string workflowId,
            string icCountVariant,
            long? outputCapacity = null)
        {
            Calls.Add(new MetadataQueryCall(
                icId,
                workflowId,
                icCountVariant,
                outputCapacity));
            return Result;
        }

        public CanonicalCapabilityCatalogSnapshot GetCurrentSnapshot()
        {
            throw new NotSupportedException();
        }

        public CanonicalCapabilityCatalogSnapshot? TryGetCurrentSnapshot()
        {
            throw new NotSupportedException();
        }

        public CapabilityResolutionResult Resolve(string routeId)
        {
            throw new NotSupportedException();
        }

        public CapabilityRouteResolutionResult ResolveDynamicRoute(string routeId)
        {
            throw new NotSupportedException();
        }

        public CapabilityResolutionResult ResolveUniqueRoute(
            string icId,
            string workflowId,
            string icCountVariant,
            long? outputCapacity = null)
        {
            throw new NotSupportedException();
        }

        public CapabilityResolutionResult ResolveUniqueTopologyRoute(
            string icId,
            string workflowId,
            TopologySelection? topology)
        {
            throw new NotSupportedException();
        }

        public bool HasAuthorableCapability(string icId, string workflowId)
        {
            throw new NotSupportedException();
        }

        public ResolvedCapability? ResolveCurrentCompilation(
            CompiledComposition composition,
            ResolvedCapability? acceptedCapability = null)
        {
            throw new NotSupportedException();
        }
    }
}
