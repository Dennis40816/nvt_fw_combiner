using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal interface IGeneralAuthoringPlanner
{
    bool CanPlanGeneralReplace(string icId);

    GeneralAuthoringAdmissionResult GetGeneralMergeAdmission(
        string icId,
        GeneralMergeDraftState draft);

    GeneralAuthoringAdmissionResult? GetGeneralReplaceAdmission(
        string icId,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft);

    GeneralMergeOutputInitializer GetGeneralMergeDefaultOutputInitializer(string icId);

    GeneralMergeAuthoringPlanResult PlanGeneralMerge(
        string icId,
        GeneralMergeDraftState draft,
        IReadOnlyDictionary<string, long> observedFileLengths,
        ResolvedCapability? retainedCapability);

    GeneralReplaceAuthoringPlanResult PlanGeneralReplace(
        string icId,
        string number,
        GeneralMappingDraftState draft,
        ReadOnlyMemory<byte> referenceBytes,
        IReadOnlyDictionary<string, long> observedFileLengths,
        ResolvedCapability? retainedCapability);
}

internal interface IRuntimeDependencyReadinessLeaseProvider
{
    RuntimeDependencyReadinessLease AcquireCurrent();
}

internal sealed record RuntimeDependencyReadinessLease(
    IRuntimeDependencyReadinessProvider ReadinessProvider,
    long Generation,
    Func<long, bool> GenerationIsCurrent);

internal sealed record GeneralReplaceRuntimeAuthority(
    SavedRuleParentIdentity ParentBinding,
    IReadOnlyList<string> ProcessorStageIds,
    IReadOnlyList<ExternalProcessorDependencyReference> RuntimeDependencies);

internal sealed record GeneralMergeAuthoringPlan(
    GeneralMappingDraftState MappingDraft,
    ResolvedCapability Capability,
    IReadOnlyList<GeneralInputResource> InputResources);

internal sealed record GeneralMergeAuthoringPlanResult(
    GeneralMergeAuthoringPlan? Plan,
    GeneralAuthoringAdmissionResult? Admission,
    IReadOnlyList<CompositionIssue> Issues);

internal sealed record GeneralReplaceAuthoringPlan(
    GeneralMappingDraftState MappingDraft,
    ResolvedCapability Capability,
    GeneralAuthoringAdmissionResult Admission,
    GeneralReplaceRuntimeAuthority RuntimeAuthority,
    bool RequiresPostbuild,
    CompositionIssue? PlanningIssue,
    long ReferenceCapacity);

internal sealed record GeneralReplaceAuthoringPlanResult(
    GeneralReplaceAuthoringPlan? Plan,
    GeneralAuthoringAdmissionResult? Admission,
    IReadOnlyList<CompositionIssue> Issues);

internal sealed partial class GeneralAuthoringExperience : IGeneralAuthoring
{
    private const string GeneralReplaceReferenceAddressSpaceId = "reference-image";
    private readonly IGeneralAuthoringPlanner _planner;
    private readonly ISelectedFileContentInspector _contentInspector;
    private readonly GeneralSelectedFileInspectionService _selectedFileInspector;
    private readonly IRuntimeDependencyReadinessLeaseProvider _runtimeLeases;
    private readonly ISystemClock _clock;

    internal GeneralAuthoringExperience(
        IGeneralAuthoringPlanner planner,
        ISelectedFileContentInspector contentInspector,
        IRuntimeDependencyReadinessLeaseProvider runtimeLeases,
        ISystemClock clock)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _contentInspector = contentInspector ?? throw new ArgumentNullException(nameof(contentInspector));
        _selectedFileInspector = new GeneralSelectedFileInspectionService(
            contentInspector,
            GeneralAuthoringTechnicalLimits.Default.MaximumFileBytes);
        _runtimeLeases = runtimeLeases ?? throw new ArgumentNullException(nameof(runtimeLeases));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public GeneralAuthoringAdmissionResult GetMergeAdmission(
        string icId,
        GeneralMergeDraftState draft)
    {
        return _planner.GetGeneralMergeAdmission(icId, draft);
    }

