using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns shared workflow-context and selected-firmware prompt presentation.</summary>
public sealed partial class WorkflowSessionPresentationViewModel : ObservableObject
{
    private readonly PresentationCompositionServices _compositionServices;
    private readonly Action<WorkflowContextSelection> _applyWorkflowContext;
    private readonly MergePresentationViewModel _merge;
    private readonly ReplacePresentationViewModel _replace;
    private readonly Action<string, string> _showToast;
    private readonly WorkflowSessionStateBindings _stateBindings;
    private readonly Func<ShellTextResources> _textProvider;

    internal WorkflowSessionPresentationViewModel(
        PresentationCompositionServices compositionServices,
        Func<ShellTextResources> textProvider,
        MergePresentationViewModel merge,
        ReplacePresentationViewModel replace,
        Action<WorkflowContextSelection> applyWorkflowContext,
        Action<string, string> showToast,
        Func<
            string,
            IReadOnlyList<FirmwareInspectionSnapshotInput>,
            IReadOnlyList<FirmwareInspectionSnapshotResult>> firmwareInspectionReader,
        WorkflowSessionStateBindings stateBindings)
    {
        _compositionServices = compositionServices ??
            throw new ArgumentNullException(nameof(compositionServices));
        AbMergeIcChoices = Array.AsReadOnly(
        [
            .. _compositionServices.Capabilities.GetAbMergeProfileSummaries()
                .Select(static profile => profile.IcId),
        ]);
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        _merge = merge ?? throw new ArgumentNullException(nameof(merge));
        _replace = replace ?? throw new ArgumentNullException(nameof(replace));
        _applyWorkflowContext = applyWorkflowContext ?? throw new ArgumentNullException(nameof(applyWorkflowContext));
        _showToast = showToast ?? throw new ArgumentNullException(nameof(showToast));
        ArgumentNullException.ThrowIfNull(firmwareInspectionReader);
        _stateBindings = stateBindings ?? throw new ArgumentNullException(nameof(stateBindings));
        InspectionSession = new FirmwareInspectionSession(firmwareInspectionReader);
        WorkflowContextSetup = new WorkflowContextSetupViewModel(_compositionServices);
        ConfirmWorkflowContextCommand = new RelayCommand(ConfirmWorkflowContext);
        CancelWorkflowContextCommand = new RelayCommand(CancelWorkflowContext);
        AcceptFirmwareIcMismatchCommand = new RelayCommand(AcceptFirmwareIcMismatch);
        DismissFirmwareIcMismatchCommand = new RelayCommand(DismissFirmwareIcMismatch);
        AcceptFirmwareNumberMismatchCommand = new RelayCommand(AcceptFirmwareNumberMismatch);
        DismissFirmwareNumberMismatchCommand = new RelayCommand(DismissFirmwareNumberMismatch);
        _selectedIc = _compositionServices.Capabilities.DefaultIcId;
        _replaceWorkflowContextIc = SelectedIc;
    }

    /// <summary>Gets current localized shell text used by workflow-session prompts.</summary>
    public ShellTextResources Text => _textProvider();

    internal FirmwareInspectionSession InspectionSession { get; }

    internal bool IsApplyingFirmwareInspectionContext { get; set; }

    internal bool IsRefreshingFirmwareInspectionContext { get; set; }

    private bool IsCtrlRamReplaceModeSelected => _replace.IsCtrlRamReplaceModeSelected;

    private bool IsReplaceVisible =>
        _stateBindings.SelectedPage() == ShellPage.Replace &&
        string.Equals(_replace.SelectedReplaceMode, ExperienceIds.DpReplace, StringComparison.Ordinal);

    private bool IsAbCodeMergeModeSelected => _merge.IsAbCodeMergeModeSelected;

    private bool IsStandardMergeModeSelected => _merge.IsNormalMergeModeSelected;

    private string SelectedMergeMode => _merge.SelectedMergeMode;

