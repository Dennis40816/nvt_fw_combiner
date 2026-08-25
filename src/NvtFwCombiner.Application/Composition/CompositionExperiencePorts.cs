using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Focused host-facing projection of one workflow's compiled input-slot inspection.</summary>
public interface ICompiledInputSlotInspector<out TBatch>
{
    /// <summary>Inspects the applicable inputs through one caller-owned distinct-path reader.</summary>
    TBatch InspectInputSlots(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        Func<string, byte[]?> readFirmwareImage);
}

/// <summary>Read-only capability and workflow disclosure consumed by UI and CLI surfaces.</summary>
public interface ICompositionCapabilityExperience
{
    /// <summary>Default IC selected before explicit user input.</summary>
    string DefaultIcId { get; }

    /// <summary>Gets current selectable IC identifiers.</summary>
    IReadOnlyList<string> GetIcIds();

    /// <summary>Gets profile-owned IC-number choices.</summary>
    IReadOnlyList<CapabilityNumberChoice> GetNumberSelectionChoices(string icId);

    /// <summary>Gets catalog counts for startup and Settings disclosure.</summary>
    CapabilityCatalogSummary GetCatalogSummary();

    /// <summary>Gets current DP Replace Reference capacity disclosure.</summary>
    string? GetDpReplaceReferenceCapacityLabel(string icId);

    /// <summary>Gets authorable AB Merge profiles.</summary>
    IReadOnlyList<CapabilityProfileSummary> GetAbMergeProfileSummaries();

    /// <summary>Gets authorable Standard Merge profiles.</summary>
    IReadOnlyList<CapabilityProfileSummary> GetStandardMergeProfileSummaries();

    /// <summary>Gets one authorable Standard Merge profile when declared.</summary>
    CapabilityProfileSummary? FindStandardMergeProfileSummary(string icId);

    /// <summary>Returns whether the current publication declares the IC.</summary>
    bool IsKnownIcId(string icId);

    /// <summary>Gets authorable DP Replace profiles.</summary>
    IReadOnlyList<CapabilityProfileSummary> GetDpReplaceProfileSummaries();

    /// <summary>Gets Replace readiness from canonical publication.</summary>
    CapabilityWorkflowReadiness GetReplaceWorkflowReadiness(
        string icId,
        string replaceMode);

    /// <summary>Returns whether the selected Replace workflow is authorable.</summary>
    bool IsReplaceWorkflowAvailable(string icId, string replaceMode);

    /// <summary>Gets canonical family disclosure without inventing a source member.</summary>
    CapabilityFamilySummary GetIcFamilySummary(string icId);

    /// <summary>Returns whether two members have one declared perfect relationship.</summary>
    bool ArePerfectFamilyMembers(string firstIcId, string secondIcId);

    /// <summary>Returns whether the IC uses a DP-perspective composition.</summary>
    bool IsDpPerspectiveIc(string icId);
}

/// <summary>Focused Standard Merge authoring operations over one canonical workflow owner.</summary>
public interface IStandardMergeAuthoring
{
    /// <summary>Returns whether the IC has one authorable Standard Merge route.</summary>
    bool IsSupported(string icId);

    /// <summary>Gets the selected route's profile id.</summary>
    string? GetProfileId(string icId);

    /// <summary>Gets required compiled input spaces.</summary>
    IReadOnlyList<string> GetRequiredAddressSpaces(string icId);

    /// <summary>Gets all selectable compiled input spaces.</summary>
    IReadOnlyList<string> GetInputAddressSpaces(string icId);

    /// <summary>Projects exact selection readiness.</summary>
    CompiledAuthoringSelectionSnapshot GetAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null);

    /// <summary>Prepares one exact accepted session from immutable inputs.</summary>
    CompiledAuthoringSessionPreparation PrepareSession(
        AuthoringSessionState session,
        string icId,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs);
}

/// <summary>Focused AB Merge authoring operations over one canonical workflow owner.</summary>
public interface IAbMergeAuthoring
{
    /// <summary>Returns whether the IC has one authorable AB Merge route.</summary>
    bool IsAvailable(string icId);

    /// <summary>Gets compiled topology choices.</summary>
    IReadOnlyList<CapabilityTopologyChoice> GetTopologyChoices(string icId);

    /// <summary>Projects exact selection readiness.</summary>
    CompiledAuthoringSelectionSnapshot GetAuthoringSnapshot(
        string icId,
        string? topologyToken,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null);

