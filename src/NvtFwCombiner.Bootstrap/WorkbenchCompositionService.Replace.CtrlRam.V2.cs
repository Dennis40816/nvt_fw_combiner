using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string Nt51926Fw200ProcessorId = "nfc.nt51926.ctrlram-postbuild-v1";
    private const string Nt51920Fw120SingleBaseSha256 = "b9965def2946fd6e28165af5929ede885e1d0e3c0ab29266a737ac458225920d";
    private const string Nt51920Fw120Cascade2BaseSha256 = "681f904ecdf5785ca26f94eabb8191ddaa8976e0e6f750145475568c6cde4d43";
    private const string Nt51923Fw141SingleBaseSha256 = "a65ae33c9c11091f69d8935422ffc57db32262eb922590364d4bdd9c3af9916f";
    private const string Nt51923Fw141Cascade3BaseSha256 = "06dda13a592c151a767d47fff60da993f33d7bda37666794dd9ea5cf92094d18";
    private const string Nt51927Fw141SingleBaseSha256 = "fc4d2f9701c626b1c7cddd2b448970611d332295c64f86415af2855f1569c55a";
    private const string Nt51931Fw130ExactBaseSha256 = "2268ac5b49df546a03e177b97858805f0f83fa58b3e55a3b1590899ce9fd07c3";
    private const string Nt51932Fw200ExactBaseSha256 = "3eb556e0a9323dd4fbe4c703be1eb33679df2b1ba839e79ddd7bbffa235008fd";
    private const string Nt51951Fw200ExactBaseSha256 = "c1cd54d93af431727220adc37fec2488765909dc09cb917d1ff69f6087bb6b69";

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

        return BuiltInV2BundleRegistry.All[$"{context.PostbuildProfile!.IcId.ToLowerInvariant()}-ctrlram-replace-candidate"].CompileRuntimeReferenceReplace(
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
