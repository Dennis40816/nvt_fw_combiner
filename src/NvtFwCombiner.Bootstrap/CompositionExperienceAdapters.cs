#pragma warning disable IDE0022 // Focused ports intentionally stay as concise forwarding adapters.
using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal sealed class CompositionCapabilityExperienceAdapter
    : ICompositionCapabilityExperience
{
    public string DefaultIcId => CanonicalCapabilityProjection.DefaultIcId;

    public IReadOnlyList<string> GetIcIds() => CanonicalCapabilityProjection.GetIcIds();

    public IReadOnlyList<CapabilityNumberChoice> GetNumberSelectionChoices(string icId) => CanonicalCapabilityProjection.GetNumberSelectionChoices(icId);

    public CapabilityCatalogSummary GetCatalogSummary() => CanonicalCapabilityProjection.GetCatalogSummary();

    public IReadOnlyList<CapabilityProfileSummary> GetAbMergeProfileSummaries() => CanonicalCapabilityProjection.GetAbMergeProfileSummaries();

    public CapabilityWorkflowReadiness GetReplaceWorkflowReadiness(
        string icId,
        string replaceMode) =>
        CanonicalCapabilityProjection.GetReplaceWorkflowReadiness(icId, replaceMode);

    public bool IsReplaceWorkflowAvailable(string icId, string replaceMode) => CanonicalCapabilityProjection.IsReplaceWorkflowAvailable(icId, replaceMode);

    public CapabilityFamilySummary GetIcFamilySummary(string icId) => CanonicalCapabilityProjection.GetIcFamilySummary(icId);

    public bool ArePerfectFamilyMembers(string firstIcId, string secondIcId) => CanonicalCapabilityProjection.ArePerfectFamilyMembers(firstIcId, secondIcId);

    public bool IsDpPerspectiveIc(string icId) => CanonicalCapabilityProjection.IsDpPerspectiveIc(icId);

    public bool HasBuiltInV2DpReplace(string icId) => CanonicalCapabilityProjection.HasBuiltInV2DpReplace(icId);
}