    /// <summary>Prepares one exact accepted session from immutable inputs.</summary>
    CompiledAuthoringSessionPreparation PrepareSession(
        AuthoringSessionState session,
        string icId,
        string? topologyToken,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs);
}

/// <summary>Focused DP Replace authoring operations over one canonical workflow owner.</summary>
public interface IDpReplaceAuthoring
{
    /// <summary>Projects exact selection readiness.</summary>
    CompiledAuthoringSelectionSnapshot GetAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null);

    /// <summary>Prepares one exact accepted session from immutable inputs.</summary>
    CompiledAuthoringSessionPreparation PrepareSession(
        AuthoringSessionState session,
        string icId,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs);
}

/// <summary>Focused General Merge and General Replace authoring owner.</summary>
public interface IGeneralAuthoring
{
    /// <summary>Gets General Merge admission from the trusted parent.</summary>
    GeneralAuthoringAdmissionResult GetMergeAdmission(
        string icId,
        GeneralMergeDraftState draft);

    /// <summary>Gets General Replace admission from the trusted parent.</summary>
    GeneralAuthoringAdmissionResult? GetReplaceAdmission(
        string icId,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft);

    /// <summary>Gets the profile-owned default output length text.</summary>
    string GetDefaultOutputLength(string icId);

    /// <summary>Gets the profile-owned default output fill-byte text.</summary>
    string GetDefaultOutputFillByte(string icId);

    /// <summary>Prepares one exact General Merge accepted session.</summary>
    ValueTask<GeneralAuthoringSessionPreparation> PrepareMergeSessionAsync(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft,
        CancellationToken cancellationToken,
        IProgress<AuthoringInspectionProgress>? progress = null);

    /// <summary>Prepares one exact General Replace accepted session.</summary>
    ValueTask<GeneralAuthoringSessionPreparation> PrepareReplaceSessionAsync(
        AuthoringSessionState session,
        string icId,
        string number,
        string referencePath,
        GeneralMappingDraftState draft,
        CancellationToken cancellationToken,
        IProgress<AuthoringInspectionProgress>? progress = null);
}

/// <summary>Focused trusted Saved Rule v2 authoring operations.</summary>
public interface ISavedRuleAuthoring
{
    /// <summary>Loads one exact General Merge Saved Rule through its trusted Parent.</summary>
    SavedRuleV2DraftLoadResult<GeneralMergeDraftState> LoadGeneralMergeSavedRule(
        string icId,
        string path,
        IReadOnlyDictionary<string, string> slotsById);

    /// <summary>Gets the exact Parent-owned General Replace reference slot.</summary>
    string? GetGeneralReplaceSavedRuleReferenceSlotId(string icId);

    /// <summary>Loads one exact General Replace Saved Rule through its trusted Parent.</summary>
    SavedRuleV2DraftLoadResult<GeneralMappingDraftState> LoadGeneralReplaceSavedRule(
        string icId,
        string path,
        IReadOnlyDictionary<string, string> slotsById);

    /// <summary>Inspects one Saved Rule v2 document against its exact trusted Parent.</summary>
    SavedRuleV2InspectionResult InspectSavedRuleV2(string path);
}

/// <summary>Focused CtrlRAM Replace authoring owner.</summary>
public interface ICtrlRamAuthoring
{
    /// <summary>Gets one coherent CtrlRAM region and input-slot discovery publication.</summary>
    CtrlRamInspectionDisplay GetDiscoveryDisplay(
        string icId,
        string number,
        string? basePath);

    /// <summary>Gets CtrlRAM discovery from one already admitted immutable base snapshot.</summary>
    CtrlRamInspectionDisplay GetDiscoveryDisplayFromAcceptedBase(
        string icId,
        string number,
        ReadOnlyMemory<byte> acceptedBaseBytes);

    /// <summary>Prepares one exact CtrlRAM Replace session from immutable inputs.</summary>
    CtrlRamAuthoringSessionPreparation PrepareSession(
        AuthoringSessionState session,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit = null);

    /// <summary>Adopts one already-inspected exact batch without resolving or reading it again.</summary>
    AuthoringSessionTransitionResult AdoptInspectedBatch(
        AuthoringSessionState session,
        AuthoringCapabilityCatalogSnapshot catalog,
        IReadOnlyCollection<AuthoringInputSlotStatus> statuses);

