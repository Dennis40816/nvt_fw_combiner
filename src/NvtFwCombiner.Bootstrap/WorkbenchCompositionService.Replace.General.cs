using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static readonly GeneralReplaceRunActionStrategy PreviewGeneralReplaceStrategy = new(
        RenderGeneralReplacePreviewFailure,
        RenderGeneralReplacePreviewPostbuildUnavailable,
        CompleteGeneralReplacePreviewAsync);

    private static readonly GeneralReplaceRunActionStrategy BuildGeneralReplaceStrategy = new(
        RenderGeneralReplaceBuildFailure,
        RenderGeneralReplaceBuildPostbuildUnavailable,
        CompleteGeneralReplaceBuildAsync);

    private static async ValueTask<WorkbenchRunResult> RunGeneralReplaceDraftCoreAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        AuthoringRevision authoringRevision,
        GeneralReplaceRunActionStrategy strategy,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken,
        GeneralSavedRuleResourcePolicy? savedRulePolicy = null,
        GeneralReplacePostbuildReadinessOverride? postbuildReadinessOverride = null,
        SavedRuleV2GeneralReplaceExactParent? exactParentOverride = null,
        ResolvedCapability? acceptedCapability = null)
    {
        if (!TryCreateGeneralReplaceRunContext(
                icId,
                number,
                slotPaths,
                mappingDraft,
                out GeneralReplaceRunContext? context,
                out IReadOnlyDictionary<string, string> reportSlotPaths,
                out CompositionIssue? contextIssue))
        {
            return strategy.RenderFailure(new GeneralReplaceRunFailure(
                icId,
                reportSlotPaths,
                AcceptedDraft: null,
                [],
                [contextIssue!],
                GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                Admission: null));
        }

        GeneralAuthoringAdmissionResult? admission = null;
        WorkbenchRunResult Blocked(
            IReadOnlyList<CompositionIssue> issues,
            IReadOnlyList<OperationRunSummary>? operations = null,
            string? outputFileName = null,
            GeneralReplaceDiagnosticPreviewSummary? diagnosticPreview = null,
            CapabilityActionReadinessSnapshot? readiness = null)
        {
            return strategy.RenderFailure(new GeneralReplaceRunFailure(
                icId,
                context!.ReportSlotPaths,
                IsAcceptedGeneralMappingDraft(context.MappingDraft)
                    ? context.MappingDraft
                    : null,
                operations ?? [],
                issues,
                outputFileName ?? GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.General),
                admission,
                diagnosticPreview,
                readiness));
        }

        GeneralSelectedFileBindingResult acceptedFiles =
            RequireAcceptedGeneralSelectedFiles(context!.MappingDraft);
        if (!acceptedFiles.Succeeded)
        {
            return Blocked(acceptedFiles.Issues);
        }

        context = context with { MappingDraft = acceptedFiles.Draft! };
        string normalizedIcId = Profiles.IcSupportCatalog.NormalizeIcId(icId);
        _ = BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
            normalizedIcId,
            out GeneralReplaceV2Registration? registration);
        // Preserve route-independent authoring diagnostics only while one exact
        // diagnostic parent exists. Never choose among multiple registered maps.
        GeneralReplaceV2Registration? validationRegistration =
            ResolveGeneralReplaceValidationRegistration(normalizedIcId);
        if (exactParentOverride is null && validationRegistration is null)
        {
            admission = AdmitGeneralMappingDraft(
                context.MappingDraft,
                context.Capacity,
                CreateCurrentGeneralTrustedParentPolicy(
                    "general-replace-diagnostic",
                    context.MappingDraft),
                savedRulePolicy);
            return admission.IsAdmitted
                ? Blocked([new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                    "The selected General Replace shape has no unique validation authority.",
                    "mapping")])
                : Blocked(admission.ToCompositionIssues());
        }

        SavedRuleV2GeneralReplaceExactParent exactParent =
            exactParentOverride ?? validationRegistration!.ExactParent;
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
        IReadOnlyList<OperationRunSummary> planningOperations =
            CreateExplicitMappingPlanningOperations(
                explicitMappings,
                CompositionOperationKind.ReplaceRange);
        LegacyCombinerPostbuildCommandPlan? commandPlan = TryPlanGeneralReplacePostbuild(
            icId,
            context.Selection,
            context.Capacity,
            regionsForMappingPolicy,
            explicitMappings,
            postbuildProfileResolved,
            postbuildProfile,
            postbuildIssue,
            out bool touchesTpRegion,
            out CompositionIssue? postbuildPlanningIssue);
        if (postbuildPlanningIssue is not null)
        {
            return Blocked([postbuildPlanningIssue], planningOperations);
        }

        ResolvedCapability? resolvedCapability = acceptedCapability;
        if (resolvedCapability is null && !TryResolveGeneralReplaceCompiledRoute(
            registration,
            context.Capacity,
            context.Selection,
            context.MappingDraft,
            regionsForMappingPolicy,
            explicitMappings,
            requestAddressSpaces,
            out resolvedCapability,
            out IReadOnlyList<CompositionIssue> routeIssues))
        {
            return Blocked(
                routeIssues,
                planningOperations,
                registration?.DefaultOutputFileName);
        }
        CompiledComposition compiledComposition = resolvedCapability!.CompiledComposition;

        if (touchesTpRegion)
        {
            (CapabilityActionReadinessSnapshot readiness, string? requiredStageId) =
                await ResolveGeneralReplacePostbuildReadinessAsync(
                    admission,
                    exactParent.Runtime,
                    resolvedCapability,
                    authoringRevision,
                    requiresPostbuild: true,
                    planningIssue: null,
                    postbuildReadinessOverride,
                    cancellationToken).ConfigureAwait(false);
            if (!readiness.Build.IsAvailable)
            {
                return strategy.RenderPostbuildUnavailable(
                    new GeneralReplacePostbuildUnavailable(
                        icId,
                        context,
                        admission,
                        readiness,
                        requiredStageId,
                        planningOperations));
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
                registration!.DefaultOutputFileName);
        }

        InputArtifactBinding[] bindings =
        [
            CompiledCompositionInputBindingFactory.Create(
                compiledComposition,
                GeneralReplaceV2Registration.ReferenceAddressSpaceId,
                context.BasePath),
            .. mappingBindings.Select(binding => CompiledCompositionInputBindingFactory.Create(
                compiledComposition,
                binding.AddressSpaceId,
                binding.ArtifactId,
                acceptedContentStamp: binding.AcceptedContentStamp)),
        ];

        return await strategy.CompleteAsync(
            new GeneralReplacePreparedRun(
                context,
                compiledComposition,
                bindings,
                patchVirtualArtifacts,
                commandPlan is null ? null : ExternalProcessorFactory.GetOrCreateOrNull(),
                admission,
                resolvedCapability),
            outputPath,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private static WorkbenchRunResult RenderGeneralReplacePreviewFailure(
        GeneralReplaceRunFailure failure)
    {
        return RenderGeneralReplaceFailure(failure, "preview");
    }

    private static WorkbenchRunResult RenderGeneralReplaceBuildFailure(
        GeneralReplaceRunFailure failure)
    {
        return RenderGeneralReplaceFailure(failure, "build");
    }

    private static WorkbenchRunResult RenderGeneralReplaceFailure(
        GeneralReplaceRunFailure failure,
        string runAction)
    {
        WorkbenchRunResult result = CreateReplaceReportRunResult(
            failure.IcId,
            WorkbenchReplaceModes.General,
            failure.SlotPaths,
            runAction,
            failure.Operations,
            failure.Issues,
            failure.OutputFileName,
            failure.Admission,
            failure.DiagnosticPreview);
        return result with
        {
            AcceptedGeneralMappingDraft = failure.AcceptedDraft,
            ActionReadiness = failure.Readiness,
        };
    }

    private static WorkbenchRunResult RenderGeneralReplacePreviewPostbuildUnavailable(
        GeneralReplacePostbuildUnavailable unavailable)
    {
        CapabilityActionAvailability action = unavailable.Readiness.Preview.IsAvailable
            ? unavailable.Readiness.Build
            : unavailable.Readiness.Preview;
        CapabilityActionBlocker blocker = action.PrimaryBlocker!;
        GeneralReplaceDiagnosticPreviewSummary? diagnostic =
            unavailable.Readiness.Preview.IsAvailable
                ? GeneralReplaceDiagnosticPreviewProjector.Project(
                    unavailable.Context.Capacity,
                    unavailable.Admission,
                    unavailable.Readiness,
                    unavailable.RequiredStageId)
                : null;
        return RenderGeneralReplacePreviewFailure(new GeneralReplaceRunFailure(
            unavailable.IcId,
            unavailable.Context.ReportSlotPaths,
            unavailable.Context.MappingDraft,
            unavailable.PlanningOperations,
            [new CompositionIssue(blocker.Code, blocker.Message, blocker.SubjectId)],
            GetReplaceDefaultOutputFileName(
                unavailable.IcId,
                WorkbenchReplaceModes.General),
            unavailable.Admission,
            diagnostic,
            unavailable.Readiness));
    }

    private static WorkbenchRunResult RenderGeneralReplaceBuildPostbuildUnavailable(
        GeneralReplacePostbuildUnavailable unavailable)
    {
        return CreateReplaceReadinessOnlyResult(
            unavailable.IcId,
            WorkbenchReplaceModes.General,
            unavailable.Readiness) with
        {
            AcceptedGeneralMappingDraft = unavailable.Context.MappingDraft,
        };
    }

    private static async ValueTask<WorkbenchRunResult> CompleteGeneralReplacePreviewAsync(
        GeneralReplacePreparedRun prepared,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        WorkbenchRunResult result = await RunCompiledCompositionAsync(
            GeneralReplaceRunIdPrefix,
            prepared.CompiledComposition,
            prepared.Bindings,
            prepared.Context.BasePath,
            build: false,
            outputPath,
            prepared.ExternalProcessor,
            prepared.Context.Selection,
            cancellationToken,
            prepared.VirtualArtifacts,
            progress,
            generalAdmission: prepared.Admission,
            resolvedCapability: prepared.ResolvedCapability).ConfigureAwait(false);
        return result with { AcceptedGeneralMappingDraft = prepared.Context.MappingDraft };
    }

    private static async ValueTask<WorkbenchRunResult> CompleteGeneralReplaceBuildAsync(
        GeneralReplacePreparedRun prepared,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        WorkbenchRunResult result = await RunCompiledCompositionAsync(
            GeneralReplaceRunIdPrefix,
            prepared.CompiledComposition,
            prepared.Bindings,
            prepared.Context.BasePath,
            build: true,
            outputPath,
            prepared.ExternalProcessor,
            prepared.Context.Selection,
            cancellationToken,
            prepared.VirtualArtifacts,
            progress,
            generalAdmission: prepared.Admission,
            resolvedCapability: prepared.ResolvedCapability).ConfigureAwait(false);
        return result with { AcceptedGeneralMappingDraft = prepared.Context.MappingDraft };
    }

    private sealed record GeneralReplaceRunActionStrategy(
        Func<GeneralReplaceRunFailure, WorkbenchRunResult> RenderFailure,
        Func<GeneralReplacePostbuildUnavailable, WorkbenchRunResult> RenderPostbuildUnavailable,
        Func<
            GeneralReplacePreparedRun,
            string?,
            CompositionRunProgressFeed?,
            CancellationToken,
            ValueTask<WorkbenchRunResult>> CompleteAsync);

    private sealed record GeneralReplaceRunFailure(
        string IcId,
        IReadOnlyDictionary<string, string> SlotPaths,
        GeneralMappingDraftState? AcceptedDraft,
        IReadOnlyList<OperationRunSummary> Operations,
        IReadOnlyList<CompositionIssue> Issues,
        string OutputFileName,
        GeneralAuthoringAdmissionResult? Admission,
        GeneralReplaceDiagnosticPreviewSummary? DiagnosticPreview = null,
        CapabilityActionReadinessSnapshot? Readiness = null);

    private sealed record GeneralReplacePostbuildUnavailable(
        string IcId,
        GeneralReplaceRunContext Context,
        GeneralAuthoringAdmissionResult Admission,
        CapabilityActionReadinessSnapshot Readiness,
        string? RequiredStageId,
        IReadOnlyList<OperationRunSummary> PlanningOperations);

    private sealed record GeneralReplacePreparedRun(
        GeneralReplaceRunContext Context,
        CompiledComposition CompiledComposition,
        IReadOnlyList<InputArtifactBinding> Bindings,
        IReadOnlyDictionary<string, byte[]> VirtualArtifacts,
        IExternalProcessor? ExternalProcessor,
        GeneralAuthoringAdmissionResult Admission,
        ResolvedCapability ResolvedCapability);

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
