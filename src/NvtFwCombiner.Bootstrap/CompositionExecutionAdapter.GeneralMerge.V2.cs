using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionExecutionAdapter
{
    private const string GeneralMergeV2CandidateFallbackProfileId = "general-merge-logical-output-candidate";
    private const string GeneralMergeV2CandidateMemberNotAdmitted = "general-merge.v2-candidate.member-not-admitted";
    /// <summary>Runs a registered logical-output V2 General Merge profile through the shared application core.</summary>
    private static async ValueTask<WorkbenchRunResult> RunGeneralMergeV2Async(
        string icId,
        GeneralMergeDraftState? draft,
        IReadOnlyList<CompositionIssue>? draftIssues,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        CompositionRunProgressFeed? progress = null,
        ResolvedCapability? acceptedCapability = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        GeneralMappingDraftState? mappingDraft = draft?.Mappings;
        Dictionary<string, string> reportSlotPaths = draft is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : CompositionPlanningAdapter.CreateGeneralMergeReportSlotPaths(draft.Mappings);
        string defaultOutputFileName = CanonicalAuthoringAdapter.GetGeneralMergeDefaultOutputFileName(icId);
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
                    CanonicalAuthoringAdapter.IsAcceptedGeneralMappingDraft(mappingDraft)
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

        CompositionPlanningAdapter.GeneralMergePlanningResult planning =
            CompositionPlanningAdapter.PlanGeneralMergeDraft(
            icId,
            registration,
            draft,
            savedRulePolicy,
            acceptedCapability: acceptedCapability);
        admission = planning.Admission;
        if (planning.Plan is not { } plan)
        {
            return Blocked(
                planning.Issues,
                CreateExplicitMappingPlanningOperations(
                    planning.ExplicitMappings,
                    CompositionOperationKind.CopyRange));
        }

        mappingDraft = plan.MappingDraft;
        CompiledComposition composition = plan.Capability.CompiledComposition;

        InputArtifactBinding[] candidateBindings =
        [
            .. plan.MappingBindings.Select(binding => CompiledCompositionInputBindingFactory.Create(
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
            resolvedCapability: plan.Capability).ConfigureAwait(false);
        return result with { AcceptedGeneralMappingDraft = mappingDraft };
    }

}