internal sealed class CompositionAuthoringExperienceAdapter
    : ICompositionAuthoringExperience
{
    public bool IsStandardMergeSupported(string icId) => CanonicalAuthoringAdapter.IsStandardMergeSupported(icId);

    public string? GetStandardMergeProfileId(string icId) => CanonicalAuthoringAdapter.GetStandardMergeProfileId(icId);

    public IReadOnlyList<string> GetStandardMergeRequiredAddressSpaces(string icId) => CanonicalAuthoringAdapter.GetStandardMergeRequiredAddressSpaces(icId);

    public IReadOnlyList<string> GetStandardMergeInputAddressSpaces(string icId) => CanonicalAuthoringAdapter.GetStandardMergeInputAddressSpaces(icId);

    public CompiledAuthoringSelectionSnapshot GetStandardMergeAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null) =>
        CanonicalAuthoringAdapter.GetStandardMergeAuthoringSnapshot(
            icId,
            selectedSlotIds,
            acceptedFileStamps,
            authoringRevision,
            retainedSession);

    public bool IsAbMergeAvailable(string icId) => CanonicalAuthoringAdapter.IsAbMergeAvailable(icId);

    public IReadOnlyList<CapabilityTopologyChoice> GetAbMergeTopologyChoices(string icId) => CanonicalAuthoringAdapter.GetAbMergeTopologyChoices(icId);

    public IReadOnlyList<WorkbenchAbMergeInputSlot> GetAbMergeInputSlots(
        string icId,
        string? topologyToken) =>
        CanonicalAuthoringAdapter.GetAbMergeInputSlots(icId, topologyToken);

    public CompiledAuthoringSelectionSnapshot GetAbMergeAuthoringSnapshot(
        string icId,
        string? topologyToken,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null) =>
        CanonicalAuthoringAdapter.GetAbMergeAuthoringSnapshot(
            icId,
            topologyToken,
            selectedSlotIds,
            acceptedFileStamps,
            authoringRevision,
            retainedSession);

    public CompiledAuthoringSelectionSnapshot GetDpReplaceAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null) =>
        CanonicalAuthoringAdapter.GetDpReplaceAuthoringSnapshot(
            icId,
            selectedSlotIds,
            acceptedFileStamps,
            authoringRevision,
            retainedSession);

    public IReadOnlyList<WorkbenchCtrlRamRegion> GetCtrlRamRegions(
        string icId,
        string number,
        string? basePath = null) =>
        CanonicalAuthoringAdapter.GetCtrlRamRegions(icId, number, basePath);

    public AuthoringMappingState CreateGeneralMergeAuthoringState(
        string mappingId,
        string filePath,
        string sourceStart,
        string targetStart,
        string length,
        int alignment = 1,
        string? reason = null,
        OperationProvenance? provenance = null,
        FileStamp? acceptedFileStamp = null) =>
        CanonicalAuthoringAdapter.CreateGeneralMergeAuthoringState(
            mappingId,
            filePath,
            sourceStart,
            targetStart,
            length,
            alignment,
            reason,
            provenance,
            acceptedFileStamp);

    public AuthoringMappingState CreateGeneralReplaceAuthoringState(
        string mappingId,
        GeneralMappingSourceKind sourceKind,
        string sourceValue,
        string targetStart,
        string length,
        FileStamp? acceptedFileStamp = null) =>
        CanonicalAuthoringAdapter.CreateGeneralReplaceAuthoringState(
            mappingId,
            sourceKind,
            sourceValue,
            targetStart,
            length,
            acceptedFileStamp);

    public bool TryCreateGeneralMergeAuthoringDraft(
        IReadOnlyList<AuthoringMappingState> states,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues) =>
        CanonicalAuthoringAdapter.TryCreateGeneralMergeAuthoringDraft(
            states,
            out draft,
            out issues);

    public bool TryCreateGeneralReplaceAuthoringDraft(
        IReadOnlyList<AuthoringMappingState> states,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues) =>
        CanonicalAuthoringAdapter.TryCreateGeneralReplaceAuthoringDraft(
            states,
            out draft,
            out issues);

    public GeneralAuthoringAdmissionResult GetGeneralMergeAuthoringAdmission(
        string icId,
        GeneralMergeDraftState draft) =>
        CanonicalAuthoringAdapter.GetGeneralMergeAuthoringAdmission(icId, draft);

    public GeneralAuthoringAdmissionResult? GetGeneralReplaceAuthoringAdmission(
        string icId,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft) =>
        CanonicalAuthoringAdapter.GetGeneralReplaceAuthoringAdmission(
            icId,
            referenceCapacity,
            mappingDraft);

    public ValueTask<GeneralSelectedFileLengthResult> ObserveGeneralSelectedFileLengthAsync(
        string mappingId,
        string selectedPath,
        CancellationToken cancellationToken) =>
        CanonicalAuthoringAdapter.ObserveGeneralSelectedFileLengthAsync(
            mappingId,
            selectedPath,
            cancellationToken);

    public ValueTask<GeneralSelectedFileInspectionResult> InspectGeneralSelectedFileAsync(
        string mappingId,
        string selectedPath,
        AuthoringRevision authoringRevision,
        long expectedLength,
        CancellationToken cancellationToken) =>
        CanonicalAuthoringAdapter.InspectGeneralSelectedFileAsync(
            mappingId,
            selectedPath,
            authoringRevision,
            expectedLength,
            cancellationToken);

    public string GetGeneralMergeDefaultOutputLength(string icId) => CanonicalAuthoringAdapter.GetGeneralMergeDefaultOutputLength(icId);

    public string GetGeneralMergeDefaultOutputFillByte(string icId) => CanonicalAuthoringAdapter.GetGeneralMergeDefaultOutputFillByte(icId);

    public string GetGeneralMergeDefaultOutputFileName(string icId) => CanonicalAuthoringAdapter.GetGeneralMergeDefaultOutputFileName(icId);

    public bool TryResolveGeneralMergeOutputInitializer(
        string? outputLength,
        string? outputFillByte,
        [NotNullWhen(true)] out WorkbenchGeneralMergeInitializer? initializer) =>
        CanonicalAuthoringAdapter.TryResolveGeneralMergeOutputInitializer(
            outputLength,
            outputFillByte,
            out initializer);

    public GeneralMergeDraftState CreateGeneralMergeDraft(
        WorkbenchGeneralMergeInitializer initializer,
        GeneralMappingDraftState mappings) =>
        CanonicalAuthoringAdapter.CreateGeneralMergeDraft(initializer, mappings);
}

