using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;
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
        CancellationToken cancellationToken)
    {
        return await RunCtrlRamReplaceWithProcessorAsync(
            icId,
            number,
            slotPaths,
            build,
            outputPath,
            firmwareVersionEdit,
            ExternalProcessorFactory.CreateOrNull(),
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
                    context.PostbuildProfile),
                issues,
                outputFileName ?? GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.CtrlRam));
        }

        if (context.ValidationIssues.Count != 0 ||
            context.BasePath is null ||
            context.PostbuildProfile is null ||
            context.CommandPlan is null)
        {
            return Blocked(context.ValidationIssues);
        }

        WorkbenchFirmwareContextSuggestion? nt51926Firmware = firmwareVersionEdit is null
            ? TryReadFirmwareContextSuggestion("NT51926", context.BasePath!)
            : null;
        string? nt51926V2ProfileId = (
            context.PostbuildProfile!.ProcessorId,
            context.CommandPlan!.Branch,
            context.Selection.Mode,
            nt51926Firmware?.ChipNumber,
            nt51926Firmware?.ProjectId) switch
        {
            ("nfc.nt51926.ctrlram-postbuild-fw1.4.1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, > 1, _) =>
                "nt51926-ctrlram-replace-fw141-runtime-cascade",
            (Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, 1, 0x1309) =>
                "nt51926-ctrlram-replace-fw200-runtime-single",
            (Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, 3, 0x1309) =>
                "nt51926-ctrlram-replace-fw200-runtime-cascade",
            _ => null,
        };
        if (nt51926V2ProfileId is not null)
        {
            var nt51926Topology = new TopologySelection(
                nt51926Firmware!.ChipNumber,
                nt51926Firmware.NumberToken,
                TopologySelectionSource.Requested,
                "ic-number");
            V2CompositionPlanCompileResult v2Compile = CompileNt51926CtrlRamV2(
                context,
                nt51926V2ProfileId,
                nt51926Topology);
            return !v2Compile.IsCompiled
                ? Blocked(v2Compile.Issues, "nt51926-ctrlram-replace.bin")
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
                    cancellationToken).ConfigureAwait(false);
        }

        ExternalProcessorStagedSourceBinding[] stagedSourceBindings = CreateCtrlRamStagedSourceBindings(
            context.SelectedSources,
            context.SelectedSourceLengths);
        IReadOnlyList<LegacyCombinerPostbuildWriteRange> postbuildWriteRangeSections =
            LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForStagedSources(
            context.CommandPlan!,
            context.BaseLength,
            stagedSourceBindings.Select(binding => binding.FirmwareRange),
            context.Regions.Select(region => region.Range));
        if (postbuildWriteRangeSections.Count == 0)
        {
            return Blocked(
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.ReplaceCtrlRamPostbuildWriteRangeMissing,
                        "No approved postbuild write range could be derived from the legacy Combiner command plan.",
                        "postbuild"),
                ]);
        }

        CompositionProfileDefinition profile = CreateCtrlRamReplaceProfile(
            icId,
            context.Selection,
            context.BaseLength,
            context.Regions,
            context.SelectedSources,
            context.SelectedSourceLengths,
            stagedSourceBindings,
            context.PostbuildProfile!,
            context.CommandPlan!,
            postbuildWriteRangeSections,
            context.FirmwareVersionWritePlan);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            return Blocked(compile.Issues, profile.DefaultOutputFileName);
        }

        InputArtifactBinding[] bindings = CreateCtrlRamReplaceBindings(
            compile.CompiledComposition!,
            context,
            slotPaths);

        return await RunCompiledCompositionAsync(
            CtrlRamReplaceRunIdPrefix,
            compile.CompiledComposition!,
            bindings,
            context.BasePath!,
            build,
            outputPath,
            externalProcessor,
            icNumberSelection: context.Selection,
            overwrite: true,
            cancellationToken).ConfigureAwait(false);
    }
}
