using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string Nt51926Fw141ProcessorId = "nfc.nt51926.ctrlram-postbuild-fw1.4.1";
    private const string Nt51926Fw141RuntimeProfileId = "nt51926-ctrlram-replace-fw141-runtime-cascade";

    private static bool IsNt51926Fw141CascadeV2Route(CtrlRamReplaceRunContext context)
    {
        return context.Selection.Mode == IcNumberInputMode.CascadeSelector &&
            context.CommandPlan!.Branch == LegacyCombinerPostbuildBranch.Cascade &&
            StringComparer.Ordinal.Equals(context.PostbuildProfile!.ProcessorId, Nt51926Fw141ProcessorId);
    }

    private static V2CompositionPlanCompileResult CompileNt51926Fw141CascadeV2(
        CtrlRamReplaceRunContext context,
        out byte[] referenceBytes)
    {
        referenceBytes = File.ReadAllBytes(context.BasePath!);
        List<V2RuntimeReferenceReplaceInputBinding> bindings =
        [
            new(
                CompositionAddressSpaceIds.ReferenceBase,
                CompositionAddressSpaceIds.ReferenceBase,
                context.BaseLength),
        ];
        List<ExplicitMapping> mappings = [];
        int mappingIndex = 0;
        int sequence = 100;
        foreach (TpCtrlRamPostbuildSource source in context.SelectedSources)
        {
            string sourceSpaceId = CtrlRamSlotId(source.SourceId);
            long sourceLength = context.SelectedSourceLengths[source.SourceId];
            bindings.Add(new V2RuntimeReferenceReplaceInputBinding(
                sourceSpaceId,
                "ctrlram-source",
                sourceLength));

            foreach (LegacyCombinerBlockArgument block in source.Blocks
                         .OrderBy(static block => block.FirmwareRange.Start)
                         .ThenBy(static block => block.SourceOffset))
            {
                long effectiveLength = Math.Min(
                    block.FirmwareRange.Length,
                    sourceLength - block.SourceOffset);
                mappings.Add(new ExplicitMapping(
                    FormattableString.Invariant($"replace-{source.SourceId}-{mappingIndex++:D2}"),
                    sequence++,
                    ExplicitMappingOperationKind.ReplaceRange,
                    sourceSpaceId,
                    new ByteRange(block.SourceOffset, effectiveLength),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(block.FirmwareRange.Start, effectiveLength),
                    OverlapPolicy.Reject,
                    alignment: 1,
                    reason: "Copy the selected CtrlRAM source prefix into its owner-approved NT51926 Common FW 1.4.1 physical range."));
            }
        }

        return BuiltInV2BundleRegistry.All["nt51926-ctrlram-replace-candidate"].CompileRuntimeReferenceReplace(
            Nt51926Fw141RuntimeProfileId,
            "0.1.0",
            "NT51926",
            ExperienceIds.CtrlRamReplace,
            requestedTopology: null,
            [new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, referenceBytes)],
            new V2RuntimeReferenceReplaceCompileRequest(bindings, mappings));
    }

    private static InputArtifactBinding[] CreateNt51926Fw141CascadeV2Bindings(
        CtrlRamReplaceRunContext context,
        IReadOnlyDictionary<string, string> slotPaths)
    {
        return
        [
            new(
                CompositionAddressSpaceIds.ReferenceBase,
                CompositionAddressSpaceIds.ReferenceBase,
                context.BasePath!,
                Path.GetFileName(context.BasePath!),
                CompiledInputArtifactClass.ReferenceImage),
            .. context.SelectedSources.Select(source =>
            {
                string sourceSpaceId = CtrlRamSlotId(source.SourceId);
                return new InputArtifactBinding(
                    sourceSpaceId,
                    sourceSpaceId,
                    Path.GetFullPath(slotPaths[sourceSpaceId]),
                    Path.GetFileName(slotPaths[sourceSpaceId]),
                    CompiledInputArtifactClass.CtrlRamReplacement);
            }),
        ];
    }
}