internal sealed class CompositionAuthoringSessionPort
    : ICompositionAuthoringSession
{
    public CapabilityActionReadinessSnapshot? GetGeneralMergeActionReadiness(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft) =>
        CompositionAuthoringSessionAdapter.GetGeneralMergeActionReadiness(
            session,
            icId,
            draft);

    public bool PrepareGeneralMergeSelectionSession(
        AuthoringSessionState session,
        string icId,
        IEnumerable<string> mappingIds) =>
        CompositionAuthoringSessionAdapter.PrepareGeneralMergeSelectionSession(
            session,
            icId,
            mappingIds);

    public AuthoringSlotInspectionStartResult BeginGeneralMergeSelectedFileInspection(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft,
        string mappingId,
        long observedLength) =>
        CompositionAuthoringSessionAdapter.BeginGeneralMergeSelectedFileInspection(
            session,
            icId,
            draft,
            mappingId,
            observedLength);

    public ValueTask<CapabilityActionReadinessSnapshot?> GetGeneralReplaceActionReadinessAsync(
        AuthoringSessionState session,
        string icId,
        string number,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft,
        string referencePath,
        FileStamp acceptedReferenceStamp,
        WorkbenchFirmwareConfigMetadata? baseFirmware,
        CancellationToken cancellationToken) =>
        CompositionAuthoringSessionAdapter.GetGeneralReplaceActionReadinessAsync(
            session,
            icId,
            number,
            referenceCapacity,
            mappingDraft,
            referencePath,
            acceptedReferenceStamp,
            baseFirmware,
            cancellationToken);

    public bool PrepareGeneralReplaceSelectionSession(
        AuthoringSessionState session,
        string icId,
        long referenceCapacity,
        IEnumerable<string> mappingIds) =>
        CompositionAuthoringSessionAdapter.PrepareGeneralReplaceSelectionSession(
            session,
            icId,
            referenceCapacity,
            mappingIds);

    public AuthoringSlotInspectionStartResult BeginGeneralReplaceSelectedFileInspection(
        AuthoringSessionState session,
        string icId,
        string number,
        long referenceCapacity,
        GeneralMappingDraftState draft,
        string referencePath,
        FileStamp acceptedReferenceStamp,
        string mappingId,
        long observedLength) =>
        CompositionAuthoringSessionAdapter.BeginGeneralReplaceSelectedFileInspection(
            session,
            icId,
            number,
            referenceCapacity,
            draft,
            referencePath,
            acceptedReferenceStamp,
            mappingId,
            observedLength);

    public AuthoringCapabilityCatalogSnapshot? GetCtrlRamReplaceAuthoringCatalog(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot? retainedSession = null) =>
        CompositionAuthoringSessionAdapter.GetCtrlRamReplaceAuthoringCatalog(
            icId,
            number,
            slotPaths,
            retainedSession);

    public ValueTask<CapabilityActionReadinessSnapshot?> GetCtrlRamReplaceActionReadinessAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot acceptedSession,
        CancellationToken cancellationToken) =>
        CompositionAuthoringSessionAdapter.GetCtrlRamReplaceActionReadinessAsync(
            icId,
            number,
            slotPaths,
            acceptedSession,
            cancellationToken);

    public WorkbenchCtrlRamAuthoringTransitionResult TransitionCtrlRamFirmwareVersionCompilation(
        AuthoringSessionState session,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit) =>
        CompositionAuthoringSessionAdapter.TransitionCtrlRamFirmwareVersionCompilation(
            session,
            icId,
            number,
            slotPaths,
            firmwareVersionEdit);
}

