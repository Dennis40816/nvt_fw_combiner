using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunGeneralReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs,
        bool build,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        if (!TryCreateGeneralReplaceRunContext(
                icId,
                number,
                slotPaths,
                mappingInputs,
                build,
                out GeneralReplaceRunContext? context,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        if (!TryCreateGeneralReplaceMappings(
                context!.SelectedMappings,
                out IReadOnlyList<ExplicitMapping> explicitMappings,
                out IReadOnlyList<AddressSpace> requestAddressSpaces,
                out IReadOnlyList<InputArtifactBinding> mappingBindings,
                out IReadOnlyList<CompositionIssue> mappingIssues))
        {
            return CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.General,
                context.ReportSlotPaths,
                build,
                [],
                mappingIssues,
                GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                succeeded: false);
        }

        bool postbuildProfileResolved = TryGetPostbuildProfile(
            icId,
            context.BasePath,
            out LegacyCombinerPostbuildProfile? postbuildProfile,
            out CompositionIssue? postbuildIssue);
        IReadOnlyList<TpFlashMapRegion> regionsForMappingPolicy = TpFlashMapCatalog.GetRegions(
            icId,
            context.Selection,
            postbuildProfileResolved ? postbuildProfile : null);
        bool touchesTpRegion = GeneralReplaceTouchesTpRegion(regionsForMappingPolicy, explicitMappings);
        LegacyCombinerPostbuildCommandPlan? commandPlan = null;
        List<LegacyCombinerPostbuildWriteRange> postbuildWriteRangeSections = [];
        if (touchesTpRegion)
        {
            if (!postbuildProfileResolved)
            {
                return CreateReplaceReportRunResult(
                    icId,
                    WorkbenchReplaceModes.General,
                    context.ReportSlotPaths,
                    build,
                    CreateGeneralReplacePlanningOperations(explicitMappings),
                    [postbuildIssue!],
                    GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                    succeeded: false);
            }

            try
            {
                commandPlan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile!, context.Selection);
            }
            catch (ArgumentException exception)
            {
                return CreateReplaceReportRunResult(
                    icId,
                    WorkbenchReplaceModes.General,
                    context.ReportSlotPaths,
                    build,
                    CreateGeneralReplacePlanningOperations(explicitMappings),
                    [
                        new CompositionIssue(
                            "replace.general.ic-number-unsupported",
                            exception.Message,
                            "number"),
                    ],
                    GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                    succeeded: false);
            }

            long requiredCapacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(commandPlan, []);
            if (context.Capacity < requiredCapacity)
            {
                return CreateReplaceReportRunResult(
                    icId,
                    WorkbenchReplaceModes.General,
                    context.ReportSlotPaths,
                    build,
                    CreateGeneralReplacePlanningOperations(explicitMappings),
                    [
                        new CompositionIssue(
                            CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                            $"Base flash BIN is too short for {icId} / {number} General Replace postbuild (actual {context.Capacity} bytes, required at least {requiredCapacity} bytes).",
                            WorkbenchSlotIds.ReplaceBase),
                    ],
                    GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                    succeeded: false);
            }

            postbuildWriteRangeSections =
            [
                .. LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForInPlaceRefresh(commandPlan, context.Capacity),
            ];
            if (postbuildWriteRangeSections.Count == 0)
            {
                return CreateReplaceReportRunResult(
                    icId,
                    WorkbenchReplaceModes.General,
                    context.ReportSlotPaths,
                    build,
                    CreateGeneralReplacePlanningOperations(explicitMappings),
                    [
                        new CompositionIssue(
                            "replace.general.postbuild-write-range-missing",
                            "No approved postbuild write range could be derived for TP-touching General Replace.",
                            "postbuild"),
                    ],
                    GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                    succeeded: false);
            }
        }

        CompositionProfileDefinition profile = CreateGeneralReplaceProfile(
            icId,
            context.Selection,
            context.Capacity,
            postbuildProfileResolved ? postbuildProfile : null,
            commandPlan,
            postbuildWriteRangeSections);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(
            profile,
            explicitMappings,
            requestAddressSpaces);
        if (!compile.IsSuccess)
        {
            return CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.General,
                context.ReportSlotPaths,
                build,
                CreateGeneralReplacePlanningOperations(explicitMappings),
                compile.Issues,
                profile.DefaultOutputFileName,
                succeeded: false);
        }

        InputArtifactBinding[] bindings =
        [
            new(CompositionAddressSpaceIds.ReferenceBase, WorkbenchSlotIds.ReplaceBase, context.BasePath),
            .. mappingBindings,
        ];

        return await RunCompiledCompositionAsync(
            "ui-replace-general",
            profile,
            compile.Plan!,
            bindings,
            context.BasePath,
            build,
            outputPath,
            externalProcessor: commandPlan is null ? null : ExternalProcessorFactory.CreateOrNull(),
            icNumberSelection: context.Selection,
            overwrite: true,
            cancellationToken).ConfigureAwait(false);
    }

}
