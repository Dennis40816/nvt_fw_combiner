using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    internal const string GeneralMergeV2CandidateProfileVersion = "0.1.0";
    private const string GeneralMergeV2CandidateFallbackProfileId = "general-merge-logical-output-candidate";
    private const string GeneralMergeV2CandidateMemberNotAdmitted = "general-merge.v2-candidate.member-not-admitted";
    private const string GeneralMergeV2CandidateInputLengthUnsupported = "general-merge.v2-candidate.input-length-unsupported";
    private const string GeneralMergeV2CandidateCompilationUnexpected = "general-merge.v2-candidate.compilation-unexpected";
    private const string GeneralMergeLegacyPlanInvalid = "profile.plan.invalid";
    private const string GeneralMergeV2OperationOverlap = "profile.v2.plan.operation-overlap";
    /// <summary>Runs a registered logical-output V2 General Merge profile through the shared application core.</summary>
    private static async ValueTask<WorkbenchRunResult> RunGeneralMergeV2Async(
        string icId,
        string outputLength,
        GeneralMappingDraftState? mappingDraft,
        IReadOnlyList<CompositionIssue>? draftIssues,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        CompositionRunProgressFeed? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        Dictionary<string, string> reportSlotPaths = mappingDraft is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : CreateGeneralMergeReportSlotPaths(mappingDraft);
        string defaultOutputFileName = GetGeneralMergeDefaultOutputFileName(icId);
        WorkbenchRunResult Blocked(
            IReadOnlyList<CompositionIssue> issues,
            IReadOnlyList<OperationRunSummary>? operations = null,
            string profileId = GeneralMergeV2CandidateFallbackProfileId)
        {
            return CreateBlockedReportRunResult(
                GeneralMergeRunIdPrefix,
                profileId,
                GeneralMergeV2CandidateProfileVersion,
                icId,
                IcWorkflowIds.GeneralMerge,
                IcWorkflowIds.GeneralMerge,
                CompositionKind.Merge,
                reportSlotPaths,
                build,
                operations ?? [],
                issues,
                defaultOutputFileName);
        }

        if (!BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration))
        {
            return Blocked(
                [new CompositionIssue(
                    GeneralMergeV2CandidateMemberNotAdmitted,
                    "The General Merge V2 candidate is currently admitted only for explicitly registered members.",
                    icId)]);
        }

        if (!TryParseGeneralMergeCapacity(outputLength, out long capacity, out CompositionIssue? capacityIssue))
        {
            return Blocked(
                [capacityIssue!],
                profileId: registration.ProfileId);
        }

        if (draftIssues is { Count: > 0 })
        {
            return Blocked(draftIssues, profileId: registration.ProfileId);
        }

        if (mappingDraft is null)
        {
            return Blocked(
                [new CompositionIssue(
                    WorkbenchIssueCodes.GeneralMergeMappingRequired,
                    "General Merge requires at least one explicit source-to-target mapping.",
                    IcWorkflowIds.GeneralMerge)],
                profileId: registration.ProfileId);
        }

        GeneralAuthoringAdmissionResult admission = AdmitGeneralMappingDraft(
            mappingDraft,
            capacity);
        if (!admission.IsAdmitted)
        {
            return Blocked(
                admission.ToCompositionIssues(),
                profileId: registration.ProfileId);
        }

        if (!TryCreateGeneralMergeMappings(
                mappingDraft,
                out IReadOnlyList<ExplicitMapping> explicitMappings,
                out IReadOnlyList<AddressSpace> requestAddressSpaces,
                out IReadOnlyList<InputArtifactBinding> mappingBindings,
                out IReadOnlyList<CompositionIssue> mappingIssues))
        {
            return Blocked(
                mappingIssues,
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.CopyRange),
                registration.ProfileId);
        }

        if (requestAddressSpaces.Any(static addressSpace => addressSpace.Length > int.MaxValue))
        {
            return Blocked(
                [new CompositionIssue(
                    GeneralMergeV2CandidateInputLengthUnsupported,
                    "The General Merge V2 candidate accepts source inputs up to the supported in-memory composition size.",
                    "source")],
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.CopyRange),
                registration.ProfileId);
        }

        V2CompositionPlanCompileResult compile = registration.Bundle.CompileLogicalOutput(
            registration.ProfileId,
            GeneralMergeV2CandidateProfileVersion,
            icId,
            new V2LogicalOutputCompileRequest(
                (int)capacity,
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
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.CopyRange),
                registration.ProfileId);
        }

        if (!IsExpectedGeneralMergeV2Candidate(composition, registration))
        {
            return Blocked(
                [new CompositionIssue(
                    GeneralMergeV2CandidateCompilationUnexpected,
                    "The selected General Merge V2 artifact does not match the candidate admission contract.",
                    registration.ProfileId)],
                CreateExplicitMappingPlanningOperations(explicitMappings, CompositionOperationKind.CopyRange),
                registration.ProfileId);
        }

        InputArtifactBinding[] candidateBindings =
        [
            .. mappingBindings.Select(binding => CompiledCompositionInputBindingFactory.Create(
                composition,
                binding.AddressSpaceId,
                binding.ArtifactId)),
        ];
        return await RunCompiledCompositionAsync(
            GeneralMergeRunIdPrefix,
            composition,
            candidateBindings,
            candidateBindings[0].ArtifactId,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: null,
            cancellationToken: cancellationToken,
            progress: progress).ConfigureAwait(false);
    }

    private static bool IsExpectedGeneralMergeV2Candidate(
        CompiledComposition composition,
        GeneralMergeV2CandidateRegistration registration)
    {
        return composition.Eligibility == CompiledCompositionEligibility.V2PlanCompiled &&
               composition.Authority is ProfileBundleV2CompilationAuthority &&
               StringComparer.Ordinal.Equals(composition.ProfileId, registration.ProfileId) &&
               StringComparer.Ordinal.Equals(composition.ProfileVersion, GeneralMergeV2CandidateProfileVersion) &&
               composition.V2Details is { } details &&
               details.Provenance.Context is LogicalOutputV2CompilationContext context &&
               details.Provenance.Promotion.Stage == CompiledProfilePromotionStage.ExecutableCandidate &&
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
