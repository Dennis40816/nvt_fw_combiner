using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string Nt51926Fw200ProcessorId = "nfc.nt51926.ctrlram-postbuild-v1";

    private static V2CompositionPlanCompileResult CompileCtrlRamV2(
        CtrlRamReplaceRunContext context,
        string profileId,
        TopologySelection topology)
    {
        V2RuntimeReferenceReplaceInputBinding[] bindings =
        [
            new(
                CompositionAddressSpaceIds.ReferenceBase,
                CompositionAddressSpaceIds.ReferenceBase,
                context.BaseLength),
            .. context.SelectedSources.Select(source => new V2RuntimeReferenceReplaceInputBinding(
                CtrlRamSlotId(source.SourceId),
                "ctrlram-source",
                context.SelectedSourceLengths[source.SourceId])),
        ];
        ExplicitMapping[] mappings =
        [
            .. context.SelectedSources
                .SelectMany(source => source.Blocks
                    .OrderBy(static block => block.FirmwareRange.Start)
                    .ThenBy(static block => block.SourceOffset)
                    .Select(block => (Source: source, Block: block)))
                .Select((entry, index) =>
                {
                    long sourceLength = context.SelectedSourceLengths[entry.Source.SourceId];
                    long effectiveLength = Math.Min(
                        entry.Block.FirmwareRange.Length,
                        sourceLength - entry.Block.SourceOffset);
                    return new ExplicitMapping(
                    FormattableString.Invariant($"replace-{entry.Source.SourceId}-{index:D2}"),
                    100 + index,
                    ExplicitMappingOperationKind.ReplaceRange,
                    CtrlRamSlotId(entry.Source.SourceId),
                    new ByteRange(entry.Block.SourceOffset, effectiveLength),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(entry.Block.FirmwareRange.Start, effectiveLength),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "Copy the selected CtrlRAM source prefix into the exact physical range selected from the reference firmware.");
                }),
        ];

        string bundleId = context.PostbuildProfile!.IcId switch
        {
            "NT51917" => "nt51917-ctrlram-replace-alias-candidate",
            "NT51919" => "nt51929-ctrlram-replace-candidate",
            _ => $"{context.PostbuildProfile.IcId.ToLowerInvariant()}-ctrlram-replace-candidate",
        };
        return BuiltInV2BundleRegistry.All[bundleId].CompileRuntimeReferenceReplace(
            profileId,
            "0.1.0",
            context.PostbuildProfile.IcId,
            ExperienceIds.CtrlRamReplace,
            topology,
            [new FirmwareArtifactPayload(
                CompositionAddressSpaceIds.ReferenceBase,
                File.ReadAllBytes(context.BasePath!))],
            new V2RuntimeReferenceReplaceCompileRequest(bindings, mappings));
    }
}