    public GeneralAuthoringAdmissionResult? GetReplaceAdmission(
        string icId,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft)
    {
        return _planner.GetGeneralReplaceAdmission(icId, referenceCapacity, mappingDraft);
    }

    public string GetDefaultOutputLength(string icId)
    {
        return GeneralMergeAuthoringUseCase.FormatDefaultOutputLength(
            _planner.GetGeneralMergeDefaultOutputInitializer(icId));
    }

    public string GetDefaultOutputFillByte(string icId)
    {
        return GeneralMergeAuthoringUseCase.FormatDefaultOutputFillByte(
            _planner.GetGeneralMergeDefaultOutputInitializer(icId));
    }

    public async ValueTask<GeneralAuthoringSessionPreparation>
        PrepareMergeSessionAsync(
            AuthoringSessionState session,
            string icId,
            GeneralMergeDraftState draft,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(session);
        GeneralMappingDraftRow[] fileRows = FileRows(draft.Mappings);
        Dictionary<string, GeneralSelectedFileInspection> inspections = [];
        GeneralAuthoringSessionPreparation? captureFailure =
            await CaptureSelectedFilesAsync(fileRows, inspections, cancellationToken)
                .ConfigureAwait(false);
        if (captureFailure is not null)
        {
            return captureFailure;
        }

        IReadOnlyDictionary<string, long> observedLengths = fileRows.ToDictionary(
            static row => row.MappingId,
            row => inspections[row.MappingId].FileStamp.AcceptedLength,
            StringComparer.Ordinal);
        ResolvedCapability? retainedCapability = TryRetainGeneralMergeCompilation(
            session,
            draft,
            fileRows,
            inspections);
        GeneralMergeAuthoringPlanResult candidate = _planner.PlanGeneralMerge(
            icId,
            draft,
            observedLengths,
            retainedCapability);
        if (candidate.Plan is not { } plan)
        {
            return Failed(candidate.Issues, candidate.Admission);
        }

        if (!session.Activate(CreateExactCatalog(
                plan.Capability,
                plan.InputResources,
                plan.MappingDraft)).Succeeded)
        {
            return Failed("The selected General Merge route is unavailable.");
        }

        GeneralAuthoringSessionPreparation? transitionFailure =
            AcceptDraftAndSelectedFiles(session, draft, fileRows, inspections, "Merge");
        if (transitionFailure is not null)
        {
            return transitionFailure;
        }

        var acceptedDraft =
            (GeneralMergeDraftState)session.CurrentSnapshot!.DraftState!;
        if (!plan.MappingDraft.HasSameCompilationInputs(acceptedDraft.Mappings))
        {
            return Failed("The selected General Merge mapping compilation became stale.");
        }

        CapabilityActionReadinessSnapshot? readiness =
            PublishGeneralMergeReadiness(session, plan);
        return readiness is not null && readiness.Build.IsAvailable
            ? new GeneralAuthoringSessionPreparation(
                session.CurrentSnapshot,
                [],
                candidate.Admission,
                readiness)
            : Failed("General Merge readiness could not be published.");
    }

    public async ValueTask<GeneralAuthoringSessionPreparation>
        PrepareReplaceSessionAsync(
            AuthoringSessionState session,
            string icId,
            string number,
            string referencePath,
            GeneralMappingDraftState draft,
            CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referencePath);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(session);
        if (!_planner.CanPlanGeneralReplace(icId))
        {
            return Failed(
                "Not available: the selected General Replace route is unavailable.",
                ExperienceIds.GeneralReplace,
                CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported);
        }

