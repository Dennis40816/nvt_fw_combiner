using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
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
        CompositionRunProgressFeed? progress = null,
        ResolvedCapability? acceptedCapability = null)
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

        GeneralMergePlanningResult planning = PlanGeneralMergeDraft(
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

    /// <summary>Compiles one accepted General Merge draft into exact-route action readiness.</summary>
    public static CapabilityActionReadinessSnapshot? GetGeneralMergeActionReadiness(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(draft);
        ActiveSessionSnapshot? retainedSession = session.CurrentSnapshot;
        ResolvedCapability? retainedCapability =
            retainedSession?.DraftState is GeneralMergeDraftState retainedDraft &&
            Equals(retainedDraft.OutputInitializer, draft.OutputInitializer) &&
            retainedDraft.Mappings.HasSameCompilationInputs(draft.Mappings) &&
            retainedSession.Slots.All(static slot =>
                    slot.Lifecycle is AuthoringSlotLifecycle.Verified or
                        AuthoringSlotLifecycle.Warning)
            ? retainedSession.ExactCapability
            : null;
        if (!BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration) ||
            PlanGeneralMergeDraft(
                icId,
                registration,
                draft,
                savedRulePolicy: null,
                acceptedCapability: retainedCapability).Plan is not { } plan)
        {
            return null;
        }

        if (!StringComparer.Ordinal.Equals(
                session.CurrentSnapshot?.CompilationFingerprint,
                plan.Capability.CompiledComposition.CompilationFingerprint))
        {
            return null;
        }
        AuthoringSessionTransitionResult activated = session.Activate(CreateGeneralExactCatalog(
            plan.Capability,
            plan.InputResources,
            plan.MappingDraft));
        if (!activated.Succeeded)
        {
            return null;
        }
        AuthoringSessionTransitionResult drafted = session.SetDraft(draft);
        if (!drafted.Succeeded ||
            !ReferenceEquals(drafted.Snapshot!.ExactCapability, plan.Capability))
        {
            return null;
        }
        ActiveSessionSnapshot current = drafted.Snapshot;
        var admission = CapabilityAdmissionSnapshot.FromResolvedCapability(
            plan.Capability,
            current.AuthoringRevision);
        var runtime = new RuntimeDependencyReadinessSnapshot(
            admission.RouteId,
            admission.CapabilityFingerprint,
            admission.CompilationFingerprint,
            admission.ResolutionToken,
            admission.AuthoringRevision,
            generation: 1,
            DateTimeOffset.UnixEpoch,
            []);
        CapabilityActionReadinessSnapshot readiness = CapabilityActionReadinessResolver.Resolve(
            admission,
            plan.MappingDraft.Rows.Select(static row =>
                new CapabilityChildReadiness(row.MappingId, ResolvedChildReadiness.Ready)),
            runtime,
            currentRuntimeDependencyGeneration: 1);
        AuthoringPublicationLease lease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation,
            plan.Capability.CompiledComposition.CompilationFingerprint);
        return session.TryPublish(lease, new AuthoringDerivedPublication(
            AuthoringDerivedResultKind.Validation,
            $"general-merge-readiness:{current.AuthoringRevision.Value}",
            plan.Capability.CompiledComposition.CompilationFingerprint)).Succeeded ? readiness : null;
    }

    /// <summary>Publishes General Merge mapping membership before range parsing succeeds.</summary>
    public static bool PrepareGeneralMergeSelectionSession(
        AuthoringSessionState session,
        string icId,
        IEnumerable<string> mappingIds)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(mappingIds);
        string[] definitions = [.. mappingIds];
        var identity = new CapabilityRouteIdentity(
            icId,
            IcWorkflowIds.GeneralMerge,
            "not-applicable",
            "generic");
        CapabilityRouteResolutionResult resolution =
            WorkbenchHostServices.CanonicalCapabilities.Read(
                catalog => catalog.ResolveDynamicRoute(identity.RouteId));
        return resolution.Succeeded &&
            session.Activate(AuthoringCapabilityCatalogSnapshot.FromDynamicRoute(
                resolution.Route!,
                definitions)).Succeeded;
    }

    /// <summary>Compiles the exact candidate input contract before entering Checking.</summary>
    public static AuthoringSlotInspectionStartResult BeginGeneralMergeSelectedFileInspection(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft,
        string mappingId,
        long observedLength)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ActiveSessionSnapshot? current = session.CurrentSnapshot;
        ResolvedCapability? retained = current?.DraftState is GeneralMergeDraftState currentDraft &&
            Equals(currentDraft.OutputInitializer, draft.OutputInitializer)
                ? TryRetainGeneralMappingCompilation(
                    session, draft.Mappings, mappingId, observedLength)
                : null;
        bool hasCandidateLengths = TryCollectGeneralCandidateFileLengths(
            session,
            draft.Mappings,
            mappingId,
            observedLength,
            out IReadOnlyDictionary<string, long> observedFileLengths);
        bool prepared = retained is not null
            ? session.SetDraft(draft).Succeeded
            : hasCandidateLengths &&
            BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration) &&
            PlanGeneralMergeDraft(
                icId,
                registration,
                draft,
                savedRulePolicy: null,
                observedFileLengths).Plan is { } plan &&
            session.Activate(CreateGeneralExactCatalog(
                plan.Capability,
                plan.InputResources,
                plan.MappingDraft)).Succeeded &&
            session.SetDraft(draft).Succeeded;
        return prepared
            ? session.BeginSlotFileInspection(
                mappingId,
                draft.Mappings.Rows.Single(row =>
                    StringComparer.Ordinal.Equals(row.MappingId, mappingId)).Source.Reference)
            : new AuthoringSlotInspectionStartResult(
                session.CurrentSnapshot,
                Lease: null,
                new AuthoringSessionIssue(
                    AuthoringSessionIssueCodes.RouteUnavailable,
                    "The selected General file did not compile one exact input contract.",
                    mappingId));
    }

    private static GeneralMergePlanningResult PlanGeneralMergeDraft(
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
            GeneralSelectedFileBindingResult accepted = RequireAcceptedGeneralSelectedFiles(mappingDraft);
            if (!accepted.Succeeded)
            {
                return new(null, null, [], accepted.Issues);
            }
        }

        GeneralTrustedParentResourcePolicy trustedParent =
            CreateCurrentGeneralTrustedParentPolicy(
                registration.ProfileId,
                mappingDraft,
                registration.Bundle.GetGeneralMergeSavedRuleAdmissionContext(
                    registration.ProfileId).ParentBinding);
        GeneralAuthoringAdmissionResult admission = observedFileLengths is null
            ? AdmitGeneralMappingDraft(
                mappingDraft,
                draft.OutputInitializer.Capacity,
                trustedParent,
                savedRulePolicy)
            : AdmitGeneralMappingCandidate(
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
            IcWorkflowIds.GeneralMerge,
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
                    mappingDraft, acceptedCapability, bindings, admission.InputResources),
                    admission, mappings, []);
        }

        V2CompositionPlanCompileResult compile = registration.Bundle.CompileLogicalOutput(
            registration.ProfileId,
            registration.ProfileVersion,
            icId,
            new V2LogicalOutputCompileRequest(
                draft.OutputInitializer,
                spaces.Select(static space => new V2LogicalOutputInputBinding(
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

    private sealed record GeneralMergePlanningResult(
        GeneralMergeCompiledPlan? Plan,
        GeneralAuthoringAdmissionResult? Admission,
        IReadOnlyList<ExplicitMapping> ExplicitMappings,
        IReadOnlyList<CompositionIssue> Issues);

    private sealed record GeneralMergeCompiledPlan(
        GeneralMappingDraftState MappingDraft,
        ResolvedCapability Capability,
        IReadOnlyList<InputArtifactBinding> MappingBindings,
        IReadOnlyList<GeneralInputResource> InputResources);

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
