using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string GeneralMergeV2CandidateIcId = "NT51920";
    private const string GeneralMergeV2CandidateProfileId = "nt51920-general-merge-logical-candidate";
    private const string GeneralMergeV2CandidateProfileVersion = "0.1.0";
    private const string GeneralMergeV2CandidateMemberNotAdmitted = "general-merge.v2-candidate.member-not-admitted";
    private const string GeneralMergeV2CandidateInputLengthUnsupported = "general-merge.v2-candidate.input-length-unsupported";
    private const string GeneralMergeV2CandidateCompilationUnexpected = "general-merge.v2-candidate.compilation-unexpected";

    /// <summary>Runs the explicit NT51920 logical-output V2 parity candidate without changing the default General Merge route.</summary>
    internal static async ValueTask<WorkbenchRunResult> RunGeneralMergeV2CandidateAsync(
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
        const string defaultOutputFileName = "nt51920-general-merge.bin";
        if (!StringComparer.Ordinal.Equals(icId, GeneralMergeV2CandidateIcId))
        {
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                [],
                [new CompositionIssue(
                    GeneralMergeV2CandidateMemberNotAdmitted,
                    "The General Merge V2 candidate is currently admitted only for NT51920.",
                    icId)],
                defaultOutputFileName,
                succeeded: false);
        }

        if (!TryParseGeneralMergeCapacity(outputLength, out long capacity, out CompositionIssue? capacityIssue))
        {
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                [],
                [capacityIssue!],
                defaultOutputFileName,
                succeeded: false);
        }

        if (mappingInputs.Count == 0)
        {
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                [],
                [new CompositionIssue(
                    WorkbenchIssueCodes.GeneralMergeMappingRequired,
                    "General Merge requires at least one explicit source-to-target mapping.",
                    IcWorkflowIds.GeneralMerge)],
                defaultOutputFileName,
                succeeded: false);
        }

        if (!TryCreateGeneralMergeMappings(
                mappingInputs,
                out IReadOnlyList<ExplicitMapping> explicitMappings,
                out IReadOnlyList<AddressSpace> requestAddressSpaces,
                out IReadOnlyList<InputArtifactBinding> mappingBindings,
                out IReadOnlyList<CompositionIssue> mappingIssues))
        {
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                mappingIssues,
                defaultOutputFileName,
                succeeded: false);
        }

        if (requestAddressSpaces.Any(static addressSpace => addressSpace.Length > int.MaxValue))
        {
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                [new CompositionIssue(
                    GeneralMergeV2CandidateInputLengthUnsupported,
                    "The General Merge V2 candidate accepts source inputs up to the supported in-memory composition size.",
                    "source")],
                defaultOutputFileName,
                succeeded: false);
        }

        V2CompositionPlanCompileResult compile = s_nt51920GeneralMergeLogicalCandidateV2Bundle.CompileLogicalOutput(
            GeneralMergeV2CandidateProfileId,
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
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                compile.Issues,
                defaultOutputFileName,
                succeeded: false);
        }

        if (!IsExpectedGeneralMergeV2Candidate(composition))
        {
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                [new CompositionIssue(
                    GeneralMergeV2CandidateCompilationUnexpected,
                    "The selected General Merge V2 artifact does not match the candidate admission contract.",
                    GeneralMergeV2CandidateProfileId)],
                defaultOutputFileName,
                succeeded: false);
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

    private static bool IsExpectedGeneralMergeV2Candidate(CompiledComposition composition)
    {
        return composition.Eligibility == CompiledCompositionEligibility.V2PlanCompiled &&
               composition.Authority is ProfileBundleV2CompilationAuthority &&
               StringComparer.Ordinal.Equals(composition.ProfileId, GeneralMergeV2CandidateProfileId) &&
               StringComparer.Ordinal.Equals(composition.ProfileVersion, GeneralMergeV2CandidateProfileVersion) &&
               composition.V2Details is
               {
                   Provenance.Context: LogicalOutputV2CompilationContext
                   {
                       FamilyId: "nt51920",
                       MemberId: GeneralMergeV2CandidateIcId,
                   },
                   Provenance.Promotion.Stage: CompiledProfilePromotionStage.ExecutableCandidate,
               };
    }

    private static WorkbenchRunResult CreateCandidateReport(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName,
        bool succeeded)
    {
        return CreateGeneralMergeReportRunResult(
            icId,
            slotPaths,
            build,
            operations,
            issues,
            outputFileName,
            succeeded,
            GeneralMergeV2CandidateProfileId,
            GeneralMergeV2CandidateProfileVersion);
    }
}
