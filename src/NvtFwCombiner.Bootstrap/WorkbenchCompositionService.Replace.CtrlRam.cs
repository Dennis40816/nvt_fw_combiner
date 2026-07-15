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

        if (!context.CanRun)
        {
            return CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.CtrlRam,
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(
                    icId,
                    context.Selection,
                    context.Regions,
                    slotPaths,
                    runnablePreview: false,
                    context.PostbuildProfile),
                context.ValidationIssues,
                GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.CtrlRam),
                succeeded: false);
        }

        IReadOnlyList<LegacyCombinerPostbuildWriteRange> postbuildWriteRangeSections =
            LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForStagedSources(
            context.CommandPlan!,
            context.BaseLength,
            context.Regions.Select(region => region.Range),
            context.Regions.Select(region => region.Range));
        if (postbuildWriteRangeSections.Count == 0)
        {
            return CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.CtrlRam,
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(
                    icId,
                    context.Selection,
                    context.Regions,
                    slotPaths,
                    runnablePreview: false,
                    context.PostbuildProfile),
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.ReplaceCtrlRamPostbuildWriteRangeMissing,
                        "No approved postbuild write range could be derived from the legacy Combiner command plan.",
                        "postbuild"),
                ],
                GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.CtrlRam),
                succeeded: false);
        }

        CompositionProfileDefinition profile = CreateCtrlRamReplaceProfile(
            icId,
            context.Selection,
            context.BaseLength,
            context.Regions,
            context.SelectedRegions,
            context.PostbuildProfile!,
            context.CommandPlan!,
            postbuildWriteRangeSections,
            context.FirmwareVersionWritePlan);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            return CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.CtrlRam,
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(
                    icId,
                    context.Selection,
                    context.Regions,
                    slotPaths,
                    runnablePreview: false,
                    context.PostbuildProfile),
                compile.Issues,
                profile.DefaultOutputFileName,
                succeeded: false);
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
