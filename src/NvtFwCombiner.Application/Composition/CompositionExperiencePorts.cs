using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

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

    /// <summary>Gets authorable AB Merge profiles.</summary>
    IReadOnlyList<CapabilityProfileSummary> GetAbMergeProfileSummaries();

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

    /// <summary>Returns whether the IC owns one executable DP Replace capability.</summary>
    bool HasBuiltInV2DpReplace(string icId);
}

/// <summary>Typed authoring operations shared by desktop and command-line clients.</summary>
public interface ICompositionAuthoringExperience
{
    /// <summary>Returns whether Standard Merge is authorable.</summary>
    bool IsStandardMergeSupported(string icId);

    /// <summary>Gets the Standard Merge profile identifier.</summary>
    string? GetStandardMergeProfileId(string icId);

    /// <summary>Gets required Standard Merge input spaces.</summary>
    IReadOnlyList<string> GetStandardMergeRequiredAddressSpaces(string icId);

    /// <summary>Gets all selectable Standard Merge input spaces.</summary>
    IReadOnlyList<string> GetStandardMergeInputAddressSpaces(string icId);

    /// <summary>Projects Standard Merge selection readiness.</summary>
    CompiledAuthoringSelectionSnapshot GetStandardMergeAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null);

    /// <summary>Returns whether AB Merge is authorable.</summary>
    bool IsAbMergeAvailable(string icId);

    /// <summary>Gets AB topology choices from compiled capability facts.</summary>
    IReadOnlyList<CapabilityTopologyChoice> GetAbMergeTopologyChoices(string icId);

    /// <summary>Gets AB input slots for one topology token.</summary>
    IReadOnlyList<WorkbenchAbMergeInputSlot> GetAbMergeInputSlots(
        string icId,
        string? topologyToken);

    /// <summary>Projects AB Merge selection readiness.</summary>
    CompiledAuthoringSelectionSnapshot GetAbMergeAuthoringSnapshot(
        string icId,
        string? topologyToken,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null);

    /// <summary>Projects DP Replace selection readiness.</summary>
    CompiledAuthoringSelectionSnapshot GetDpReplaceAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null);

    /// <summary>Gets visible CtrlRAM regions.</summary>
    IReadOnlyList<WorkbenchCtrlRamRegion> GetCtrlRamRegions(
        string icId,
        string number,
        string? basePath = null);

    /// <summary>Creates one parsed General Merge authoring row.</summary>
    AuthoringMappingState CreateGeneralMergeAuthoringState(
        string mappingId,
        string filePath,
        string sourceStart,
        string targetStart,
        string length,
        int alignment = 1,
        string? reason = null,
        OperationProvenance? provenance = null,
        FileStamp? acceptedFileStamp = null);

    /// <summary>Creates one parsed General Replace authoring row.</summary>
    AuthoringMappingState CreateGeneralReplaceAuthoringState(
        string mappingId,
        GeneralMappingSourceKind sourceKind,
        string sourceValue,
        string targetStart,
        string length,
        FileStamp? acceptedFileStamp = null);

    /// <summary>Creates a typed General Merge mapping draft.</summary>
    bool TryCreateGeneralMergeAuthoringDraft(
        IReadOnlyList<AuthoringMappingState> states,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues);

    /// <summary>Creates a typed General Replace mapping draft.</summary>
    bool TryCreateGeneralReplaceAuthoringDraft(
        IReadOnlyList<AuthoringMappingState> states,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues);

    /// <summary>Gets General Merge admission from the same Application use case as execution.</summary>
    GeneralAuthoringAdmissionResult GetGeneralMergeAuthoringAdmission(
        string icId,
        GeneralMergeDraftState draft);

    /// <summary>Gets General Replace admission from the same Application use case as execution.</summary>
    GeneralAuthoringAdmissionResult? GetGeneralReplaceAuthoringAdmission(
        string icId,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft);

    /// <summary>Observes one selected General file length.</summary>
    ValueTask<GeneralSelectedFileLengthResult> ObserveGeneralSelectedFileLengthAsync(
        string mappingId,
        string selectedPath,
        CancellationToken cancellationToken);

    /// <summary>Inspects one selected General file against its exact length.</summary>
    ValueTask<GeneralSelectedFileInspectionResult> InspectGeneralSelectedFileAsync(
        string mappingId,
        string selectedPath,
        AuthoringRevision authoringRevision,
        long expectedLength,
        CancellationToken cancellationToken);

    /// <summary>Gets the General Merge default output length text.</summary>
    string GetGeneralMergeDefaultOutputLength(string icId);

    /// <summary>Gets the General Merge default fill-byte text.</summary>
    string GetGeneralMergeDefaultOutputFillByte(string icId);

    /// <summary>Gets the General Merge default output filename.</summary>
    string GetGeneralMergeDefaultOutputFileName(string icId);

    /// <summary>Resolves editable initializer text.</summary>
    bool TryResolveGeneralMergeOutputInitializer(
        string? outputLength,
        string? outputFillByte,
        [NotNullWhen(true)] out WorkbenchGeneralMergeInitializer? initializer);

    /// <summary>Creates one typed General Merge draft.</summary>
    GeneralMergeDraftState CreateGeneralMergeDraft(
        WorkbenchGeneralMergeInitializer initializer,
        GeneralMappingDraftState mappings);
}

