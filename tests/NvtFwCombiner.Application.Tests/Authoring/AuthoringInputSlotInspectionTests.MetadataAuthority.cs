using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class AuthoringInputSlotInspectionTests
{
    /// <summary>Fixed plans stay terminal while an uncompiled CtrlRAM base uses one bounded read-only query.</summary>
    [Fact]
    public void FirmwareMetadataAuthorityHasOneApplicationOwner()
    {
        ResolvedMetadataPlan genericPlan = MetadataPlanDefinition.Empty.Resolve(
            new ResolutionToken("generic-metadata-publication"));
        var query = new RecordingMetadataQuery(genericPlan);
        var resolver = new FirmwareMetadataPlanAuthorityResolver(query);
        ResolvedCapability standard = CreateCapability(ExperienceIds.StandardMerge);
        ResolvedCapability dp = CreateCapability(ExperienceIds.DpReplace);
        ResolvedCapability ctrlRam = CreateCapability(ExperienceIds.CtrlRamReplace);
        FirmwareInspectionStatusBatch standardBatch = Batch(standard);
        FirmwareInspectionStatusBatch dpBatch = Batch(dp);
        FirmwareInspectionStatusBatch ctrlRamBatch = Batch(ctrlRam);

        FirmwareMetadataPlanAuthority exactStandard = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "standard",
                "dp.bin",
                StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
            inputLength: 8,
            FirmwareInspectionStatusBatch.Empty,
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
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.True(rejectedRetained.IsApplicable);
        Assert.Null(rejectedRetained.Plan);
        Assert.Empty(query.Calls);

        FirmwareMetadataPlanAuthority exactDp = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "dp",
                "base.bin",
                DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
            inputLength: 8,
            dpBatch,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.Same(dp.MetadataPlan, exactDp.Plan);
        Assert.Empty(query.Calls);

        FirmwareMetadataPlanAuthority exactCtrlRamBase = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "ctrlram-base",
                "base.bin",
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
            inputLength: 8,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty,
            ctrlRamBatch);
        Assert.Same(ctrlRam.MetadataPlan, exactCtrlRamBase.Plan);
        Assert.Empty(query.Calls);

        FirmwareMetadataPlanAuthority ctrlRamReplacement = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput(
                "ctrlram-replacement",
                "nf.bin",
                CtrlRamReplaceAddressSpaceId: "replace-ctrlram-nf"),
            inputLength: 8,
            FirmwareInspectionStatusBatch.Empty,
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
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.Same(genericPlan, uncompiledCtrlRamBase.Plan);
        Assert.Equal(
            [new MetadataQueryCall("NT-HEADLESS", ExperienceIds.DpReplace, "1-ic", 7)],
            query.Calls);

        FirmwareMetadataPlanAuthority generic = resolver.Resolve(
            "NT-HEADLESS",
            new FirmwareInspectionSnapshotInput("generic", "base.bin"),
            inputLength: 8,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.Same(genericPlan, generic.Plan);
        Assert.Equal(
            [
                new MetadataQueryCall("NT-HEADLESS", ExperienceIds.DpReplace, "1-ic", 7),
                new MetadataQueryCall("NT-HEADLESS", ExperienceIds.DpReplace, "1-ic", 8),
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
            FirmwareInspectionStatusBatch.Empty,
            FirmwareInspectionStatusBatch.Empty);
        Assert.Null(ambiguous.Plan);
        Assert.Same(ambiguity, ambiguous.Issue);
        Assert.Equal(
            [
                new MetadataQueryCall("NT-HEADLESS", ExperienceIds.DpReplace, "1-ic", 7),
                new MetadataQueryCall("NT-HEADLESS", ExperienceIds.DpReplace, "1-ic", 8),
                new MetadataQueryCall("NT-HEADLESS", ExperienceIds.DpReplace, "1-ic", 9),
            ],
            query.Calls);
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
        : ICanonicalCapabilityQuery
    {
        internal List<MetadataQueryCall> Calls { get; } = [];

        internal MetadataPlanResolutionResult Result { get; set; } =
            new(plan, null);

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
