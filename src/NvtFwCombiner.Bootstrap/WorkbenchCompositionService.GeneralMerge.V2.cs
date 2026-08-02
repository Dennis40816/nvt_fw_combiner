using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string GeneralMergeV2CandidateFallbackProfileId = "general-merge-logical-output-candidate";
    private const string GeneralMergeV2CandidateMemberNotAdmitted = "general-merge.v2-candidate.member-not-admitted";
    private const string GeneralMergeV2CandidateInputLengthUnsupported = "general-merge.v2-candidate.input-length-unsupported";
    private const string GeneralMergeV2CandidateCompilationUnexpected = "general-merge.v2-candidate.compilation-unexpected";
    private const string GeneralMergeLegacyPlanInvalid = "profile.plan.invalid";
    private const string GeneralMergeV2OperationOverlap = "profile.v2.plan.operation-overlap";
    /// <summary>Runs a registered logical-output V2 General Merge profile through the shared application core.</summary>
    private static async ValueTask<WorkbenchRunResult> RunGeneralMergeV2Async(
        string icId,
        GeneralMergeDraftState? draft,
        IReadOnlyList<CompositionIssue>? draftIssues,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        CompositionRunProgressFeed? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        GeneralMappingDraftState? mappingDraft = draft?.Mappings;
        Dictionary<string, string> reportSlotPaths = draft is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : CreateGeneralMergeReportSlotPaths(draft.Mappings);
        string defaultOutputFileName = GetGeneralMergeDefaultOutputFileName(icId);
        GeneralAuthoringAdmissionResult? admission = null;
        _ = BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
            icId,
            out GeneralMergeV2CandidateRegistration? registration);
        string reportProfileId = registration?.ProfileId ??
            GeneralMergeV2CandidateFallbackProfileId;
        string reportProfileVersion = registration?.ProfileVersion ?? "unregistered";
        WorkbenchRunResult Blocked(
            IReadOnlyList<CompositionIssue> issues,
            IReadOnlyList<OperationRunSummary>? operations = null)
        {
            return CreateBlockedReportRunResult(
                GeneralMergeRunIdPrefix,
                reportProfileId,
                reportProfileVersion,
                icId,
                IcWorkflowIds.GeneralMerge,
                IcWorkflowIds.GeneralMerge,
                CompositionKind.Merge,
                reportSlotPaths,
                build,
                operations ?? [],
                issues,
                defaultOutputFileName,
                imageInitialization: draft is null
                    ? null
                    : ImageInitializationSummary.FromCompiled(
                        draft.OutputInitializer.ToImageInitialization(
                            CompositionAddressSpaceIds.OutputImage)),
                generalAdmission: admission) with
            {
                AcceptedGeneralMappingDraft =
                    IsAcceptedGeneralMappingDraft(mappingDraft)
                        ? mappingDraft
                        : null,
            };
        }

        if (registration is null)
        {
            return Blocked(
                [new CompositionIssue(
                    GeneralMergeV2CandidateMemberNotAdmitted,
                    "The General Merge V2 candidate is currently admitted only for explicitly registered members.",
                    icId)]);
        }

        var capabilityIdentity = new CapabilityRouteIdentity(
            icId,
            IcWorkflowIds.GeneralMerge,
            "not-applicable",
            "generic");
        CapabilityRouteResolutionResult capabilityResolution =
            s_canonicalCapabilityCatalog.ResolveDynamicRoute(
                capabilityIdentity.RouteId);
        if (!capabilityResolution.Succeeded)
        {
            return Blocked(
                [new CompositionIssue(
                    capabilityResolution.Issue!.Code,
                    capabilityResolution.Issue.Message)]);
        }

        if (draftIssues is { Count: > 0 })
        {
            return Blocked(draftIssues);
        }

        if (draft is null)
        {
            return Blocked(
                [new CompositionIssue(
                    WorkbenchIssueCodes.GeneralMergeMappingRequired,
                    "General Merge requires at least one explicit source-to-target mapping.",
                    IcWorkflowIds.GeneralMerge)]);
        }

        GeneralSelectedFileBindingResult acceptedFiles =
            RequireAcceptedGeneralSelectedFiles(mappingDraft!);
        if (!acceptedFiles.Succeeded)
        {
            return Blocked(acceptedFiles.Issues);
        }

        mappingDraft = acceptedFiles.Draft!;
        draft = new GeneralMergeDraftState(
            draft.OutputInitializer,
            mappingDraft);
        admission = AdmitGeneralMappingDraft(
            mappingDraft,
            draft.OutputInitializer.Capacity,
            CreateCurrentGeneralTrustedParentPolicy(
                registration.ProfileId,
                mappingDraft,
                registration.Bundle
                    .GetGeneralMergeSavedRuleAdmissionContext(
                        registration.ProfileId)
                    .ParentBinding),
            savedRulePolicy);
        if (!admission.IsAdmitted)
        {
            return Blocked(admission.ToCompositionIssues());
        }

        if (!TryCreateGeneralMergeMappings(
                admission,
                out IReadOnlyList<ExplicitMapping> explicitMappings,
                out IReadOnlyList<AddressSpace> requestAddressSpaces,
                out IReadOnlyList<InputArtifactBinding> mappingBindings,
                out IReadOnlyList<CompositionIssue> mappingIssues))
        {
            return Blocked(
                mappingIssues,
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.CopyRange));
        }

        if (requestAddressSpaces.Any(static addressSpace => addressSpace.Length > int.MaxValue))
        {
            return Blocked(
                [new CompositionIssue(
                    GeneralMergeV2CandidateInputLengthUnsupported,
                    "The General Merge V2 candidate accepts source inputs up to the supported in-memory composition size.",
                    "source")],
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.CopyRange));
        }

        V2CompositionPlanCompileResult compile = registration.Bundle.CompileLogicalOutput(
            registration.ProfileId,
            registration.ProfileVersion,
            icId,
            new V2LogicalOutputCompileRequest(
                draft.OutputInitializer,
                requestAddressSpaces.Select(static addressSpace => new V2LogicalOutputInputBinding(
                    addressSpace.AddressSpaceId,
                    "source",
                    (int)addressSpace.Length)),
                explicitMappings.Select(static mapping => new ExplicitMapping(
                    mapping.MappingId,
                    mapping.Sequence,
                    mapping.OperationKind,
                    mapping.SourceBindingId,
                    mapping.SourceRange,
                    mapping.TargetSpaceId,
                    mapping.TargetRange,
                    mapping.OverlapPolicy,
                    mapping.Alignment,
                    mapping.Reason,
                    targetRegionId: null,
                    provenance: mapping.Provenance))));
        if (!compile.IsCompiled || compile.CompiledComposition is not { } composition)
        {
            return Blocked(
                NormalizeGeneralMergeV2Issues(compile.Issues),
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.CopyRange));
        }

        if (!IsExpectedGeneralMergeV2Candidate(composition, registration))
        {
            return Blocked(
                [new CompositionIssue(
                    GeneralMergeV2CandidateCompilationUnexpected,
                    "The selected General Merge V2 artifact does not match the candidate admission contract.",
                    registration.ProfileId)],
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.CopyRange));
        }

        ResolvedCapability resolvedCapability =
            capabilityResolution.Route!.BindCompilation(composition);
        composition = resolvedCapability.CompiledComposition;

        InputArtifactBinding[] candidateBindings =
        [
            .. mappingBindings.Select(binding => CompiledCompositionInputBindingFactory.Create(
                composition,
                binding.AddressSpaceId,
                binding.ArtifactId,
                acceptedContentStamp: binding.AcceptedContentStamp)),
        ];
        WorkbenchRunResult result = await RunCompiledCompositionAsync(
            GeneralMergeRunIdPrefix,
            composition,
            candidateBindings,
            candidateBindings[0].ArtifactId,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: null,
            cancellationToken: cancellationToken,
            progress: progress,
            generalAdmission: admission,
            resolvedCapability: resolvedCapability).ConfigureAwait(false);
        return result with { AcceptedGeneralMappingDraft = mappingDraft };
    }

    private static bool IsExpectedGeneralMergeV2Candidate(
        CompiledComposition composition,
        GeneralMergeV2CandidateRegistration registration)
    {
        return composition.Eligibility == CompiledCompositionEligibility.V2PlanCompiled &&
               StringComparer.Ordinal.Equals(composition.ProfileId, registration.ProfileId) &&
               StringComparer.Ordinal.Equals(composition.ProfileVersion, registration.ProfileVersion) &&
               composition.V2Details.Provenance.Context is LogicalOutputV2CompilationContext context &&
               composition.V2Details.Provenance.Promotion.Stage == CompiledProfilePromotionStage.ExecutableCandidate &&
               StringComparer.Ordinal.Equals(context.FamilyId, registration.FamilyId) &&
               StringComparer.Ordinal.Equals(context.MemberId, registration.IcId);
    }

    private static CompositionIssue[] NormalizeGeneralMergeV2Issues(
        IReadOnlyList<CompositionIssue> issues)
    {
        return
        [
            .. issues.Select(static issue =>
                StringComparer.Ordinal.Equals(issue.Code, GeneralMergeV2OperationOverlap)
                    ? new CompositionIssue(GeneralMergeLegacyPlanInvalid, issue.Message, issue.OperationId)
                    : issue),
        ];
    }
}