/// <summary>Authoring-session lifecycle and per-slot readiness operations.</summary>
public interface ICompositionAuthoringSession
{
    /// <summary>Gets General Merge action readiness.</summary>
    CapabilityActionReadinessSnapshot? GetGeneralMergeActionReadiness(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft);

    /// <summary>Publishes General Merge mapping membership.</summary>
    bool PrepareGeneralMergeSelectionSession(
        AuthoringSessionState session,
        string icId,
        IEnumerable<string> mappingIds);

    /// <summary>Begins exact General Merge file inspection.</summary>
    AuthoringSlotInspectionStartResult BeginGeneralMergeSelectedFileInspection(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft,
        string mappingId,
        long observedLength);

    /// <summary>Gets General Replace action readiness.</summary>
    ValueTask<CapabilityActionReadinessSnapshot?> GetGeneralReplaceActionReadinessAsync(
        AuthoringSessionState session,
        string icId,
        string number,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft,
        string referencePath,
        FileStamp acceptedReferenceStamp,
        WorkbenchFirmwareConfigMetadata? baseFirmware,
        CancellationToken cancellationToken);

    /// <summary>Publishes General Replace mapping membership.</summary>
    bool PrepareGeneralReplaceSelectionSession(
        AuthoringSessionState session,
        string icId,
        long referenceCapacity,
        IEnumerable<string> mappingIds);

    /// <summary>Begins exact General Replace file inspection.</summary>
    AuthoringSlotInspectionStartResult BeginGeneralReplaceSelectedFileInspection(
        AuthoringSessionState session,
        string icId,
        string number,
        long referenceCapacity,
        GeneralMappingDraftState draft,
        string referencePath,
        FileStamp acceptedReferenceStamp,
        string mappingId,
        long observedLength);

    /// <summary>Gets CtrlRAM authoring catalog for selected paths.</summary>
    AuthoringCapabilityCatalogSnapshot? GetCtrlRamReplaceAuthoringCatalog(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot? retainedSession = null);

    /// <summary>Gets CtrlRAM action readiness.</summary>
    ValueTask<CapabilityActionReadinessSnapshot?> GetCtrlRamReplaceActionReadinessAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        CancellationToken cancellationToken);

    /// <summary>Transitions CtrlRAM firmware-version authoring.</summary>
    WorkbenchCtrlRamAuthoringTransitionResult TransitionCtrlRamFirmwareVersionCompilation(
        AuthoringSessionState session,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit);
}

/// <summary>Read-only memory and input-slot projections.</summary>
public interface ICompositionMemoryPresentation
{
    /// <summary>Gets current DP Replace Reference capacity disclosure.</summary>
    string? GetDpReplaceReferenceCapacityLabel(string icId);

    /// <summary>Gets Standard Merge memory display.</summary>
    WorkbenchMemoryDisplay GetStandardMergeMemoryDisplay(string icId, long? dpInputLength);

    /// <summary>Gets parsed General Merge memory display.</summary>
    WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        string outputLength,
        string? outputFillByte);

    /// <summary>Gets parsed General Merge memory display.</summary>
    WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        WorkbenchGeneralMergeInitializer initializer,
        IReadOnlyList<AuthoringMappingState> states,
        GeneralAuthoringAdmissionResult? admission);

    /// <summary>Gets AB Merge memory display.</summary>
    WorkbenchMemoryDisplay GetAbMergeMemoryDisplay(
        string icId,
        string? topologyToken,
        long? dpInputLength);

    /// <summary>Gets Replace input slots.</summary>
    IReadOnlyList<WorkbenchReplaceInputSlot> GetReplaceInputSlots(
        string icId,
        string number,
        string replaceMode,
        string? basePath);

    /// <summary>Gets Replace memory display.</summary>
    WorkbenchMemoryDisplay GetReplaceMemoryDisplay(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength,
        string? ctrlRamBasePath);

    /// <summary>Applies selected CtrlRAM regions to an existing display.</summary>
    WorkbenchMemoryDisplay ApplyReplaceCoverageSelection(
        WorkbenchMemoryDisplay display,
        IEnumerable<string> selectedRegionIds);

    /// <summary>Gets invalid General Replace authoring display.</summary>
    WorkbenchMemoryDisplay GetGeneralReplaceMemoryDisplay(
        long referenceCapacity,
        GeneralAuthoringAdmissionResult admission);

    /// <summary>Gets invalid General Replace authoring display.</summary>
    WorkbenchMemoryDisplay GetGeneralReplaceMemoryDisplay(
        long referenceCapacity,
        IReadOnlyList<AuthoringMappingState> authoringStates);
}