        SelectedFileContentInspection reference;
        try
        {
            reference = await _contentInspector.InspectAsync(
                    referencePath,
                    int.MaxValue,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(
                $"Base firmware inspection failed ({exception.GetType().Name}).",
                CompositionSlotIds.ReplaceBase,
                CompositionPlanningIssueCodes.InputArtifactReadFailed);
        }

        ReadOnlyMemory<byte> referenceBytes = reference.AcceptedBytes ??
            throw new InvalidOperationException(
                "The selected-file content adapter did not retain immutable Base bytes.");
        if (referenceBytes.IsEmpty)
        {
            return Failed("Base flash BIN must not be empty.", CompositionSlotIds.ReplaceBase);
        }

        GeneralMappingDraftRow[] fileRows = FileRows(draft);
        Dictionary<string, GeneralSelectedFileInspection> inspections = [];
        GeneralAuthoringSessionPreparation? captureFailure =
            await CaptureSelectedFilesAsync(fileRows, inspections, cancellationToken)
                .ConfigureAwait(false);
        if (captureFailure is not null)
        {
            return captureFailure;
        }

        IReadOnlyDictionary<string, long> observedLengths = fileRows.ToDictionary(
            static row => row.MappingId,
            row => inspections[row.MappingId].FileStamp.AcceptedLength,
            StringComparer.Ordinal);
        ResolvedCapability? retainedCapability = TryRetainGeneralMappingCompilation(
            session,
            draft,
            fileRows,
            inspections);
        GeneralReplaceAuthoringPlanResult candidate = _planner.PlanGeneralReplace(
            icId,
            number,
            draft,
            referenceBytes,
            observedLengths,
            retainedCapability);
        if (candidate.Plan is not { } plan)
        {
            return Failed(candidate.Issues, candidate.Admission);
        }

        if (!session.Activate(CreateExactCatalog(
                plan.Capability,
                plan.Admission.InputResources,
                plan.MappingDraft,
                plan.ReferenceCapacity)).Succeeded)
        {
            return Failed("The selected General Replace route is unavailable.");
        }

        AuthoringSessionTransitionResult drafted = session.SetDraft(draft);
        if (!drafted.Succeeded)
        {
            return Failed(drafted.Issue!);
        }

        AuthoringSlotInspectionStartResult referenceStarted =
            session.BeginSlotFileInspection(
                GeneralReplaceReferenceAddressSpaceId,
                referencePath);
        if (!referenceStarted.Succeeded ||
            !session.TryAcceptSlotFileInspection(
                referenceStarted.Lease!,
                new GeneralSelectedFileInspection(
                    GeneralReplaceReferenceAddressSpaceId,
                    referenceStarted.Snapshot!.AuthoringRevision,
                    referencePath,
                    reference.FileStamp,
                    reference.DisplayNameHint,
                    reference.LastWriteTimeUtcHint,
                    referenceBytes)).Succeeded)
        {
            return Failed(
                referenceStarted.Issue ?? new AuthoringSessionIssue(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The selected General Replace Base became stale.",
                    CompositionSlotIds.ReplaceBase));
        }

        GeneralAuthoringSessionPreparation? inspectionFailure =
            AcceptSelectedFiles(session, fileRows, inspections, "Replace");
        if (inspectionFailure is not null)
        {
            return inspectionFailure;
        }

        (CapabilityActionReadinessSnapshot Readiness, string? RequiredStageId)? publication =
            await PublishGeneralReplaceReadinessAsync(session, plan, cancellationToken)
                .ConfigureAwait(false);
        CapabilityActionReadinessSnapshot? readiness = publication?.Readiness;
        GeneralReplaceDiagnosticPreviewSummary? diagnostic =
            readiness?.Preview.IsAvailable == true && !readiness.Build.IsAvailable
                ? GeneralReplaceDiagnosticPreviewProjector.Project(
                    plan.ReferenceCapacity,
                    plan.Admission,
                    readiness,
                    publication?.RequiredStageId)
                : null;
        if (readiness is null ||
            session.CurrentSnapshot is not { ExactCapability: not null } acceptedSession)
        {
            return Failed(
                "General Replace readiness could not be published.",
                ExperienceIds.GeneralReplace,
                CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported);
        }

        CompositionRunReport? diagnosticReport = diagnostic is null
            ? null
            : GeneralReplaceDiagnosticPreviewReportProjector.Project(
                acceptedSession,
                plan.Admission,
                diagnostic,
                _clock.UtcNow);
        return new GeneralAuthoringSessionPreparation(
            acceptedSession,
            [],
            plan.Admission,
            readiness,
            diagnosticReport);
    }