    private string SelectedReplaceMode => _replace.SelectedReplaceMode;

    private FirmwareSlotViewModel MergeDpSlot => _merge.MergeDpSlot;

    private FirmwareSlotViewModel MergeTpSlot => _merge.MergeTpSlot;

    private FirmwareSlotViewModel ReplaceBaseSlot => _replace.ReplaceBaseSlot;

    private IEnumerable<FirmwareSlotViewModel> MergeSlots => _merge.MergeSlots;

    private IEnumerable<FirmwareSlotViewModel> ReplaceSlots => _replace.ReplaceSlots;

    private IEnumerable<FirmwareSlotViewModel> AbMergeSlots => _merge.AbMergeSlots;

    private IReadOnlyDictionary<string, string> AbMergeAddressSpaceBySlotId =>
        _merge.AbMergeAddressSpaceBySlotId;

    private string? GetSelectedAbMergeTopologyToken()
    {
        return _merge.GetSelectedAbMergeTopologyToken();
    }

    private void ApplyCtrlRamInspectionDisplay(CtrlRamInspectionDisplay display)
    {
        _replace.ApplyCtrlRamInspectionDisplay(display);
    }

    private void RefreshMergeMemoryMapState()
    {
        _merge.RefreshMergeMemoryMapState();
    }

    private void RefreshReplaceMemoryMapState()
    {
        _replace.RefreshReplaceMemoryMapState();
    }

    private void RefreshCommandState()
    {
        _stateBindings.RefreshCommandState();
    }

    internal void ApplyLanguageChanged()
    {
        DeviceContextRefreshSummary = Text.DeviceContextStatus;
        RelocalizeFirmwareFacts();
        RelocalizeInputInspection();
        OnPropertyChanged(nameof(Text));
        NotifyContextTextChanged();
    }

    private void RelocalizeFirmwareFacts()
    {
        foreach (FirmwareSlotViewModel slot in _merge.MergeSlots
                     .Concat(_replace.ReplaceSlots)
                     .Append(_replace.ReplaceBaseSlot)
                     .Distinct())
        {
            if (!FirmwareInspectionRequestFactory.SupportsFacts(slot) ||
                !InspectionSession.TryGetInspection(
                    slot.SlotId,
                    slot.FilePath,
                    out FirmwareInspectionSnapshot inspection))
            {
                continue;
            }

            if (inspection.AbMergeFacts is not null)
            {
                FirmwareInspectionProjection.ApplyAbInputFacts(slot, inspection, Text);
            }
            else
            {
                slot.RelocalizeFirmwareFacts(slot.SlotKind == FirmwareSlotKind.Dp
                    ? UiCompositionRunner.GetDpFirmwareSlotFacts(inspection, Text)
                    : UiCompositionRunner.GetFirmwareSlotFacts(
                        inspection,
                        includeBaseFacts: slot.SlotKind == FirmwareSlotKind.Base,
                        text: Text));
            }
        }
    }

    private void RelocalizeInputInspection()
    {
        foreach (FirmwareSlotViewModel slot in _merge.MergeSlots
                     .Concat(_merge.AbMergeSlots)
                     .Concat(_replace.ReplaceSlots)
                     .Concat([_replace.ReplaceBaseSlot])
                     .Distinct())
        {
            if (!InspectionSession.TryGetInspection(
                    slot.SlotId,
                    slot.FilePath,
                    out FirmwareInspectionSnapshot projected))
            {
                continue;
            }

            if (projected.InputSlotStatus is { } status)
            {
                FirmwareInspectionProjection.ApplyInputSlotInspection(slot, status, Text);
            }
        }
    }

    internal sealed record WorkflowContextSelection(
        ShellPage Page,
        string Mode,
        bool ShowNumber,
        string IcId,
        string Number);

    internal sealed record AcceptedFirmwareMismatchSelection(string SlotId, string Path);
}
