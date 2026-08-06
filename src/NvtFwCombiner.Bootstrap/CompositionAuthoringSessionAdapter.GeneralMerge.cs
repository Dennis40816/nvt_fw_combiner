using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionAuthoringSessionAdapter
{
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
            CompositionPlanningAdapter.PlanGeneralMergeDraft(
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
        AuthoringSessionTransitionResult activated = session.Activate(
            CanonicalAuthoringAdapter.CreateGeneralExactCatalog(
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
        CapabilityActionReadinessSnapshot readiness =
            CapabilityActionReadinessResolver.Resolve(
                admission,
                plan.MappingDraft.Rows.Select(static row =>
                    new CapabilityChildReadiness(
                        row.MappingId,
                        ResolvedChildReadiness.Ready)),
                runtime,
                currentRuntimeDependencyGeneration: 1);
        AuthoringPublicationLease lease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation,
            plan.Capability.CompiledComposition.CompilationFingerprint);
        return session.TryPublish(lease, new AuthoringDerivedPublication(
            AuthoringDerivedResultKind.Validation,
            $"general-merge-readiness:{current.AuthoringRevision.Value}",
            plan.Capability.CompiledComposition.CompilationFingerprint)).Succeeded
                ? readiness
                : null;
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
            ExperienceIds.GeneralMerge,
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
        ResolvedCapability? retained =
            current?.DraftState is GeneralMergeDraftState currentDraft &&
            Equals(currentDraft.OutputInitializer, draft.OutputInitializer)
                ? CanonicalAuthoringAdapter.TryRetainGeneralMappingCompilation(
                    session,
                    draft.Mappings,
                    mappingId,
                    observedLength)
                : null;
        bool hasCandidateLengths =
            CanonicalAuthoringAdapter.TryCollectGeneralCandidateFileLengths(
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
              CompositionPlanningAdapter.PlanGeneralMergeDraft(
                  icId,
                  registration,
                  draft,
                  savedRulePolicy: null,
                  observedFileLengths).Plan is { } plan &&
              session.Activate(CanonicalAuthoringAdapter.CreateGeneralExactCatalog(
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
}
