using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
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
        GeneralSavedRuleResourcePolicy? savedRulePolicy = null,
        GeneralReplacePostbuildReadinessOverride? postbuildReadinessOverride = null,
        SavedRuleV2GeneralReplaceExactParent? exactParentOverride = null)
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
            string? outputFileName = null,
            GeneralReplaceDiagnosticPreviewSummary? diagnosticPreview = null,
            CapabilityActionReadinessSnapshot? readiness = null)
        {
            WorkbenchRunResult result = CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.General,
                context!.ReportSlotPaths,
                build,
                operations ?? [],
                issues,
                outputFileName ?? GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                generalAdmission: admission,
                diagnosticPreview: diagnosticPreview);
            return result with
            {
                AcceptedGeneralMappingDraft =
                    IsAcceptedGeneralMappingDraft(context!.MappingDraft)
                        ? context.MappingDraft
                        : null,
                ActionReadiness = readiness,
            };
        }

        GeneralSelectedFileBindingResult acceptedFiles =
            RequireAcceptedGeneralSelectedFiles(context!.MappingDraft);
        if (!acceptedFiles.Succeeded)
        {
            return Blocked(acceptedFiles.Issues);
        }

        context = context with { MappingDraft = acceptedFiles.Draft! };
        SavedRuleV2GeneralReplaceExactParent exactParent =
            exactParentOverride ?? GetNt51926GeneralReplaceExactParent();
        admission = AdmitGeneralMappingDraft(
            context.MappingDraft,
            context.Capacity,
            CreateCurrentGeneralTrustedParentPolicy(
                exactParent.Admission.ParentBinding.ProfileId,
                context.MappingDraft,
                exactParent.Admission.ParentBinding),
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
        IReadOnlyList<OperationRunSummary> planningOperations =
            CreateExplicitMappingPlanningOperations(
                explicitMappings,
                CompositionOperationKind.ReplaceRange);
        LegacyCombinerPostbuildCommandPlan? commandPlan = null;
        List<ExternalProcessorWriteRangeSection> postbuildWriteRangeSections = [];
        if (touchesTpRegion)
        {
            if (!postbuildProfileResolved)
            {
                return Blocked(
                    [postbuildIssue!],
                    planningOperations);
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
                    planningOperations);
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
                    planningOperations);
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
                    planningOperations);
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
                planningOperations);
        }

        IReadOnlyList<Domain.Firmware.FirmwareImageMap> capabilityMaps =
            GetNt51926GeneralReplaceSupportMaps(
                out _,
                out IReadOnlyList<CompositionIssue> capabilityMapIssues);
        Domain.Firmware.FirmwareImageMap? capabilityMap =
            capabilityMapIssues.Count == 0
                ? capabilityMaps.SingleOrDefault(map =>
                    map.CapacityBytes == context.Capacity)
                : null;
        if (capabilityMap is null)
        {
            return Blocked(
                capabilityMapIssues.Count == 0
                    ? [new CompositionIssue(
                        CapabilityCatalogIssueCodes.RouteUnavailable,
                        "The selected General Replace capacity has no canonical map route.")]
                    : capabilityMapIssues,
                planningOperations);
        }

        var capabilityIdentity = new CapabilityRouteIdentity(
            icId,
            Profiles.IcWorkflowIds.GeneralReplace,
            "1-ic",
            capabilityMap.MapId);
        CapabilityRouteResolutionResult capabilityResolution =
            s_canonicalCapabilityCatalog.ResolveDynamicRoute(
                capabilityIdentity.RouteId);
        if (!capabilityResolution.Succeeded)
        {
            return Blocked(
                [new CompositionIssue(
                    capabilityResolution.Issue!.Code,
                    capabilityResolution.Issue.Message)],
                planningOperations);
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
                planningOperations,
                "nt51926-general-replace.bin");
        }
        ResolvedCapability resolvedCapability =
            capabilityResolution.Route!.BindCompilation(
                compiledComposition,
                BuiltInV2BundleRegistry.All[Nt51926GeneralReplaceBundleId]
                    .CreateMetadataPlan(
                        Nt51926GeneralReplaceDpProfileId,
                        Nt51926GeneralReplaceDpProfileVersion,
                        compiledComposition));
        compiledComposition = resolvedCapability.CompiledComposition;

        if (touchesTpRegion &&
            StringComparer.Ordinal.Equals(
                icId,
                Nt51926GeneralReplaceIcId))
        {
            GeneralReplacePostbuildReadinessResult postbuild =
                await ResolveGeneralReplacePostbuildReadinessAsync(
                    admission,
                    exactParent.Runtime,
                    resolvedCapability,
                    postbuildReadinessOverride,
                    cancellationToken).ConfigureAwait(false);
            CapabilityActionReadinessSnapshot readiness = postbuild.Readiness;
            if (!readiness.Build.IsAvailable)
            {
                CapabilityActionBlocker blocker =
                    readiness.Build.PrimaryBlocker!;
                CompositionIssue issue = new(
                    blocker.Code,
                    blocker.Message,
                    blocker.SubjectId);
                GeneralReplaceDiagnosticPreviewSummary? diagnostic = build
                    ? null
                    : GeneralReplaceDiagnosticPreviewProjector.Project(
                        context.Capacity,
                        admission,
                        readiness,
                        postbuild.RequiredStageId);
                return build
                    ? CreateReplaceReadinessOnlyResult(
                        icId,
                        WorkbenchReplaceModes.General,
                        readiness) with
                    {
                        AcceptedGeneralMappingDraft = context.MappingDraft,
                    }
                    : Blocked(
                        [issue],
                        planningOperations,
                        diagnosticPreview: diagnostic,
                        readiness: readiness);
            }
        }

        if (!TryMaterializeGeneralReplacePatchArtifacts(
                patchArtifacts,
                out IReadOnlyDictionary<string, byte[]> patchVirtualArtifacts,
                out IReadOnlyList<CompositionIssue> materializationIssues))
        {
            return Blocked(
                materializationIssues,
                planningOperations,
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
            generalAdmission: admission,
            resolvedCapability: resolvedCapability).ConfigureAwait(false);
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
