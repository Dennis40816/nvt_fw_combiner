using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns shared workflow-context and selected-firmware prompt presentation.</summary>
public sealed partial class WorkflowSessionPresentationViewModel : ObservableObject
{
    private readonly Action<WorkflowContextSelection> _applyWorkflowContext;
    private readonly Action<string> _applyDetectedNumber;
    private readonly Func<IReadOnlyList<string>> _icChoices;
    private readonly Func<bool> _isReplaceVisible;
    private readonly Func<IReadOnlyList<IcNumberChoiceViewModel>> _numberChoices;
    private readonly Func<string> _selectedIc;
    private readonly Func<string> _selectedNumber;
    private readonly Action<string> _setSelectedIc;
    private readonly Action<string, string> _showToast;
    private readonly Func<ShellTextResources> _textProvider;
    private readonly WorkflowInspectionBindings _inspectionBindings;

    internal WorkflowSessionPresentationViewModel(
        Func<ShellTextResources> textProvider,
        Func<string> selectedIc,
        Func<string> selectedNumber,
        Func<IReadOnlyList<string>> icChoices,
        Func<IReadOnlyList<IcNumberChoiceViewModel>> numberChoices,
        Func<bool> isReplaceVisible,
        Action<WorkflowContextSelection> applyWorkflowContext,
        Action<string> setSelectedIc,
        Action<string> applyDetectedNumber,
        Action<string, string> showToast,
        Func<
            string,
            IReadOnlyList<WorkbenchFirmwareInspectionInput>,
            IReadOnlyList<WorkbenchFirmwareInspectionResult>> firmwareInspectionReader,
        WorkflowInspectionBindings inspectionBindings)
    {
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        _selectedIc = selectedIc ?? throw new ArgumentNullException(nameof(selectedIc));
        _selectedNumber = selectedNumber ?? throw new ArgumentNullException(nameof(selectedNumber));
        _icChoices = icChoices ?? throw new ArgumentNullException(nameof(icChoices));
        _numberChoices = numberChoices ?? throw new ArgumentNullException(nameof(numberChoices));
        _isReplaceVisible = isReplaceVisible ?? throw new ArgumentNullException(nameof(isReplaceVisible));
        _applyWorkflowContext = applyWorkflowContext ?? throw new ArgumentNullException(nameof(applyWorkflowContext));
        _setSelectedIc = setSelectedIc ?? throw new ArgumentNullException(nameof(setSelectedIc));
        _applyDetectedNumber = applyDetectedNumber ?? throw new ArgumentNullException(nameof(applyDetectedNumber));
        _showToast = showToast ?? throw new ArgumentNullException(nameof(showToast));
        ArgumentNullException.ThrowIfNull(firmwareInspectionReader);
        _inspectionBindings = inspectionBindings ?? throw new ArgumentNullException(nameof(inspectionBindings));
        InspectionSession = new FirmwareInspectionSession(firmwareInspectionReader);
        ConfirmWorkflowContextCommand = new RelayCommand(ConfirmWorkflowContext);
        CancelWorkflowContextCommand = new RelayCommand(CancelWorkflowContext);
        AcceptFirmwareIcMismatchCommand = new RelayCommand(AcceptFirmwareIcMismatch);
        DismissFirmwareIcMismatchCommand = new RelayCommand(DismissFirmwareIcMismatch);
        AcceptFirmwareNumberMismatchCommand = new RelayCommand(AcceptFirmwareNumberMismatch);
        DismissFirmwareNumberMismatchCommand = new RelayCommand(DismissFirmwareNumberMismatch);
    }

    /// <summary>Gets current localized shell text used by workflow-session prompts.</summary>
    public ShellTextResources Text => _textProvider();

    internal FirmwareInspectionSession InspectionSession { get; }

    internal bool IsApplyingFirmwareInspectionContext { get; set; }

    internal bool IsRefreshingFirmwareInspectionContext { get; set; }

    private string SelectedIc => _selectedIc();

    private string SelectedNumber => _selectedNumber();

    private bool IsCtrlRamReplaceModeSelected => _inspectionBindings.IsCtrlRamReplaceModeSelected();

    private bool IsReplaceVisible => _inspectionBindings.IsDpReplaceContext();

    private bool IsNumberSelectorVisible => _inspectionBindings.IsNumberSelectorVisible();

    private bool IsAbCodeMergeModeSelected => _inspectionBindings.IsAbCodeMergeModeSelected();

    private string SelectedMergeMode => _inspectionBindings.SelectedMergeMode();

    private string SelectedReplaceMode => _inspectionBindings.SelectedReplaceMode();

    private FirmwareSlotViewModel MergeDpSlot => _inspectionBindings.MergeDpSlot();

    private FirmwareSlotViewModel MergeTpSlot => _inspectionBindings.MergeTpSlot();

    private FirmwareSlotViewModel ReplaceBaseSlot => _inspectionBindings.ReplaceBaseSlot();

    private IEnumerable<FirmwareSlotViewModel> MergeSlots => _inspectionBindings.MergeSlots();

    private IEnumerable<FirmwareSlotViewModel> ReplaceSlots => _inspectionBindings.ReplaceSlots();

    private IEnumerable<FirmwareSlotViewModel> AbMergeSlots => _inspectionBindings.AbMergeSlots();

    private IReadOnlyDictionary<string, string> AbMergeAddressSpaceBySlotId =>
        _inspectionBindings.AbMergeAddressSpaceBySlotId();

    private string? GetSelectedAbMergeTopologyToken()
    {
        return _inspectionBindings.SelectedAbMergeTopologyToken();
    }

    private FirmwareSlotViewModel? SelectSlotFile(string slotId, string path)
    {
        return _inspectionBindings.SelectSlotFile(slotId, path);
    }

    private FirmwareSlotViewModel? FindSlot(string slotId)
    {
        return _inspectionBindings.FindSlot(slotId);
    }

    private void ApplyCtrlRamInspectionDisplay(WorkbenchCtrlRamInspectionDisplay display)
    {
        _inspectionBindings.ApplyCtrlRamDisplay(display);
    }

    private void RefreshMergeMemoryMapState()
    {
        _inspectionBindings.RefreshMergeMemoryMap();
    }

    private void RefreshReplaceMemoryMapState()
    {
        _inspectionBindings.RefreshReplaceMemoryMap();
    }

    private void RefreshCommandState()
    {
        _inspectionBindings.RefreshCommandState();
    }

    private void NotifySlotFileOutputNames()
    {
        _inspectionBindings.NotifySlotFileOutputNames();
    }

    internal void ApplyLanguageChanged()
    {
        OnPropertyChanged(nameof(Text));
    }

    internal sealed record WorkflowContextSelection(
        ShellPage Page,
        string Mode,
        bool ShowNumber,
        string IcId,
        string Number);

    internal sealed record AcceptedFirmwareMismatchSelection(string SlotId, string Path);
}
