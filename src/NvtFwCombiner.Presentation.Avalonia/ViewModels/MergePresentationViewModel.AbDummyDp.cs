using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    private string? _dummyPromptIc;
    private string? _dummyPromptNumber;

    public bool UseDummyDpForAbMerge { get; private set; }
    public bool IsAbDummyDpPromptOpen { get; private set; }
    public FirmwareSlotViewModel DummyDpDisplaySlot { get; } = new(
        "dummy-dp-display", "DP", "Dummy bytes (0xFF)", FirmwareSlotKind.Dp, isOptional: true);
    public IAsyncRelayCommand ToggleAbDummyDpCommand { get; }
    public IAsyncRelayCommand ConfirmAbDummyDpCommand { get; }
    public IRelayCommand CancelAbDummyDpCommand { get; }

    internal bool CanChangeAbDummyDp => IsAbCodeMergeModeSelected &&
        _stateBindings.IsMergePageActive() && !_stateBindings.IsAbDummyDpTransitionInProgress() &&
        _stateBindings.IsWorkflowLoaded() && !_stateBindings.IsWorkflowLoading() &&
        !_stateBindings.IsRunInProgress();

    private async Task ToggleAbDummyDpAsync()
    {
        if (!CanChangeAbDummyDp) { return; }
        if (UseDummyDpForAbMerge)
        {
            await _stateBindings.SetAbDummyDpModeAsync(false, CancellationToken.None);
            return;
        }
        _dummyPromptIc = SelectedIc;
        _dummyPromptNumber = SelectedNumber;
        IsAbDummyDpPromptOpen = true;
        NotifyAbDummyDpCommandStateChanged();
        OnPropertyChanged(nameof(IsAbDummyDpPromptOpen));
        OnPropertyChanged(nameof(UseDummyDpForAbMerge));
    }

    private async Task ConfirmAbDummyDpAsync()
    {
        bool accepted = IsAbDummyDpPromptOpen && CanChangeAbDummyDp &&
            _dummyPromptIc == SelectedIc && _dummyPromptNumber == SelectedNumber;
        CancelAbDummyDp();
        if (accepted)
        {
            await _stateBindings.SetAbDummyDpModeAsync(true, CancellationToken.None);
        }
    }

    private void CancelAbDummyDp()
    {
        IsAbDummyDpPromptOpen = false;
        _dummyPromptIc = null;
        _dummyPromptNumber = null;
        NotifyAbDummyDpCommandStateChanged();
        OnPropertyChanged(nameof(IsAbDummyDpPromptOpen));
        OnPropertyChanged(nameof(UseDummyDpForAbMerge));
    }

    internal void ApplyAbDummyDpMode(bool enabled)
    {
        UseDummyDpForAbMerge = enabled;
        ApplyDummyDpDisplayText();
        _hasPreparedAbMergeSnapshot = false;
        _preparedAbMergeSnapshot = null;
        ClearAbMergeActionReadiness();
        RefreshMergeSlotRequirements();
        OnPropertyChanged(nameof(UseDummyDpForAbMerge));
    }

    private AbMergeDpMode AbDpMode => UseDummyDpForAbMerge ? AbMergeDpMode.Dummy : AbMergeDpMode.Normal;

    internal void NotifyAbDummyDpCommandStateChanged()
    {
        ToggleAbDummyDpCommand.NotifyCanExecuteChanged();
        ConfirmAbDummyDpCommand.NotifyCanExecuteChanged();
    }

    private void ApplyDummyDpDisplayText()
    {
        DummyDpDisplaySlot.ApplyDisplayText("DP", Text.AbDummyDpDisabledDetail,
            Text.RequiredLabel, Text.AbDummyDpOptionLabel, "0xFF");
    }
}
