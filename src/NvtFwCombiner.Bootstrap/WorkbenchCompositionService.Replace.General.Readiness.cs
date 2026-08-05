using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Resolves exact-route Preview/Build readiness without creating a run report.</summary>
    public static async ValueTask<CapabilityActionReadinessSnapshot?>
        GetGeneralReplaceActionReadinessAsync(
            AuthoringSessionState session,
            string icId,
            string number,
            long referenceCapacity,
            GeneralMappingDraftState mappingDraft,
            string referencePath,
            FileStamp acceptedReferenceStamp,
            WorkbenchFirmwareConfigMetadata? baseFirmware,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(referenceCapacity);
        ArgumentNullException.ThrowIfNull(mappingDraft);
        ArgumentException.ThrowIfNullOrWhiteSpace(referencePath);
        if (!BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
                Profiles.IcSupportCatalog.NormalizeIcId(icId),
                out GeneralReplaceV2Registration? registration))
        {
            return null;
        }

        GeneralAuthoringAdmissionResult admission = AdmitGeneralMappingDraft(
            mappingDraft,
            referenceCapacity,
            CreateCurrentGeneralTrustedParentPolicy(
                registration.ExactParent.Admission.ParentBinding.ProfileId,
                mappingDraft,
                registration.ExactParent.Admission.ParentBinding));
        if (!admission.IsAdmitted ||
            !TryCreateGeneralReplaceMappings(
                admission,
                referenceCapacity,
                out IReadOnlyList<ExplicitMapping> mappings,
                out IReadOnlyList<AddressSpace> addressSpaces,
                out _,
                out _,
                out _))
        {
            return null;
        }

        IcNumberSelection selection = ToIcNumberSelection(number);
        bool profileResolved = TryResolvePostbuildProfile(
            icId,
            GetPostbuildProfiles(icId),
            baseFirmware is not null,
            baseFirmware?.CommonFwVersion,
            out LegacyCombinerPostbuildProfile? profile,
            out CompositionIssue? profileIssue);
        IReadOnlyList<TpFlashMapRegion> regions = BuiltInTpFlashMapCatalog.GetRegions(
            icId,
            selection,
            profileResolved ? profile : null);
        _ = TryPlanGeneralReplacePostbuild(
            icId,
            selection,
            referenceCapacity,
            regions,
            mappings,
            profileResolved,
            profile,
            profileIssue,
            out bool touchesTp,
            out CompositionIssue? planningIssue);
        ActiveSessionSnapshot? retainedSession = session.CurrentSnapshot;
        ResolvedCapability? capability =
            retainedSession?.DraftState is GeneralMappingDraftState retainedDraft &&
            retainedDraft.HasSameValue(mappingDraft) &&
            retainedSession.Slots.All(static slot =>
                    slot.Lifecycle is AuthoringSlotLifecycle.Verified or
                        AuthoringSlotLifecycle.Warning)
            ? retainedSession.ExactCapability
            : null;
        if (capability is null && !TryResolveGeneralReplaceCompiledRoute(
                registration, referenceCapacity, selection, mappingDraft, regions,
                mappings, addressSpaces, out capability, out _))
        {
            return null;
        }

        if (mappingDraft.Rows.Any(static row =>
                row.Source.Kind == GeneralMappingSourceKind.FileArtifact) &&
            !StringComparer.Ordinal.Equals(
                session.CurrentSnapshot?.CompilationFingerprint,
                capability!.CompiledComposition.CompilationFingerprint))
        {
            return null;
        }
        AuthoringSessionTransitionResult activated = session.Activate(CreateGeneralExactCatalog(
            capability!,
            admission.InputResources,
            mappingDraft,
            referenceCapacity));
        if (!activated.Succeeded)
        {
            return null;
        }
        AuthoringSessionTransitionResult drafted = session.SetDraft(mappingDraft);
        if (!drafted.Succeeded ||
            !ReferenceEquals(drafted.Snapshot!.ExactCapability, capability) ||
            !TryAcceptGeneralReplaceReference(
                session,
                referencePath,
                acceptedReferenceStamp))
        {
            return null;
        }
        AuthoringPublicationLease publication = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation,
            capability!.CompiledComposition.CompilationFingerprint);
        (CapabilityActionReadinessSnapshot readiness, _) =
            await ResolveGeneralReplacePostbuildReadinessAsync(
                admission,
                registration.ExactParent.Runtime,
                capability,
                publication.AuthoringRevision,
                touchesTp,
                planningIssue,
                runtimeOverride: null,
                cancellationToken).ConfigureAwait(false);
        AuthoringPublicationResult published = session.TryPublish(
            publication,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                $"general-replace-readiness:{publication.AuthoringRevision.Value}",
                capability.CompiledComposition.CompilationFingerprint));
        return published.Succeeded ? readiness : null;
    }

    /// <summary>Publishes General Replace mapping membership before range parsing succeeds.</summary>
    public static bool PrepareGeneralReplaceSelectionSession(
        AuthoringSessionState session,
        string icId,
        long referenceCapacity,
        IEnumerable<string> mappingIds)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(mappingIds);
        string normalizedIcId = Profiles.IcSupportCatalog.NormalizeIcId(icId);
        ResolvedCapabilityRoute? route = null;
        bool resolved = BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
                normalizedIcId,
                out GeneralReplaceV2Registration? registration) &&
            TryResolveGeneralReplaceRoute(
                registration!,
                referenceCapacity,
                out route,
                out _);
        return resolved &&
            session.Activate(AuthoringCapabilityCatalogSnapshot.FromDynamicRoute(
                    route!,
                    mappingIds)).Succeeded;
    }

    /// <summary>Compiles the exact candidate input contract before entering Checking.</summary>
    public static AuthoringSlotInspectionStartResult BeginGeneralReplaceSelectedFileInspection(
        AuthoringSessionState session,
        string icId,
        string number,
        long referenceCapacity,
        GeneralMappingDraftState draft,
        string referencePath,
        FileStamp acceptedReferenceStamp,
        string mappingId,
        long observedLength)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(referencePath);
        ResolvedCapability? retained = TryRetainGeneralMappingCompilation(
            session, draft, mappingId, observedLength);
        if (retained is not null &&
            session.SetDraft(draft).Succeeded &&
            TryAcceptGeneralReplaceReference(
                session,
                referencePath,
                acceptedReferenceStamp))
        {
            string retainedPath = draft.Rows.Single(row =>
                StringComparer.Ordinal.Equals(row.MappingId, mappingId)).Source.Reference;
            return session.BeginSlotFileInspection(mappingId, retainedPath);
        }
        if (!BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
                Profiles.IcSupportCatalog.NormalizeIcId(icId),
                out GeneralReplaceV2Registration? registration))
        {
            return InspectionStartFailure(session, mappingId);
        }
        if (!TryCollectGeneralCandidateFileLengths(
                session,
                draft,
                mappingId,
                observedLength,
                out IReadOnlyDictionary<string, long> observedFileLengths))
        {
            return InspectionStartFailure(session, mappingId);
        }

        GeneralAuthoringAdmissionResult admission = AdmitGeneralMappingCandidate(
            draft,
            referenceCapacity,
            CreateCurrentGeneralTrustedParentPolicy(
                registration.ExactParent.Admission.ParentBinding.ProfileId,
                draft,
                registration.ExactParent.Admission.ParentBinding),
            observedFileLengths);
        IcNumberSelection selection = ToIcNumberSelection(number);
        IReadOnlyList<TpFlashMapRegion> regions = BuiltInTpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfile: null);
        if (!admission.IsAdmitted ||
            !TryCreateGeneralReplaceMappings(
                admission,
                referenceCapacity,
                out IReadOnlyList<ExplicitMapping> mappings,
                out IReadOnlyList<AddressSpace> spaces,
                out _,
                out _,
                out _,
                allowUnbound: true) ||
            !TryResolveGeneralReplaceCompiledRoute(
                registration,
                referenceCapacity,
                selection,
                draft,
                regions,
                mappings,
                spaces,
                out ResolvedCapability? capability,
                out _) ||
            !session.Activate(CreateGeneralExactCatalog(
                capability!,
                admission.InputResources,
                draft,
                referenceCapacity)).Succeeded ||
            !session.SetDraft(draft).Succeeded ||
            !TryAcceptGeneralReplaceReference(
                session,
                referencePath,
                acceptedReferenceStamp))
        {
            return InspectionStartFailure(session, mappingId);
        }

        string selectedPath = draft.Rows.Single(row =>
            StringComparer.Ordinal.Equals(row.MappingId, mappingId)).Source.Reference;
        return session.BeginSlotFileInspection(mappingId, selectedPath);
    }

    private static bool TryAcceptGeneralReplaceReference(
        AuthoringSessionState session,
        string referencePath,
        FileStamp acceptedReferenceStamp)
    {
        string fullPath = Path.GetFullPath(referencePath);
        AuthoringSlotState? accepted = session.CurrentSnapshot?.Slots.SingleOrDefault(slot =>
            StringComparer.Ordinal.Equals(
                slot.DefinitionId,
                GeneralReplaceV2Registration.ReferenceAddressSpaceId));
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (accepted?.SelectedPath is { } selectedPath &&
            string.Equals(Path.GetFullPath(selectedPath), fullPath, pathComparison) &&
            accepted.FileStamp == acceptedReferenceStamp &&
            accepted.Lifecycle is AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning)
        {
            return true;
        }

        AuthoringSlotInspectionStartResult started = session.BeginSlotFileInspection(
            GeneralReplaceV2Registration.ReferenceAddressSpaceId,
            fullPath);
        return started.Succeeded && session.TryAcceptSlotFileInspection(
            started.Lease!,
            new GeneralSelectedFileInspection(
                GeneralReplaceV2Registration.ReferenceAddressSpaceId,
                started.Snapshot!.AuthoringRevision,
                fullPath,
                acceptedReferenceStamp)).Succeeded;
    }

    private static AuthoringSlotInspectionStartResult InspectionStartFailure(
        AuthoringSessionState session,
        string mappingId)
    {
        return new AuthoringSlotInspectionStartResult(
            session.CurrentSnapshot,
            Lease: null,
            new AuthoringSessionIssue(
                AuthoringSessionIssueCodes.RouteUnavailable,
                "The selected General file did not compile one exact input contract.",
                mappingId));
    }

    private static LegacyCombinerPostbuildCommandPlan? TryPlanGeneralReplacePostbuild(
        string icId,
        IcNumberSelection selection,
        long capacity,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> mappings,
        bool profileResolved,
        LegacyCombinerPostbuildProfile? profile,
        CompositionIssue? profileIssue,
        out bool touchesTp,
        out CompositionIssue? issue)
    {
        touchesTp = GeneralReplaceTouchesTpRegion(regions, mappings);
        issue = null;
        if (!touchesTp)
        {
            return null;
        }

        if (!profileResolved)
        {
            issue = profileIssue;
            return null;
        }

        LegacyCombinerPostbuildCommandPlan plan;
        try
        {
            plan = LegacyCombinerPostbuildPlanner.CreatePlan(profile!, selection);
        }
        catch (ArgumentException exception)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceGeneralIcNumberUnsupported,
                exception.Message,
                "number");
            return null;
        }

        long requiredCapacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(plan, []);
        if (capacity < requiredCapacity)
        {
            issue = new CompositionIssue(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                $"Base flash BIN is too short for {icId} General Replace postbuild (actual {capacity} bytes, required at least {requiredCapacity} bytes).",
                WorkbenchSlotIds.ReplaceBase);
            return null;
        }

        if (LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForInPlaceRefresh(
                plan,
                capacity).Count == 0)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceGeneralPostbuildWriteRangeMissing,
                "No approved postbuild write range could be derived for TP-touching General Replace.",
                "postbuild");
            return null;
        }

        return plan;
    }

    private static bool TryResolveGeneralReplaceCompiledRoute(
        GeneralReplaceV2Registration? registration,
        long capacity,
        IcNumberSelection selection,
        GeneralMappingDraftState mappingDraft,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> mappings,
        IReadOnlyList<AddressSpace> addressSpaces,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        resolvedCapability = null;
        if (registration is null ||
            !IsGeneralReplaceDpV2Route(selection, mappingDraft, regions, mappings))
        {
            issues = [new CompositionIssue(
                WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                "The selected General Replace shape has no exact evidence-backed V2 route.",
                "mapping")];
            return false;
        }

        if (!TryResolveGeneralReplaceRoute(
                registration,
                capacity,
                out ResolvedCapabilityRoute? route,
                out issues))
        {
            return false;
        }

        Profiles.V2.V2CompositionPlanCompileResult compile = registration.Compile(
            capacity,
            addressSpaces,
            mappings);
        if (compile.CompiledComposition is null)
        {
            issues = compile.Issues;
            return false;
        }

        resolvedCapability = route!.BindCompilation(
            compile.CompiledComposition,
            registration.CreateMetadataPlan(compile.CompiledComposition));
        issues = [];
        return true;
    }

    private static bool TryResolveGeneralReplaceRoute(
        GeneralReplaceV2Registration registration,
        long capacity,
        out ResolvedCapabilityRoute? route,
        out IReadOnlyList<CompositionIssue> issues)
    {
        IReadOnlyList<Domain.Firmware.FirmwareImageMap> maps = registration.GetMapVariants(
            out _,
            out IReadOnlyList<CompositionIssue> mapIssues);
        Domain.Firmware.FirmwareImageMap? map = mapIssues.Count == 0
            ? maps.SingleOrDefault(candidate => candidate.CapacityBytes == capacity)
            : null;
        if (map is null)
        {
            route = null;
            issues = mapIssues.Count == 0
                ? [new CompositionIssue(
                    CapabilityCatalogIssueCodes.RouteUnavailable,
                    "The selected General Replace capacity has no canonical map route.")]
                : mapIssues;
            return false;
        }

        var identity = new CapabilityRouteIdentity(
            registration.IcId,
            Profiles.IcWorkflowIds.GeneralReplace,
            "1-ic",
            map.MapId);
        CapabilityRouteResolutionResult resolution =
            WorkbenchHostServices.CanonicalCapabilities.Read(
                catalog => catalog.ResolveDynamicRoute(identity.RouteId));
        route = resolution.Route;
        issues = resolution.Succeeded
            ? []
            : [new CompositionIssue(resolution.Issue!.Code, resolution.Issue.Message)];
        return resolution.Succeeded;
    }
}