internal sealed class CompositionMemoryPresentationAdapter
    : ICompositionMemoryPresentation
{
    public string? GetDpReplaceReferenceCapacityLabel(string icId) =>
        CompositionMemoryProjection.GetDpReplaceReferenceCapacityLabel(icId);

    public WorkbenchMemoryDisplay GetStandardMergeMemoryDisplay(
        string icId,
        long? dpInputLength) =>
        CompositionMemoryProjection.GetStandardMergeMemoryDisplay(icId, dpInputLength);

    public WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        string outputLength,
        string? outputFillByte) =>
        CompositionMemoryProjection.GetGeneralMergeMemoryDisplay(
            icId,
            outputLength,
            outputFillByte);

    public WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        WorkbenchGeneralMergeInitializer initializer,
        IReadOnlyList<AuthoringMappingState> states,
        GeneralAuthoringAdmissionResult? admission) =>
        CompositionMemoryProjection.GetGeneralMergeMemoryDisplay(
            icId,
            initializer,
            states,
            admission);

    public WorkbenchMemoryDisplay GetAbMergeMemoryDisplay(
        string icId,
        string? topologyToken,
        long? dpInputLength) =>
        CompositionMemoryProjection.GetAbMergeMemoryDisplay(
            icId,
            topologyToken,
            dpInputLength);

    public IReadOnlyList<WorkbenchReplaceInputSlot> GetReplaceInputSlots(
        string icId,
        string number,
        string replaceMode,
        string? basePath) =>
        CompositionMemoryProjection.GetReplaceInputSlots(
            icId,
            number,
            replaceMode,
            basePath);

    public WorkbenchMemoryDisplay GetReplaceMemoryDisplay(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength,
        string? ctrlRamBasePath) =>
        CompositionMemoryProjection.GetReplaceMemoryDisplay(
            icId,
            number,
            replaceMode,
            dpBaseLength,
            ctrlRamBasePath);

    public WorkbenchMemoryDisplay ApplyReplaceCoverageSelection(
        WorkbenchMemoryDisplay display,
        IEnumerable<string> selectedRegionIds) =>
        CompositionMemoryProjection.ApplyReplaceCoverageSelection(
            display,
            selectedRegionIds);

    public WorkbenchMemoryDisplay GetGeneralReplaceMemoryDisplay(
        long referenceCapacity,
        GeneralAuthoringAdmissionResult admission) =>
        CompositionMemoryProjection.GetGeneralReplaceMemoryDisplay(
            referenceCapacity,
            admission);

    public WorkbenchMemoryDisplay GetGeneralReplaceMemoryDisplay(
        long referenceCapacity,
        IReadOnlyList<AuthoringMappingState> authoringStates) =>
        CompositionMemoryProjection.GetGeneralReplaceMemoryDisplay(
            referenceCapacity,
            authoringStates);
}

internal sealed class FirmwareInspectionPort : IFirmwareInspection
{
    public WorkbenchFirmwareConfigMetadata? TryReadFirmwareConfigMetadata(
        string icId,
        string path) =>
        FirmwareInspectionAdapter.TryReadFirmwareConfigMetadata(icId, path);

    public IReadOnlyList<WorkbenchFirmwareInspectionResult> InspectFirmwareBatch(
        string icId,
        IReadOnlyList<WorkbenchFirmwareInspectionInput> inputs) =>
        FirmwareInspectionAdapter.InspectFirmwareBatch(icId, inputs);

    public WorkbenchCtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
        string icId,
        string numberToken,
        WorkbenchFirmwareConfigMetadata? baseFirmware) =>
        FirmwareInspectionAdapter.ProjectCtrlRamInspectionDisplay(
            icId,
            numberToken,
            baseFirmware);
}

internal sealed class CompositionOutputNamingAdapter : ICompositionOutputNaming
{
    public ValueTask<string> ResolveAutomaticOutputFileNameAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        CancellationToken cancellationToken,
        string? abMergeTopologyToken = null,
        ActiveSessionSnapshot? acceptedSession = null) =>
        CompositionOutputNaming.ResolveAutomaticOutputFileNameAsync(
            icId,
            slotPaths,
            cancellationToken,
            abMergeTopologyToken,
            acceptedSession);

    public WorkbenchOutputFileNameSuggestion CreateFlashCodeOutputFileNameFromInspections(
        string icId,
        IReadOnlyList<WorkbenchOutputNameInspectionCandidate> candidates,
        DateOnly? effectiveDate = null) =>
        CompositionOutputNaming.CreateFlashCodeOutputFileNameFromInspections(
            icId,
            candidates,
            effectiveDate);

    public WorkbenchOutputFileNameSuggestion CreateFlashCodeOutputFileNameFromInspections(
        string icId,
        IReadOnlyList<WorkbenchOutputNameInspectionCandidate> candidates,
        WorkbenchCtrlRamFirmwareVersionEdit firmwareVersionEdit,
        DateOnly? effectiveDate = null) =>
        CompositionOutputNaming.CreateFlashCodeOutputFileNameFromInspections(
            icId,
            candidates,
            firmwareVersionEdit,
            effectiveDate);

    public WorkbenchOutputFileNameSuggestion CreateCtrlRamReplaceOutputFileNameFromInspections(
        string icId,
        IReadOnlyList<WorkbenchOutputNameInspectionCandidate> candidates,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit = null,
        DateOnly? effectiveDate = null) =>
        CompositionOutputNaming.CreateCtrlRamReplaceOutputFileNameFromInspections(
            icId,
            candidates,
            firmwareVersionEdit,
            effectiveDate);
}


