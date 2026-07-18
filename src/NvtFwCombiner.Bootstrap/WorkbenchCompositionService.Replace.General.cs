using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;
using V2CompositionPlanCompileResult = NvtFwCombiner.Profiles.V2.V2CompositionPlanCompileResult;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunGeneralReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> patchInputs,
        bool build,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        if (!TryCreateGeneralReplaceRunContext(
                icId,
                number,
                slotPaths,
                mappingInputs,
                patchInputs,
                build,
                out GeneralReplaceRunContext? context,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        WorkbenchRunResult Blocked(
            IReadOnlyList<CompositionIssue> issues,
            IReadOnlyList<OperationRunSummary>? operations = null,
            string? outputFileName = null)
        {
            return CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.General,
                context!.ReportSlotPaths,
                build,
                operations ?? [],
                issues,
                outputFileName ?? GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General));
        }

        if (!TryCreateGeneralReplaceMappings(
                context!.SelectedMappings,
                context.SelectedPatches,
                context.Capacity,
                out IReadOnlyList<ExplicitMapping> explicitMappings,
                out IReadOnlyList<AddressSpace> requestAddressSpaces,
                out IReadOnlyList<InputArtifactBinding> mappingBindings,
                out IReadOnlyList<GeneralReplacePatchArtifact> patchArtifacts,
                out IReadOnlyList<CompositionIssue> mappingIssues))
        {
            return Blocked(mappingIssues);
        }

        bool postbuildProfileResolved = TryGetPostbuildProfile(
            icId,
            context.BasePath,
            out LegacyCombinerPostbuildProfile? postbuildProfile,
            out CompositionIssue? postbuildIssue);
        IReadOnlyList<TpFlashMapRegion> regionsForMappingPolicy = BuiltInTpFlashMapCatalog.GetRegions(
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
                return Blocked(
                    [postbuildIssue!],
                    CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.ReplaceRange));
            }

            try
            {
                commandPlan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile!, context.Selection);
            }
            catch (ArgumentException exception)
            {
                return Blocked(
                    [
                        new CompositionIssue(
                            WorkbenchIssueCodes.ReplaceGeneralIcNumberUnsupported,
                            exception.Message,
                            "number"),
                    ],
                    CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.ReplaceRange));
            }

            long requiredCapacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(commandPlan, []);
            if (context.Capacity < requiredCapacity)
            {
                return Blocked(
                    [
                        new CompositionIssue(
                            CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                            $"Base flash BIN is too short for {icId} / {number} General Replace postbuild (actual {context.Capacity} bytes, required at least {requiredCapacity} bytes).",
                            WorkbenchSlotIds.ReplaceBase),
                    ],
                    CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.ReplaceRange));
            }

            postbuildWriteRangeSections =
            [
                .. LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForInPlaceRefresh(commandPlan, context.Capacity),
            ];
            if (postbuildWriteRangeSections.Count == 0)
            {
                return Blocked(
                    [
                        new CompositionIssue(
                            WorkbenchIssueCodes.ReplaceGeneralPostbuildWriteRangeMissing,
                            "No approved postbuild write range could be derived for TP-touching General Replace.",
                            "postbuild"),
                    ],
                    CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.ReplaceRange));
            }
        }

        CompiledComposition? compiledComposition;
        IReadOnlyList<CompositionIssue> compilationIssues;
        string defaultOutputFileName;
        bool useNt51926DpV2 = IsNt51926GeneralReplaceDpV2Route(
            icId,
            context,
            regionsForMappingPolicy,
            explicitMappings);
        if (useNt51926DpV2)
        {
            V2CompositionPlanCompileResult compile = CompileNt51926GeneralReplaceDpV2(
                context,
                requestAddressSpaces,
                explicitMappings);
            compiledComposition = compile.CompiledComposition;
            compilationIssues = compile.Issues;
            defaultOutputFileName = "nt51926-general-replace.bin";
        }
        else
        {
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
            compiledComposition = compile.CompiledComposition;
            compilationIssues = compile.Issues;
            defaultOutputFileName = profile.DefaultOutputFileName;
        }

        if (compiledComposition is null)
        {
            return Blocked(
                compilationIssues,
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.ReplaceRange),
                defaultOutputFileName);
        }

        if (!TryMaterializeGeneralReplacePatchArtifacts(
                patchArtifacts,
                out IReadOnlyDictionary<string, byte[]> patchVirtualArtifacts,
                out IReadOnlyList<CompositionIssue> materializationIssues))
        {
            return Blocked(
                materializationIssues,
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.ReplaceRange),
                defaultOutputFileName);
        }

        InputArtifactBinding[] bindings = useNt51926DpV2
            ?
            [
                CompiledCompositionInputBindingFactory.Create(
                    compiledComposition,
                    Nt51926GeneralReplaceReferenceSpaceId,
                    context.BasePath),
                .. mappingBindings.Select(binding => CompiledCompositionInputBindingFactory.Create(
                    compiledComposition,
                    binding.AddressSpaceId,
                    binding.ArtifactId)),
            ]
            :
            [
                new(CompositionAddressSpaceIds.ReferenceBase, WorkbenchSlotIds.ReplaceBase, context.BasePath),
                .. mappingBindings,
            ];

        return await RunCompiledCompositionAsync(
            GeneralReplaceRunIdPrefix,
            compiledComposition,
            bindings,
            context.BasePath,
            build,
            outputPath,
            externalProcessor: commandPlan is null ? null : ExternalProcessorFactory.CreateOrNull(),
            icNumberSelection: context.Selection,
            overwrite: true,
            cancellationToken,
            patchVirtualArtifacts).ConfigureAwait(false);
    }

}
