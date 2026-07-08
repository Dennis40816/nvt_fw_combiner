using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
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
        CancellationToken cancellationToken)
    {
        CtrlRamReplaceRunContext context = CreateCtrlRamReplaceRunContext(icId, number, slotPaths);

        if (!context.CanRun)
        {
            return CreateReplaceReportRunResult(
                icId,
                "CtrlRAM",
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
                GetReplaceDefaultOutputFileName(icId, "CtrlRAM"),
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
                "CtrlRAM",
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
                        "replace.ctrlram.postbuild-write-range-missing",
                        "No approved postbuild write range could be derived from the legacy Combiner command plan.",
                        "postbuild"),
                ],
                GetReplaceDefaultOutputFileName(icId, "CtrlRAM"),
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
            postbuildWriteRangeSections);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            return CreateReplaceReportRunResult(
                icId,
                "CtrlRAM",
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
            "ui-replace-ctrlram",
            profile,
            compile.Plan!,
            bindings,
            context.BasePath!,
            build,
            outputPath,
            externalProcessor: ExternalProcessorFactory.CreateOrNull(),
            icNumberSelection: context.Selection,
            overwrite: true,
            cancellationToken).ConfigureAwait(false);
    }
}
