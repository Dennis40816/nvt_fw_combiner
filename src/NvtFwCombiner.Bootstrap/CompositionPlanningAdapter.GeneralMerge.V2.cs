using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

internal static partial class CompositionPlanningAdapter
{
    private const string GeneralMergeV2CandidateInputLengthUnsupported =
        "general-merge.v2-candidate.input-length-unsupported";
    private const string GeneralMergeV2CandidateCompilationUnexpected =
        "general-merge.v2-candidate.compilation-unexpected";
    private const string GeneralMergeLegacyPlanInvalid = "profile.plan.invalid";
    private const string GeneralMergeV2OperationOverlap =
        "profile.v2.plan.operation-overlap";

    internal static GeneralMergePlanningResult PlanGeneralMergeDraft(
        string icId,
        GeneralMergeV2CandidateRegistration registration,
        GeneralMergeDraftState draft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        IReadOnlyDictionary<string, long>? observedFileLengths = null,
        ResolvedCapability? acceptedCapability = null)
    {
        GeneralMappingDraftState mappingDraft = draft.Mappings;
        if (observedFileLengths is null)
        {
            GeneralSelectedFileBindingResult accepted =
                CanonicalAuthoringAdapter.RequireAcceptedGeneralSelectedFiles(mappingDraft);
            if (!accepted.Succeeded)
            {
                return new(null, null, [], accepted.Issues);
            }
        }

        GeneralTrustedParentResourcePolicy trustedParent =
            CanonicalAuthoringAdapter.CreateCurrentGeneralTrustedParentPolicy(
                registration.ProfileId,
                mappingDraft,
                registration.Bundle.GetGeneralMergeSavedRuleAdmissionContext(
                    registration.ProfileId).ParentBinding);
        GeneralAuthoringAdmissionResult admission = observedFileLengths is null
            ? CanonicalAuthoringAdapter.AdmitGeneralMappingDraft(
                mappingDraft,
                draft.OutputInitializer.Capacity,
                trustedParent,
                savedRulePolicy)
            : CanonicalAuthoringAdapter.AdmitGeneralMappingCandidate(
                mappingDraft,
                draft.OutputInitializer.Capacity,
                trustedParent,
                observedFileLengths);
        if (!admission.IsAdmitted)
        {
            return new(null, admission, [], admission.ToCompositionIssues());
        }

        if (!TryCreateGeneralMergeMappings(
                admission,
                out IReadOnlyList<ExplicitMapping> mappings,
                out IReadOnlyList<AddressSpace> spaces,
                out IReadOnlyList<InputArtifactBinding> bindings,
                out IReadOnlyList<CompositionIssue> mappingIssues,
                allowUnbound: observedFileLengths is not null))
        {
            return new(null, admission, mappings, mappingIssues);
        }

        if (spaces.Any(static space => space.Length > int.MaxValue))
        {
            return new(null, admission, mappings, [new CompositionIssue(
                GeneralMergeV2CandidateInputLengthUnsupported,
                "The General Merge V2 candidate accepts source inputs up to the supported in-memory composition size.",
                "source")]);
        }

        var identity = new CapabilityRouteIdentity(
            icId,
            ExperienceIds.GeneralMerge,
            "not-applicable",
            "generic");
        CapabilityRouteResolutionResult resolution =
            WorkbenchHostServices.CanonicalCapabilities.Read(
                catalog => catalog.ResolveDynamicRoute(identity.RouteId));
        if (!resolution.Succeeded)
        {
            return new(null, admission, mappings, [new CompositionIssue(
                resolution.Issue!.Code,
                resolution.Issue.Message)]);
        }

        if (acceptedCapability is not null)
        {
            return !StringComparer.Ordinal.Equals(
                    acceptedCapability.Identity.RouteId,
                    resolution.Route!.Identity.RouteId)
                ? new(null, admission, mappings, [new CompositionIssue(
                    GeneralMergeV2CandidateCompilationUnexpected,
                    "The retained General Merge compilation belongs to another route.")])
                : new(new GeneralMergeCompiledPlan(
                    mappingDraft,
                    acceptedCapability,
                    bindings,
                    admission.InputResources),
                    admission,
                    mappings,
                    []);
        }

        V2CompositionPlanCompileResult compile = registration.Bundle.CompileLogicalOutput(
            registration.ProfileId,
            registration.ProfileVersion,
            icId,
            new V2LogicalOutputCompileRequest(
                draft.OutputInitializer,
                spaces.Select(static space => new V2ExplicitMappingInputBinding(
                    space.AddressSpaceId,
                    "source",
                    (int)space.Length)),
                mappings.Select(static mapping => new ExplicitMapping(
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
        return !compile.IsCompiled || compile.CompiledComposition is not { } composition
            ? new(null, admission, mappings, NormalizeGeneralMergeV2Issues(compile.Issues))
            : !IsExpectedGeneralMergeV2Candidate(composition, registration)
            ? new(null, admission, mappings, [new CompositionIssue(
                GeneralMergeV2CandidateCompilationUnexpected,
                "The selected General Merge V2 artifact does not match the candidate admission contract.",
                registration.ProfileId)])
            : new(
                new GeneralMergeCompiledPlan(
                    mappingDraft,
                    resolution.Route!.BindCompilation(composition),
                    bindings,
                    admission.InputResources),
                admission,
                mappings,
                []);
    }

    internal sealed record GeneralMergePlanningResult(
        GeneralMergeCompiledPlan? Plan,
        GeneralAuthoringAdmissionResult? Admission,
        IReadOnlyList<ExplicitMapping> ExplicitMappings,
        IReadOnlyList<CompositionIssue> Issues);

    internal sealed record GeneralMergeCompiledPlan(
        GeneralMappingDraftState MappingDraft,
        ResolvedCapability Capability,
        IReadOnlyList<InputArtifactBinding> MappingBindings,
        IReadOnlyList<GeneralInputResource> InputResources);

    private static bool IsExpectedGeneralMergeV2Candidate(
        CompiledComposition composition,
        GeneralMergeV2CandidateRegistration registration)
    {
        return composition.Eligibility == CompiledCompositionEligibility.V2PlanCompiled &&
               StringComparer.Ordinal.Equals(composition.V2Details.ProfileId, registration.ProfileId) &&
               StringComparer.Ordinal.Equals(composition.V2Details.ProfileVersion, registration.ProfileVersion) &&
               composition.V2Details.Provenance.Context is LogicalOutputV2CompilationContext context &&
               composition.V2Details.Provenance.Promotion.Stage ==
                   CompiledProfilePromotionStage.ExecutableCandidate &&
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
                    ? new CompositionIssue(
                        GeneralMergeLegacyPlanInvalid,
                        issue.Message,
                        issue.OperationId)
                    : issue),
        ];
    }
}