/// <summary>Immutable firmware inspection operations.</summary>
public interface IFirmwareInspection
{
    /// <summary>Reads FWConfig metadata when the selected image declares it.</summary>
    WorkbenchFirmwareConfigMetadata? TryReadFirmwareConfigMetadata(string icId, string path);

    /// <summary>Inspects a distinct-path batch once.</summary>
    IReadOnlyList<WorkbenchFirmwareInspectionResult> InspectFirmwareBatch(
        string icId,
        IReadOnlyList<WorkbenchFirmwareInspectionInput> inputs);

    /// <summary>Projects CtrlRAM display from an already-inspected base.</summary>
    WorkbenchCtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
        string icId,
        string numberToken,
        WorkbenchFirmwareConfigMetadata? baseFirmware);
}

/// <summary>Output-name projection from immutable inspections or an accepted AB session.</summary>
public interface ICompositionOutputNaming
{
    /// <summary>Resolves an AB automatic output filename without execution.</summary>
    ValueTask<string> ResolveAutomaticOutputFileNameAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        CancellationToken cancellationToken,
        string? abMergeTopologyToken = null,
        ActiveSessionSnapshot? acceptedSession = null);

    /// <summary>Creates a FlashCode name with an edited CtrlRAM TP version.</summary>
    WorkbenchOutputFileNameSuggestion CreateFlashCodeOutputFileNameFromInspections(
        string icId,
        IReadOnlyList<WorkbenchOutputNameInspectionCandidate> candidates,
        DateOnly? effectiveDate = null);

    /// <summary>Creates a FlashCode name with an edited CtrlRAM TP version.</summary>
    WorkbenchOutputFileNameSuggestion CreateFlashCodeOutputFileNameFromInspections(
        string icId,
        IReadOnlyList<WorkbenchOutputNameInspectionCandidate> candidates,
        WorkbenchCtrlRamFirmwareVersionEdit firmwareVersionEdit,
        DateOnly? effectiveDate = null);

    /// <summary>Creates a CtrlRAM Replace name from immutable inspections.</summary>
    WorkbenchOutputFileNameSuggestion CreateCtrlRamReplaceOutputFileNameFromInspections(
        string icId,
        IReadOnlyList<WorkbenchOutputNameInspectionCandidate> candidates,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit = null,
        DateOnly? effectiveDate = null);
}

/// <summary>Read-only planning for optional AB delivery artifacts.</summary>
public interface IAbMergeDeliveryPlanning
{
    /// <summary>Creates an optional A-bank delivery plan without execution.</summary>
    ValueTask<WorkbenchAbAFlashCodeDeliveryPlan?> TryCreateAFlashCodeDeliveryPlanAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        CancellationToken cancellationToken,
        string? abMergeTopologyToken = null,
        ActiveSessionSnapshot? acceptedSession = null);
}

/// <summary>Preview and Build execution from exact accepted Application sessions.</summary>
public interface ICompositionExecution
{
    /// <summary>Runs Standard Merge from an exact accepted session.</summary>
    ValueTask<WorkbenchRunResult> RunStandardMergeAcceptedSessionWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null);

    /// <summary>Runs General Merge from an exact accepted session.</summary>
    ValueTask<WorkbenchRunResult> RunGeneralMergeAcceptedSessionWithProgressAsync(
        string icId,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null);

    /// <summary>Runs AB Merge from an exact accepted session.</summary>
    ValueTask<WorkbenchRunResult> RunAbMergeAcceptedSessionWithProgressAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        string? abMergeTopologyToken = null,
        string? aFlashCodeOutputPath = null,
        bool outputPathUsesAutomaticName = false,
        bool aFlashCodeOutputPathUsesAutomaticName = false);

    /// <summary>Previews General Replace from an exact accepted session.</summary>
    ValueTask<WorkbenchRunResult> PreviewGeneralReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken);

    /// <summary>Builds General Replace from an exact accepted session.</summary>
    ValueTask<WorkbenchRunResult> BuildGeneralReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        CompositionRunProgressFeed progress,
        string? outputPath,
        CancellationToken cancellationToken);

    /// <summary>Runs DP or CtrlRAM Replace from an exact accepted session.</summary>
    ValueTask<WorkbenchRunResult> RunReplaceAcceptedSessionWithProgressAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null);

    /// <summary>Creates typed diagnostics for a rejected Replace attempt.</summary>
    WorkbenchRunResult CreateRejectedReplaceAttemptResult(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<CompositionIssue> authoringIssues,
        bool build);
}

