// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap.Tests.MemoryLayout;

/// <summary>Reuses the trusted built-in registrations and compiler without private firmware or execution.</summary>
internal static class BuiltInCompositionTestProfiles
{
    /// <summary>Compiles the requested existing Standard Merge fixture and its canonical metadata plan.</summary>
    internal static (CompiledComposition Composition, MetadataPlanDefinition MetadataPlan) StandardMerge(string icId = "NT51923")
    {
        BuiltInV2Registration registration = BuiltInV2RegistrationRegistry.StandardMergeByIc[icId];
        registration.TryCompile(262144, out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues);
        CompiledComposition accepted = RequireComposition(composition, issues);
        return (accepted, registration.CreateMetadataPlan(accepted));
    }

    /// <summary>Compiles an exact existing AB layout registration without reading firmware files.</summary>
    internal static (CompiledComposition Composition, MetadataPlanDefinition MetadataPlan) AbMerge(
        string icId, string mapVariantSetId, long capacity, int topologyCount = 0)
    {
        BuiltInV2Registration registration = BuiltInV2RegistrationRegistry.FindAbMergeRegistration(icId, mapVariantSetId)
            ?? throw new InvalidOperationException("The requested built-in AB fixture is missing.");
        TopologySelection? topology = topologyCount == 0 ? null : new(topologyCount,
            topologyCount == 1 ? "single" : "cascade", TopologySelectionSource.Requested, "number-selector");
        registration.TryCompile(capacity, topology, ["dp-ab-input", "tp-a-input", "tp-b-input"],
            out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues);
        CompiledComposition accepted = RequireComposition(composition, issues);
        return (accepted, registration.CreateMetadataPlan(accepted));
    }

    /// <summary>Compiles the existing NT51923 single-chip CtrlRAM profile with one synthetic mapping.</summary>
    internal static (CompiledComposition Composition, MetadataPlanDefinition MetadataPlan) CtrlRamReplace()
    {
        CtrlRamV2Route route = CtrlRamV2RouteRegistry.All.SingleOrDefault(static route =>
            route.Key.IcId == "NT51923" && route.Key.Branch ==
                NvtFwCombiner.Application.ExternalTools.LegacyCombinerPostbuildBranch.SingleChip)
            ?? throw new InvalidOperationException("The built-in NT51923 single-chip fixture is missing.");
        BuiltInV2Bundle bundle = BuiltInV2BundleRegistry.All[route.BundleId];
        IReadOnlyList<FirmwareImageMap> maps = bundle.GetMapVariants(route.ProfileId, route.ProfileVersion,
            route.Key.IcId, ExperienceIds.CtrlRamReplace, out IReadOnlyList<CompositionIssue> issues);
        FirmwareImageMap map = maps.SingleOrDefault(static map => map.CapacityBytes == 0x3C000)
            ?? throw new InvalidOperationException(string.Join(", ", issues.Select(static issue => issue.Message)));
        FirmwareRegion target = map.Regions.First(static region => region.RegionId == "normal-ctrlram");
        byte[] reference = new byte[checked((int)map.CapacityBytes)];
        // Same synthetic count and NVT envelope used by the existing plan-closure fixture.
        reference[23] = 1;
        reference[0x0FFD] = 0x4E;
        reference[0x0FFE] = 0x56;
        reference[0x0FFF] = 0x54;
        V2CompositionPlanCompileResult result = bundle.CompileRuntimeReferenceReplace(
            route.ProfileId, route.ProfileVersion, route.Key.IcId, ExperienceIds.CtrlRamReplace,
            new TopologySelection(1, "single", TopologySelectionSource.Requested, "number-selector"),
            [new FirmwareArtifactPayload("reference-base", reference)],
            new V2RuntimeReferenceReplaceCompileRequest(
                [
                    new V2ExplicitMappingInputBinding("reference-base", "reference-base", map.CapacityBytes),
                    new V2ExplicitMappingInputBinding("ctrlram-source-1", "ctrlram-source", 1),
                ],
                [new ExplicitMapping("replace-ctrlram-byte", 1, ExplicitMappingOperationKind.ReplaceRange,
                    "ctrlram-source-1", new ByteRange(0, 1), CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(target.Range.Start, 1), OverlapPolicy.Reject, 1,
                    "Synthetic memory-layout fixture mapping")]));
        CompiledComposition accepted = RequireComposition(result.CompiledComposition, result.Issues);
        return (accepted, bundle.CreateMetadataPlan(route.ProfileId, route.ProfileVersion, accepted));
    }

    private static CompiledComposition RequireComposition(CompiledComposition? composition,
        IReadOnlyList<CompositionIssue> issues)
    {
        return composition is not null && issues.Count == 0
            ? composition
            : throw new InvalidOperationException(string.Join(", ", issues.Select(static issue => issue.Message)));
    }
}