    private async ValueTask<GeneralAuthoringSessionPreparation?> CaptureSelectedFilesAsync(
        IEnumerable<GeneralMappingDraftRow> rows,
        Dictionary<string, GeneralSelectedFileInspection> inspections,
        CancellationToken cancellationToken)
    {
        foreach (GeneralMappingDraftRow row in rows)
        {
            GeneralSelectedFileInspectionResult inspected =
                await _selectedFileInspector.InspectAsync(
                        row.MappingId,
                        row.Source.Reference,
                        new AuthoringRevision(1),
                        cancellationToken)
                    .ConfigureAwait(false);
            if (!inspected.Succeeded)
            {
                return Failed(inspected.Issue!);
            }

            inspections.Add(row.MappingId, inspected.Inspection!);
        }

        return null;
    }

    private async ValueTask<(
        CapabilityActionReadinessSnapshot Readiness,
        string? RequiredStageId)?> PublishGeneralReplaceReadinessAsync(
            AuthoringSessionState session,
            GeneralReplaceAuthoringPlan plan,
            CancellationToken cancellationToken)
    {
        AuthoringPublicationLease publication = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation,
            plan.Capability.CompiledComposition.CompilationFingerprint);
        (CapabilityActionReadinessSnapshot readiness, string? requiredStageId) =
            await ResolveGeneralReplaceReadinessAsync(
                    plan,
                    publication.AuthoringRevision,
                    cancellationToken)
                .ConfigureAwait(false);
        AuthoringPublicationResult published = session.TryPublish(
            publication,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                $"general-replace-readiness:{publication.AuthoringRevision.Value}",
                plan.Capability.CompiledComposition.CompilationFingerprint));
        return published.Succeeded ? (readiness, requiredStageId) : null;
    }

    private async ValueTask<(
        CapabilityActionReadinessSnapshot Readiness,
        string? RequiredStageId)> ResolveGeneralReplaceReadinessAsync(
            GeneralReplaceAuthoringPlan plan,
            AuthoringRevision authoringRevision,
            CancellationToken cancellationToken)
    {
        GeneralReplaceRuntimeAuthority authority = plan.RuntimeAuthority;
        SavedRuleParentIdentity parent = authority.ParentBinding;
        bool hasStageAuthority =
            !plan.RequiresPostbuild || authority.ProcessorStageIds.Count != 0;
        CapabilityActionBlocker? executionBlocker =
            plan.PlanningIssue is null && hasStageAuthority
                ? null
                : new CapabilityActionBlocker(
                    CapabilityActionReadinessIssueCodes.PostbuildStageAuthorityMissing,
                    CapabilityReadinessDimension.Execution,
                    plan.PlanningIssue?.OperationId ?? parent.ProfileId,
                    plan.PlanningIssue?.Message ??
                        "Selected General Replace targets require POSTBUILD, but the exact Parent does not declare the required stage.",
                    CapabilityReadinessNextAction.ReviewCompilation);
        ResolvedCapability resolved = plan.Capability;
        var capability = new CapabilityAdmissionSnapshot(
            resolved.Identity.RouteId,
            resolved.CapabilityFingerprint,
            resolved.CompiledComposition.CompilationFingerprint,
            resolved.ResolutionToken,
            authoringRevision,
            resolved.Authoring.Value,
            resolved.ExecutionAdmitted && executionBlocker is null,
            resolved.Evidence.Value,
            resolved.Publication.Value,
            executionBlocker);
        CapabilityChildReadiness[] inputs =
        [
            new CapabilityChildReadiness(
                CompositionSlotIds.ReplaceBase,
                ResolvedChildReadiness.Ready),
            .. plan.Admission.InputResources.Select(static input =>
                new CapabilityChildReadiness(
                    input.SlotId,
                    ResolvedChildReadiness.Ready)),
        ];
        string? requiredStageId =
            plan.RequiresPostbuild && executionBlocker is null
                ? string.Join(">", authority.ProcessorStageIds)
                : null;
        if (!plan.RequiresPostbuild || executionBlocker is not null)
        {
            var runtime = new RuntimeDependencyReadinessSnapshot(
                capability.RouteId,
                capability.CapabilityFingerprint,
                capability.CompilationFingerprint,
                capability.ResolutionToken,
                capability.AuthoringRevision,
                generation: 1,
                DateTimeOffset.UnixEpoch,
                []);
            return (
                CapabilityActionReadinessResolver.Resolve(
                    capability,
                    inputs,
                    runtime,
                    currentRuntimeDependencyGeneration: 1),
                requiredStageId);
        }

        RuntimeDependencyReadinessLease lease = _runtimeLeases.AcquireCurrent();
        var request = new RuntimeDependencyReadinessRequest(
            capability.RouteId,
            capability.CapabilityFingerprint,
            capability.CompilationFingerprint,
            capability.ResolutionToken,
            capability.AuthoringRevision,
            authority.RuntimeDependencies);
        CapabilityActionReadinessSnapshot readiness =
            await CapabilityActionReadinessResolver.RefreshAndResolveAsync(
                    capability,
                    inputs,
                    request,
                    lease.ReadinessProvider,
                    lease.Generation,
                    lease.GenerationIsCurrent,
                    cancellationToken)
                .ConfigureAwait(false);
        return (readiness, requiredStageId);
    }

    private static CapabilityActionReadinessSnapshot? PublishGeneralMergeReadiness(
        AuthoringSessionState session,
        GeneralMergeAuthoringPlan plan)
    {
        ActiveSessionSnapshot current = session.CurrentSnapshot!;
        var admission =
            CapabilityAdmissionSnapshot.FromResolvedCapability(
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
        return session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation,
                $"general-merge-readiness:{current.AuthoringRevision.Value}",
                plan.Capability.CompiledComposition.CompilationFingerprint)).Succeeded
                    ? readiness
                    : null;
    }

    private static GeneralAuthoringSessionPreparation? AcceptDraftAndSelectedFiles(
        AuthoringSessionState session,
        GeneralMergeDraftState draft,
        IReadOnlyList<GeneralMappingDraftRow> rows,
        IReadOnlyDictionary<string, GeneralSelectedFileInspection> inspections,
        string workflowLabel)
    {
        AuthoringSessionTransitionResult drafted = session.SetDraft(draft);
        return drafted.Succeeded
            ? AcceptSelectedFiles(session, rows, inspections, workflowLabel)
            : Failed(drafted.Issue!);
    }

    private static GeneralAuthoringSessionPreparation? AcceptSelectedFiles(
        AuthoringSessionState session,
        IReadOnlyList<GeneralMappingDraftRow> rows,
        IReadOnlyDictionary<string, GeneralSelectedFileInspection> inspections,
        string workflowLabel)
    {
        foreach (GeneralMappingDraftRow row in rows)
        {
            AuthoringSlotInspectionStartResult started =
                session.BeginSlotFileInspection(row.MappingId, row.Source.Reference);
            if (!started.Succeeded)
            {
                return Failed(started.Issue!);
            }

            if (!session.TryAcceptSlotFileInspection(
                    started.Lease!,
                    AtRevision(
                        inspections[row.MappingId],
                        started.Snapshot!.AuthoringRevision)).Succeeded)
            {
                return Failed(
                    $"The selected General {workflowLabel} input became stale.",
                    row.MappingId);
            }
        }

        return null;
    }

    private static AuthoringCapabilityCatalogSnapshot CreateExactCatalog(
        ResolvedCapability capability,
        IReadOnlyList<GeneralInputResource> inputResources,
        GeneralMappingDraftState draft,
        long? referenceCapacity = null)
    {
        var lengths = inputResources.ToDictionary(
            static resource => resource.SlotId,
            static resource => resource.LengthBytes,
            StringComparer.Ordinal);
        var slotLengths = draft.Rows
            .Where(static row => row.Source.Kind == GeneralMappingSourceKind.FileArtifact)
            .ToDictionary(
                static row => row.MappingId,
                row => lengths.GetValueOrDefault(
                    row.MappingId,
                    row.SourceRange.EndExclusive),
                StringComparer.Ordinal);
        if (referenceCapacity is { } capacity)
        {
            slotLengths.Add(GeneralReplaceReferenceAddressSpaceId, capacity);
        }

        return AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(
            capability,
            slotLengths);
    }

    private static ResolvedCapability? TryRetainGeneralMappingCompilation(
        AuthoringSessionState session,
        GeneralMappingDraftState draft,
        IReadOnlyList<GeneralMappingDraftRow> fileRows,
        Dictionary<string, GeneralSelectedFileInspection> inspections)
    {
        ResolvedCapability?[] retained =
        [
            .. fileRows.Select(row => TryRetainGeneralMappingCompilation(
                session,
                draft,
                row.MappingId,
                inspections[row.MappingId].FileStamp.AcceptedLength)),
        ];
        return retained.Length > 0 &&
            retained[0] is { } first &&
            retained.All(candidate => ReferenceEquals(candidate, first))
                ? first
                : null;
    }

    private static ResolvedCapability? TryRetainGeneralMergeCompilation(
        AuthoringSessionState session,
        GeneralMergeDraftState draft,
        IReadOnlyList<GeneralMappingDraftRow> fileRows,
        Dictionary<string, GeneralSelectedFileInspection> inspections)
    {
        ResolvedCapability? retained = TryRetainGeneralMappingCompilation(
            session,
            draft.Mappings,
            fileRows,
            inspections);
        return retained is not null &&
            session.CurrentSnapshot?.DraftState is GeneralMergeDraftState current &&
            current.OutputInitializer == draft.OutputInitializer
                ? retained
                : null;
    }

    private static ResolvedCapability? TryRetainGeneralMappingCompilation(
        AuthoringSessionState session,
        GeneralMappingDraftState draft,
        string mappingId,
        long observedLength)
    {
        ActiveSessionSnapshot? current = session.CurrentSnapshot;
        GeneralMappingDraftState? currentMappings = current?.DraftState switch
        {
            GeneralMergeDraftState merge => merge.Mappings,
            GeneralMappingDraftState replace => replace,
            _ => null,
        };
        ResolvedCapability? exact = current?.ExactCapability;
        InputArtifactBinding? input = exact?.GeneralExecutionPlan?.InputBindings
            .SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.BindingId, mappingId));
        AddressSpace? sourceSpace = input is null
            ? null
            : exact!.CompiledComposition.Plan.AddressSpaces.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(
                    candidate.AddressSpaceId,
                    input.AddressSpaceId));
        bool sameCompiledLength = sourceSpace is not null &&
            sourceSpace.Mutability == AddressSpaceMutability.Immutable &&
            sourceSpace.Length == observedLength;
        return current?.ExactCapability is { } capability &&
            currentMappings is not null &&
            sameCompiledLength &&
            currentMappings.HasSameCompilationInputs(draft)
                ? capability
                : null;
    }

}
