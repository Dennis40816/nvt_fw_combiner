using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns shared workflow-context and selected-firmware prompt presentation.</summary>
public sealed partial class WorkflowSessionPresentationViewModel : ObservableObject
{
    private readonly Action<WorkflowContextSelection> _applyWorkflowContext;
    private readonly Action<string> _applyDetectedNumber;
    private readonly Func<IReadOnlyList<string>> _icChoices;
    private readonly Func<bool> _isReplaceVisible;
    private readonly Func<IReadOnlyList<IcNumberChoiceViewModel>> _numberChoices;
    private readonly Action _refreshCtrlRamDisplay;
    private readonly Func<string> _selectedIc;
    private readonly Func<string> _selectedNumber;
    private readonly Action<string> _setSelectedIc;
    private readonly Action<string, string> _showToast;
    private readonly Func<ShellTextResources> _textProvider;

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
        Action refreshCtrlRamDisplay,
        Action<string, string> showToast)
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
        _refreshCtrlRamDisplay = refreshCtrlRamDisplay ?? throw new ArgumentNullException(nameof(refreshCtrlRamDisplay));
        _showToast = showToast ?? throw new ArgumentNullException(nameof(showToast));
        ConfirmWorkflowContextCommand = new RelayCommand(ConfirmWorkflowContext);
        CancelWorkflowContextCommand = new RelayCommand(CancelWorkflowContext);
        AcceptFirmwareIcMismatchCommand = new RelayCommand(AcceptFirmwareIcMismatch);
        DismissFirmwareIcMismatchCommand = new RelayCommand(DismissFirmwareIcMismatch);
        AcceptFirmwareNumberMismatchCommand = new RelayCommand(AcceptFirmwareNumberMismatch);
        DismissFirmwareNumberMismatchCommand = new RelayCommand(DismissFirmwareNumberMismatch);
    }

    /// <summary>Gets current localized shell text used by workflow-session prompts.</summary>
    public ShellTextResources Text => _textProvider();

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
