using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using V2CompositionPlanCompileResult = NvtFwCombiner.Profiles.V2.V2CompositionPlanCompileResult;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static ValueTask<WorkbenchRunResult> RunGeneralReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> patchInputs,
        bool build,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        return !TryCreateLegacyGeneralReplaceDraft(
            mappingInputs,
            patchInputs,
            out GeneralMappingDraftState? mappingDraft,
            out IReadOnlyList<CompositionIssue> draftIssues)
                ? ValueTask.FromResult(CreateReplaceReportRunResult(
                    icId,
                    WorkbenchReplaceModes.General,
                    new Dictionary<string, string>(slotPaths, StringComparer.Ordinal),
                    build,
                    [],
                    draftIssues,
                    GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General)))
                : RunGeneralReplaceWithInitialInspectionAsync(
                    icId,
                     number,
                     slotPaths,
                     mappingDraft,
                     new AuthoringRevision(1),
                     build,
                    outputPath,
                    progress,
                    cancellationToken);
    }

    private static async ValueTask<WorkbenchRunResult> RunGeneralReplaceDraftCoreAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        bool build,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken,
        GeneralSavedRuleResourcePolicy? savedRulePolicy = null)
    {
        if (!TryCreateGeneralReplaceRunContext(
                icId,
                number,
                slotPaths,
                mappingDraft,
                build,
                out GeneralReplaceRunContext? context,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        GeneralAuthoringAdmissionResult? admission = null;
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
                outputFileName ?? GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                generalAdmission: admission) with
            {
                AcceptedGeneralMappingDraft =
                    IsAcceptedGeneralMappingDraft(context!.MappingDraft)
                        ? context.MappingDraft
                        : null,
            };
        }

        GeneralSelectedFileBindingResult acceptedFiles =
            RequireAcceptedGeneralSelectedFiles(context!.MappingDraft);
        if (!acceptedFiles.Succeeded)
        {
            return Blocked(acceptedFiles.Issues);
        }

        context = context with { MappingDraft = acceptedFiles.Draft! };
        admission = AdmitGeneralMappingDraft(
            context.MappingDraft,
            context.Capacity,
            CreateCurrentGeneralTrustedParentPolicy(
                Nt51926GeneralReplaceDpProfileId,
                context.MappingDraft),
            savedRulePolicy);
        if (!admission.IsAdmitted)
        {
            return Blocked(admission.ToCompositionIssues());
        }

        if (!TryCreateGeneralReplaceMappings(
                admission,
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

        bool useNt51926DpV2 = IsNt51926GeneralReplaceDpV2Route(
            icId,
            context,
            regionsForMappingPolicy,
            explicitMappings);
        if (!useNt51926DpV2)
        {
            return Blocked(
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                        "The selected General Replace shape has no exact evidence-backed V2 route.",
                        "mapping"),
                ],
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.ReplaceRange));
        }

        V2CompositionPlanCompileResult compile = CompileNt51926GeneralReplaceDpV2(
            context,
            requestAddressSpaces,
            explicitMappings);
        CompiledComposition? compiledComposition = compile.CompiledComposition;
        if (compiledComposition is null)
        {
            return Blocked(
                compile.Issues,
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.ReplaceRange),
                "nt51926-general-replace.bin");
        }

        if (!TryMaterializeGeneralReplacePatchArtifacts(
                patchArtifacts,
                out IReadOnlyDictionary<string, byte[]> patchVirtualArtifacts,
                out IReadOnlyList<CompositionIssue> materializationIssues))
        {
            return Blocked(
                materializationIssues,
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.ReplaceRange),
                "nt51926-general-replace.bin");
        }

        InputArtifactBinding[] bindings =
        [
            CompiledCompositionInputBindingFactory.Create(
                compiledComposition,
                Nt51926GeneralReplaceReferenceSpaceId,
                context.BasePath),
            .. mappingBindings.Select(binding => CompiledCompositionInputBindingFactory.Create(
                compiledComposition,
                binding.AddressSpaceId,
                binding.ArtifactId,
                acceptedContentStamp: binding.AcceptedContentStamp)),
        ];

        WorkbenchRunResult result = await RunCompiledCompositionAsync(
            GeneralReplaceRunIdPrefix,
            compiledComposition,
            bindings,
            context.BasePath,
            build,
            outputPath,
            externalProcessor: commandPlan is null ? null : ExternalProcessorFactory.GetOrCreateOrNull(),
            icNumberSelection: context.Selection,
            cancellationToken,
            patchVirtualArtifacts,
            progress,
            generalAdmission: admission).ConfigureAwait(false);
        return result with
        {
            AcceptedGeneralMappingDraft = context.MappingDraft,
        };
    }

    private static bool GeneralReplaceTouchesTpRegion(
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> explicitMappings)
    {
        return explicitMappings.Any(mapping => regions.Any(region =>
            (region.Kind == TpFlashMapRegionKind.CtrlRam ||
                region.Tags.Any(tag =>
                    string.Equals(tag, "tp", StringComparison.OrdinalIgnoreCase) ||
                    tag.StartsWith("tp-", StringComparison.OrdinalIgnoreCase))) &&
            region.Range.Overlaps(mapping.TargetRange)));
    }

}
