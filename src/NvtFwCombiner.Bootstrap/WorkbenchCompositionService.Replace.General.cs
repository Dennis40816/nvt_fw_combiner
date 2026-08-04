using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunGeneralReplaceDraftCoreAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        GeneralMappingDraftState mappingDraft,
        AuthoringRevision authoringRevision,
        bool build,
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
                        requiredStageId);
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
