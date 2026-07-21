using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using V2CompositionPlanCompileResult = NvtFwCombiner.Profiles.V2.V2CompositionPlanCompileResult;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunCtrlRamReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        return await RunCtrlRamReplaceWithProcessorCoreAsync(
            icId,
            number,
            slotPaths,
            build,
            outputPath,
            firmwareVersionEdit,
            ExternalProcessorFactory.GetOrCreateOrNull(),
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<WorkbenchRunResult> RunCtrlRamReplaceWithProcessorAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit,
        IExternalProcessor? externalProcessor,
        CancellationToken cancellationToken)
    {
        return await RunCtrlRamReplaceWithProcessorCoreAsync(
            icId,
            number,
            slotPaths,
            build,
            outputPath,
            firmwareVersionEdit,
            externalProcessor,
            progress: null,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<WorkbenchRunResult> RunCtrlRamReplaceWithProcessorCoreAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit,
        IExternalProcessor? externalProcessor,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        CtrlRamReplaceRunContext context = CreateCtrlRamReplaceRunContext(
            icId,
            number,
            slotPaths,
            firmwareVersionEdit);
        WorkbenchRunResult Blocked(
            IReadOnlyList<CompositionIssue> issues,
            string? outputFileName = null)
        {
            return CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.CtrlRam,
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(
                    icId,
                    context.Selection,
                    context.Sources,
                    slotPaths,
                    runnablePreview: false,
                    context.PostbuildProfile,
                    context.CommandPlan),
                issues,
                outputFileName ?? GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.CtrlRam));
        }

        if (context.ValidationIssues.Count != 0 || context.BasePath is null || context.BaseBytes is null ||
            context.PostbuildProfile is null ||
            context.CommandPlan is null)
        {
            return Blocked(context.ValidationIssues);
        }

        byte[] referenceBytes = context.BaseBytes;
        var referencePayload = new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, referenceBytes);
        WorkbenchFirmwareContextSuggestion? firmware = ReadFirmwareContextSuggestion(icId, referenceBytes);
        (string? v2ProfileId, string v2ProfileVersion) = (
            context.PostbuildProfile!.IcId,
            context.PostbuildProfile!.ProcessorId,
            context.CommandPlan!.Branch,
            context.Selection.Mode,
            firmware?.CommonFwVersion,
            firmware?.ChipNumber,
            firmware?.ProjectId) switch
        {
            ("NT51919", "nfc.nt51919.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x4703) =>
                ("nt51919-ctrlram-replace-fw200-single", "0.2.0"),
            ("NT51917", "nfc.nt51917.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.4.1", 1, 0x5709) =>
                ("nt51917-ctrlram-replace-fw141-single", "0.2.0"),
            ("NT51917", "nfc.nt51917.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip, IcNumberInputMode.NumericSelector, "1.3.2", 2, 0x1615) =>
                ("nt51917-ctrlram-replace-fw132-twochip", "0.2.0"),
            ("NT51917", "nfc.nt51917.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.ThreeChip, IcNumberInputMode.NumericSelector, "1.4.0", 3, 0x570A) =>
                ("nt51917-ctrlram-replace-fw140-threechip", "0.2.0"),
            ("NT51920", "nfc.nt51920.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.2.0", 1, 0xF401) =>
                ("nt51920-ctrlram-replace-fw120-single", "0.2.0"),
            ("NT51920", "nfc.nt51920.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.2.0", 2, 0x1403) =>
                ("nt51920-ctrlram-replace-fw120-cascade2", "0.2.0"),
            ("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.4.1", 1, 0x6005) =>
                ("nt51923-ctrlram-replace-fw141-single", "0.2.0"),
            ("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.4.1", 3, 0x4C03) =>
                ("nt51923-ctrlram-replace-fw141-cascade3", "0.2.0"),
            ("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.4.1", 1, 0x5709) =>
                ("nt51927-ctrlram-replace-fw141-single", "0.2.0"),
            ("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip, IcNumberInputMode.NumericSelector, "1.3.2", 2, 0x1615) =>
                ("nt51927-ctrlram-replace-fw132-twochip", "0.2.0"),
            ("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.ThreeChip, IcNumberInputMode.NumericSelector, "1.4.0", 3, 0x570A) =>
                ("nt51927-ctrlram-replace-fw140-threechip", "0.2.0"),
            ("NT51928", "nfc.nt51928.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip, IcNumberInputMode.NumericSelector, "1.3.2", 2, 0xF206) =>
                ("nt51928-ctrlram-replace-fw132-twochip", "0.2.0"),
            ("NT51926", "nfc.nt51926.ctrlram-postbuild-fw1.4.1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, _, > 1, _) =>
                ("nt51926-ctrlram-replace-fw141-runtime-cascade", "0.2.0"),
            ("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x1309) =>
                ("nt51926-ctrlram-replace-fw200-runtime-single", "0.2.0"),
            ("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "2.0.0", 3, 0x1309) =>
                ("nt51926-ctrlram-replace-fw200-runtime-cascade", "0.2.0"),
            ("NT51929", "nfc.nt51929.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x4703) =>
                ("nt51929-ctrlram-replace-fw200-single", "0.2.0"),
            ("NT51930", "nfc.nt51930.ctrlram-postbuild-fw1.x", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.3.0", 3, 0x110D) =>
                ("nt51930-ctrlram-replace-fw130-cascade3", "0.2.0"),
            ("NT51931", "nfc.nt51931.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.3.0", 6, 0x131B) =>
                ("nt51931-ctrlram-replace-fw130-cascade6", "0.2.0"),
            ("NT51932", "nfc.nt51932.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "2.0.0", 3, 0x5601) =>
                ("nt51932-ctrlram-replace-fw200-cascade3", "0.2.0"),
            ("NT51950", "nfc.nt51950.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x4A06) =>
                ("nt51950-ctrlram-replace-fw200-single", "0.2.0"),
            ("NT51951", "nfc.nt51951.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x5901) =>
                ("nt51951-ctrlram-replace-fw200-single", "0.2.0"),
            _ => (null, "0.1.0"),
        };
        if (v2ProfileId is not null)
        {
            V2CompositionPlanCompileResult v2Compile = CompileCtrlRamV2(context, v2ProfileId, v2ProfileVersion,
                new(firmware!.ChipNumber, firmware.NumberToken, TopologySelectionSource.Requested, "ic-number"), referencePayload);
            return !v2Compile.IsCompiled
                ? Blocked(v2Compile.Issues, $"{icId.ToLowerInvariant()}-ctrlram-replace.bin")
                : await RunCompiledCompositionAsync(
                    CtrlRamReplaceRunIdPrefix,
                    v2Compile.CompiledComposition!,
                    CreateCtrlRamReplaceBindings(
                        v2Compile.CompiledComposition!,
                        context,
                        slotPaths),
                    context.BasePath!,
                    build,
                    outputPath,
                    externalProcessor,
                    icNumberSelection: context.Selection,
                    overwrite: true,
                    cancellationToken,
                    virtualArtifacts: new Dictionary<string, byte[]>(StringComparer.Ordinal)
                    {
                        [context.BasePath!] = referenceBytes,
                    },
                    progress: progress).ConfigureAwait(false);
        }

        return Blocked(
            [
                new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                    "The selected CtrlRAM Replace shape has no exact evidence-backed V2 route.",
                    "number"),
            ]);
    }
}
