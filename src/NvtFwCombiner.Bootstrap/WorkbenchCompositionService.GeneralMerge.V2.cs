using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string GeneralMergeV2CandidateProfileVersion = "0.1.0";
    private const string GeneralMergeV2CandidateFallbackProfileId = "general-merge-logical-output-candidate";
    private const string GeneralMergeV2CandidateMemberNotAdmitted = "general-merge.v2-candidate.member-not-admitted";
    private const string GeneralMergeV2CandidateInputLengthUnsupported = "general-merge.v2-candidate.input-length-unsupported";
    private const string GeneralMergeV2CandidateCompilationUnexpected = "general-merge.v2-candidate.compilation-unexpected";
    private const string GeneralMergeLegacyPlanInvalid = "profile.plan.invalid";
    private const string GeneralMergeV2OperationOverlap = "profile.v2.plan.operation-overlap";
    private static readonly ReadOnlyDictionary<string, GeneralMergeV2CandidateRegistration> s_generalMergeV2Candidates = new(
        new GeneralMergeV2CandidateRegistration[]
        {
            new("NT51917", "nt51927", "nt51917-general-merge-logical-candidate", Bundle("nt51917-nt51927-general-merge-logical-candidate")),
            new("NT51919", "nt51929-nt51932", "nt51919-general-merge-logical-candidate", Bundle("nt51919-nt51929-nt51932-general-merge-logical-candidate")),
            new("NT51920", "nt51920", "nt51920-general-merge-logical-candidate", Bundle("nt51920-general-merge-logical-candidate")),
            new("NT51923", "nt51923-nt51926", "nt51923-general-merge-logical-candidate", Bundle("nt51923-nt51926-general-merge-logical-candidate")),
            new("NT51926", "nt51923-nt51926", "nt51926-general-merge-logical-candidate", Bundle("nt51923-nt51926-general-merge-logical-candidate")),
            new("NT51927", "nt51927", "nt51927-general-merge-logical-candidate", Bundle("nt51917-nt51927-general-merge-logical-candidate")),
            new("NT51928", "nt51928", "nt51928-general-merge-logical-candidate", Bundle("nt51928-general-merge-logical-candidate")),
            new("NT51929", "nt51929-nt51932", "nt51929-general-merge-logical-candidate", Bundle("nt51919-nt51929-nt51932-general-merge-logical-candidate")),
            new("NT51930", "nt51930", "nt51930-general-merge-logical-candidate", Bundle("nt51930-general-merge-logical-candidate")),
            new("NT51931", "nt51931", "nt51931-general-merge-logical-candidate", Bundle("nt51931-general-merge-logical-candidate")),
            new("NT51932", "nt51929-nt51932", "nt51932-general-merge-logical-candidate", Bundle("nt51919-nt51929-nt51932-general-merge-logical-candidate")),
            new("NT51950", "nt51950-nt51951-dp-perspective", "nt51950-general-merge-logical-candidate", Bundle("nt51950-nt51951-general-merge-logical-candidate")),
            new("NT51951", "nt51950-nt51951-dp-perspective", "nt51951-general-merge-logical-candidate", Bundle("nt51950-nt51951-general-merge-logical-candidate")),
        }.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    /// <summary>Runs a registered logical-output V2 General Merge profile through the shared application core.</summary>
    private static async ValueTask<WorkbenchRunResult> RunGeneralMergeV2Async(
        string icId,
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        bool overwrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(mappingInputs);

        Dictionary<string, string> reportSlotPaths = CreateGeneralMergeReportSlotPaths(mappingInputs);
        string defaultOutputFileName = GetGeneralMergeDefaultOutputFileName(icId);
        WorkbenchRunResult Blocked(
            IReadOnlyList<CompositionIssue> issues,
            IReadOnlyList<OperationRunSummary>? operations = null,
            string profileId = GeneralMergeV2CandidateFallbackProfileId)
        {
            return CreateGeneralMergeReportRunResult(
                icId,
                reportSlotPaths,
                build,
                operations ?? [],
                issues,
                defaultOutputFileName,
                profileId,
                GeneralMergeV2CandidateProfileVersion);
        }

        if (!s_generalMergeV2Candidates.TryGetValue(icId, out GeneralMergeV2CandidateRegistration? registration))
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

        if (mappingInputs.Count == 0)
        {
            return Blocked(
                [new CompositionIssue(
                    WorkbenchIssueCodes.GeneralMergeMappingRequired,
                    "General Merge requires at least one explicit source-to-target mapping.",
                    IcWorkflowIds.GeneralMerge)],
                profileId: registration.ProfileId);
        }

        if (!TryCreateGeneralMergeMappings(
                mappingInputs,
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
            overwrite: overwrite,
            cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private sealed record GeneralMergeV2CandidateRegistration(
        string IcId,
        string FamilyId,
        string ProfileId,
        BuiltInV2Bundle Bundle);
}