    /// <summary>Gets CtrlRAM action readiness.</summary>
    ValueTask<CapabilityActionReadinessSnapshot?> GetActionReadinessAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        CancellationToken cancellationToken);

    /// <summary>Transitions CtrlRAM firmware-version authoring.</summary>
    CtrlRamAuthoringTransitionResult TransitionFirmwareVersionCompilation(
        AuthoringSessionState session,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit);

    /// <summary>Projects path-free confirmation facts from one exact accepted session lease.</summary>
    CompiledInputVersionObservation? ProjectFirmwareVersionConfirmationLease(ActiveSessionSnapshot session);

    /// <summary>Checks whether the exact accepted session lease is still current.</summary>
    bool IsFirmwareVersionConfirmationLeaseCurrent(ActiveSessionSnapshot current, ActiveSessionSnapshot lease);

}

/// <summary>Authoring-session lifecycle and per-slot readiness operations.</summary>
public sealed record GeneralAuthoringSessionPreparation(
    ActiveSessionSnapshot? AcceptedSession,
    IReadOnlyList<CompositionIssue> Issues,
    GeneralAuthoringAdmissionResult? Admission = null,
    CapabilityActionReadinessSnapshot? Readiness = null,
    CompositionRunReport? DiagnosticPreviewReport = null)
{
    /// <summary>True only when one exact accepted session is available.</summary>
    public bool Succeeded => AcceptedSession is not null && Issues.Count == 0;

    /// <summary>Projects one mapping-scoped issue without exposing Domain policy to Presentation.</summary>
    public GeneralSelectedFileInspectionIssue? GetSelectedFileIssue(string definitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        CompositionIssue? issue = Issues.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.OperationId, definitionId));
        return issue is null
            ? null
            : new GeneralSelectedFileInspectionIssue(
                issue.Code,
                issue.Message,
                definitionId);
    }
}

/// <summary>Result of preparing one exact CtrlRAM authoring session.</summary>
public sealed record CtrlRamAuthoringSessionPreparation(
    ActiveSessionSnapshot? AcceptedSession,
    IReadOnlyList<CompositionIssue> Issues)
{
    /// <summary>True only when current compiled inspection owns the accepted session.</summary>
    public bool Succeeded => AcceptedSession is not null && Issues.Count == 0;
}

/// <summary>Truthful completed and total work reported by selected-file inspection.</summary>
public readonly record struct AuthoringInspectionProgress(int CompletedWork, int TotalWork);

/// <summary>Immutable firmware inspection operations.</summary>
public interface IFirmwareInspection
{
    /// <summary>Inspects every distinct path once and reports content-authoritative stability.</summary>
    ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        CancellationToken cancellationToken,
        IProgress<AuthoringInspectionProgress>? progress = null);

    /// <summary>Projects CtrlRAM display from an already-inspected base.</summary>
    CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
        string icId,
        string numberToken,
        FirmwareConfigMetadataSnapshot? baseFirmware);
}

/// <summary>Output-name projection from immutable inspections or an accepted AB session.</summary>
public interface ICompositionOutputNaming
{
    /// <summary>Resolves the compiled name from one exact accepted session without reopening inputs.</summary>
    CompositionOutputPreparation ResolveAcceptedOutput(
        ActiveSessionSnapshot acceptedSession,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null);

    /// <summary>Resolves one editable bundle default from the same accepted output-name facts and UTC instant.</summary>
    CompositionOutputBundleProposal ResolveAcceptedBundleProposal(
        ActiveSessionSnapshot acceptedSession,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null);

    /// <summary>Validates one edited prepared destination through the shared platform policy.</summary>
    CompositionOutputBundleDestinationValidation ValidateBundleDestination(
        CompositionOutputBundleIntent intent);

    /// <summary>Resolves one AB automatic output name and its compiled optional deliveries without execution.</summary>
    ValueTask<CompositionOutputPreparation> PrepareAutomaticOutputAsync(
        ActiveSessionSnapshot acceptedSession,
        CancellationToken cancellationToken);

}

/// <summary>Preview and Build execution from exact accepted Application sessions.</summary>
public interface ICompositionExecution
{
    /// <summary>Executes any accepted workflow through the single shared Application operation.</summary>
    ValueTask<CompositionRunResult> ExecuteAsync(
        AcceptedCompositionExecutionRequest request,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken);
}
