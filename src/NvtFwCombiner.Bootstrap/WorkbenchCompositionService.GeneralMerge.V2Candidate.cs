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
    private static readonly BuiltInV2Bundle s_nt51920GeneralMergeLogicalCandidateV2Bundle = new(
        "profiles\\built-in\\nt51920-general-merge-logical-candidate",
        "d2f87973576f54b80439f30ef1790f47df2994a6811673f0ceb8ecd5cacdbdc7");
    private static readonly BuiltInV2Bundle s_nt51919Nt51929Nt51932GeneralMergeLogicalCandidateV2Bundle = new(
        "profiles\\built-in\\nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "fabc02474120adb7659d9e069b9c60395cad4620282afdf8ff9e9b915acc4283");
    private static readonly BuiltInV2Bundle s_nt51923Nt51926GeneralMergeLogicalCandidateV2Bundle = new(
        "profiles\\built-in\\nt51923-nt51926-general-merge-logical-candidate",
        "26f12851f81d55bb88a0a0e18ab4f10f451747369e797efbc69fdbf05cdf5a96");
    private static readonly ReadOnlyDictionary<string, GeneralMergeV2CandidateRegistration> s_generalMergeV2Candidates = new(
        new Dictionary<string, GeneralMergeV2CandidateRegistration>(StringComparer.Ordinal)
        {
            ["NT51920"] = new(
                "NT51920",
                "nt51920",
                "nt51920-general-merge-logical-candidate",
                s_nt51920GeneralMergeLogicalCandidateV2Bundle),
            ["NT51919"] = new(
                "NT51919",
                "nt51929-nt51932",
                "nt51919-general-merge-logical-candidate",
                s_nt51919Nt51929Nt51932GeneralMergeLogicalCandidateV2Bundle),
            ["NT51929"] = new(
                "NT51929",
                "nt51929-nt51932",
                "nt51929-general-merge-logical-candidate",
                s_nt51919Nt51929Nt51932GeneralMergeLogicalCandidateV2Bundle),
            ["NT51932"] = new(
                "NT51932",
                "nt51929-nt51932",
                "nt51932-general-merge-logical-candidate",
                s_nt51919Nt51929Nt51932GeneralMergeLogicalCandidateV2Bundle),
            ["NT51923"] = new(
                "NT51923",
                "nt51923-nt51926",
                "nt51923-general-merge-logical-candidate",
                s_nt51923Nt51926GeneralMergeLogicalCandidateV2Bundle),
            ["NT51926"] = new(
                "NT51926",
                "nt51923-nt51926",
                "nt51926-general-merge-logical-candidate",
                s_nt51923Nt51926GeneralMergeLogicalCandidateV2Bundle),
        });

    /// <summary>Runs an explicitly admitted logical-output V2 parity candidate without changing the default General Merge route.</summary>
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
        string defaultOutputFileName = GetGeneralMergeDefaultOutputFileName(icId);
        if (!s_generalMergeV2Candidates.TryGetValue(icId, out GeneralMergeV2CandidateRegistration? registration))
        {
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                [],
                [new CompositionIssue(
                    GeneralMergeV2CandidateMemberNotAdmitted,
                    "The General Merge V2 candidate is currently admitted only for explicitly registered members.",
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
                succeeded: false,
                registration.ProfileId);
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
                succeeded: false,
                registration.ProfileId);
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
                succeeded: false,
                registration.ProfileId);
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
                succeeded: false,
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
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                compile.Issues,
                defaultOutputFileName,
                succeeded: false,
                registration.ProfileId);
        }

        if (!IsExpectedGeneralMergeV2Candidate(composition, registration))
        {
            return CreateCandidateReport(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                [new CompositionIssue(
                    GeneralMergeV2CandidateCompilationUnexpected,
                    "The selected General Merge V2 artifact does not match the candidate admission contract.",
                    registration.ProfileId)],
                defaultOutputFileName,
                succeeded: false,
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

    private static WorkbenchRunResult CreateCandidateReport(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        IReadOnlyList<OperationRunSummary> operations,
        IReadOnlyList<CompositionIssue> issues,
        string outputFileName,
        bool succeeded,
        string profileId = GeneralMergeV2CandidateFallbackProfileId)
    {
        return CreateGeneralMergeReportRunResult(
            icId,
            slotPaths,
            build,
            operations,
            issues,
            outputFileName,
            succeeded,
            profileId,
            GeneralMergeV2CandidateProfileVersion);
    }

    private sealed class GeneralMergeV2CandidateRegistration
    {
        internal GeneralMergeV2CandidateRegistration(
            string icId,
            string familyId,
            string profileId,
            BuiltInV2Bundle bundle)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(icId);
            ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
            ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
            ArgumentNullException.ThrowIfNull(bundle);
            IcId = icId;
            FamilyId = familyId;
            ProfileId = profileId;
            Bundle = bundle;
        }

        internal string IcId { get; }

        internal string FamilyId { get; }

        internal string ProfileId { get; }

        internal BuiltInV2Bundle Bundle { get; }
    }
}
