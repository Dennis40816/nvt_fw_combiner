using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns shared workflow-context and selected-firmware prompt presentation.</summary>
internal sealed partial class WorkflowSessionPresentationViewModel : ObservableObject
{
    private readonly PresentationCompositionServices _compositionServices;
    private readonly Action<WorkflowContextSelection> _applyWorkflowContext;
    private readonly MergePresentationViewModel _merge;
    private readonly ReplacePresentationViewModel _replace;
    private readonly Action<string, string> _showToast;
    private readonly Action<SystemActivityDraft> _recordActivity;
    private readonly WorkflowSessionStateBindings _stateBindings;
    private readonly Func<ShellTextResources> _textProvider;

    internal WorkflowSessionPresentationViewModel(
        PresentationCompositionServices compositionServices,
        Func<ShellTextResources> textProvider,
        MergePresentationViewModel merge,
        ReplacePresentationViewModel replace,
        Action<WorkflowContextSelection> applyWorkflowContext,
        Action<string, string> showToast,
        Action<SystemActivityDraft> recordActivity,
        WorkflowSessionStateBindings stateBindings)
    {
        _compositionServices = compositionServices ??
            throw new ArgumentNullException(nameof(compositionServices));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        _merge = merge ?? throw new ArgumentNullException(nameof(merge));
        _replace = replace ?? throw new ArgumentNullException(nameof(replace));
        _applyWorkflowContext = applyWorkflowContext ?? throw new ArgumentNullException(nameof(applyWorkflowContext));
        _showToast = showToast ?? throw new ArgumentNullException(nameof(showToast));
        _recordActivity = recordActivity ?? throw new ArgumentNullException(nameof(recordActivity));
        _stateBindings = stateBindings ?? throw new ArgumentNullException(nameof(stateBindings));
        WorkflowContextSetup = new WorkflowContextSetupViewModel(_compositionServices);
        ConfirmWorkflowContextCommand = new RelayCommand(ConfirmWorkflowContext);
        CancelWorkflowContextCommand = new RelayCommand(CancelWorkflowContext);
        AcceptFirmwareIcMismatchCommand = new RelayCommand(AcceptFirmwareIcMismatch);
        DismissFirmwareIcMismatchCommand = new RelayCommand(DismissFirmwareIcMismatch);
        AcceptFirmwareNumberMismatchCommand = new RelayCommand(AcceptFirmwareNumberMismatch);
        DismissFirmwareNumberMismatchCommand = new RelayCommand(DismissFirmwareNumberMismatch);
        ClearSlotFileCommand = new AsyncRelayCommand<string>(ClearSlotFileFromCommandAsync);
    }

    public ShellTextResources Text => _textProvider();

    public IAsyncRelayCommand<string> ClearSlotFileCommand { get; }

    internal bool IsApplyingFirmwareInspectionContext { get; set; }

    internal bool IsRefreshingFirmwareInspectionContext { get; set; }

    private WorkflowInspectionContext? ActiveInspectionContext =>
        _stateBindings.SelectedPage() switch
        {
            ShellPage.Merge => InspectionContext(WorkflowInspectionOwner.Merge),
            ShellPage.Replace => InspectionContext(WorkflowInspectionOwner.Replace),
            ShellPage.Home or ShellPage.HexEditor => null,
            _ => throw new InvalidOperationException("Unknown shell page."),
        };

    private WorkflowInspectionContext InspectionContext(WorkflowInspectionOwner owner)
    {
        return new(owner, owner == WorkflowInspectionOwner.Merge
            ? _merge.SelectedMergeMode
            : _replace.SelectedReplaceMode);
    }

    private FirmwareSlotViewModel MergeDpSlot => _merge.MergeDpSlot;

    private FirmwareSlotViewModel MergeTpSlot => _merge.MergeTpSlot;

    private FirmwareSlotViewModel ReplaceBaseSlot => _replace.ReplaceBaseSlot;

    private IEnumerable<FirmwareSlotViewModel> MergeSlots => _merge.MergeSlots;

    private IEnumerable<FirmwareSlotViewModel> ReplaceSlots => _replace.ReplaceSlots;

    private IEnumerable<FirmwareSlotViewModel> AbMergeSlots => _merge.AbMergeSlots;

    private IReadOnlyDictionary<string, string> AbMergeAddressSpaceBySlotId =>
        _merge.AbMergeAddressSpaceBySlotId;

    internal void ApplyLanguageChanged()
    {
        DeviceContextRefreshSummary = Text.DeviceContextStatus;
        RelocalizeFirmwareFacts();
        RelocalizeInputInspection();
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(Text)));
        PresentationObserver.Invoke(() => NotifyContextTextChanged());
    }

    private void RelocalizeFirmwareFacts()
    {
        foreach (FirmwareSlotViewModel slot in _merge.MergeSlots
                     .Concat(_replace.ReplaceSlots)
                     .Append(_replace.ReplaceBaseSlot)
                     .Distinct())
        {
            if (slot.CurrentInspectionProjection is not { } inspection ||
                !FirmwareInspectionProjection.SupportsFacts(slot, inspection))
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
            if (slot.CurrentInspectionProjection is not { } projected)
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

    internal sealed record AcceptedFirmwareMismatchSelection(
        WorkflowInspectionContext Context,
        string SlotId,
        string Path);
}
