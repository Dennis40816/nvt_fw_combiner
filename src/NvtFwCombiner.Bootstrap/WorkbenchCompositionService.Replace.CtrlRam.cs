using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

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

        if (!context.CanRun)
        {
            return Blocked(context.ValidationIssues);
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

        InputArtifactBinding[] bindings = CreateCtrlRamReplaceBindings(context, slotPaths);

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
