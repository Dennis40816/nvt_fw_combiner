using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    // A page-local request, never an accepted executable capability.
    private AsyncRelayCommand? _selectAbReference;

    internal CtrlRamAuthoringDraftState? CurrentCtrlRamDraft { get; private set; }
    public bool IsAbCtrlRamReference => CurrentCtrlRamDraft is AbCtrlRamDraftState;
    public bool IsStandardCtrlRamReference => !IsAbCtrlRamReference;
    public CapabilityWorkflowReadiness AbCtrlRamReadiness =>
        _compositionServices.CtrlRamAuthoring.GetAbReferenceReadiness(SelectedIc, SelectedNumber);
    public bool CanSelectAbCtrlRamReference => HasSelectedIc && AbCtrlRamReadiness.IsAvailable;
    public bool IsCtrlRamBankASelected => CurrentCtrlRamDraft is AbCtrlRamDraftState { Banks: AbCtrlRamBankSelection.A };
    public bool IsCtrlRamBankBSelected => CurrentCtrlRamDraft is AbCtrlRamDraftState { Banks: AbCtrlRamBankSelection.B };
    public bool IsCtrlRamBothBanksSelected => CurrentCtrlRamDraft is AbCtrlRamDraftState { Banks: AbCtrlRamBankSelection.Both };

    public IAsyncRelayCommand SelectStandardCtrlRamReferenceCommand => field ??=
        new AsyncRelayCommand(() => SelectCtrlRamDraftAsync(IsAbCtrlRamReference ? null : CurrentCtrlRamDraft), AsyncRelayCommandOptions.AllowConcurrentExecutions);
    public IAsyncRelayCommand SelectAbCtrlRamReferenceCommand => _selectAbReference ??=
        new AsyncRelayCommand(() => SelectCtrlRamDraftAsync(IsAbCtrlRamReference ? CurrentCtrlRamDraft : new AbCtrlRamDraftState()),
            () => CanSelectAbCtrlRamReference, AsyncRelayCommandOptions.AllowConcurrentExecutions);
    public IAsyncRelayCommand<AbCtrlRamBankSelection> SelectCtrlRamBanksCommand => field ??=
        new AsyncRelayCommand<AbCtrlRamBankSelection>(banks => SelectCtrlRamDraftAsync(
            CurrentCtrlRamDraft is AbCtrlRamDraftState current
                ? new AbCtrlRamDraftState(banks, current.AVersion, current.BVersion)
                : new AbCtrlRamDraftState(banks)), _ => IsAbCtrlRamReference && CanSelectAbCtrlRamReference,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);

    internal async Task SelectCtrlRamDraftAsync(CtrlRamAuthoringDraftState? draft)
    {
        if (Equals(CurrentCtrlRamDraft, draft))
        {
            NotifyCtrlRamBankState();
            return;
        }
        CurrentCtrlRamDraft = draft;
        _ctrlRamReplaceSession.InvalidateCanonicalPublication();
        InspectionLifecycles[CtrlRamReplaceMode].Invalidate();
        ClearCtrlRamActionReadiness();
        InvalidateCtrlRamFirmwareVersionContext();
        ClearCtrlRamInspectionDisplay();
        _stateBindings.ResetRunResult(CaptureRunContext(CtrlRamReplaceMode));
        RefreshContextState(preserveSlotFiles: true);
        NotifyCommandAvailabilityChanged();
        await _stateBindings.RefreshReplaceInspectionsAsync();
    }

    private void NotifyCtrlRamBankState()
    {
        OnPropertyChanged(nameof(IsAbCtrlRamReference));
        OnPropertyChanged(nameof(IsStandardCtrlRamReference));
        OnPropertyChanged(nameof(AbCtrlRamReadiness));
        OnPropertyChanged(nameof(CanSelectAbCtrlRamReference));
        OnPropertyChanged(nameof(IsCtrlRamBankASelected));
        OnPropertyChanged(nameof(IsCtrlRamBankBSelected));
        OnPropertyChanged(nameof(IsCtrlRamBothBanksSelected));
        _selectAbReference?.NotifyCanExecuteChanged();
        SelectCtrlRamBanksCommand.NotifyCanExecuteChanged();
        foreach (CtrlRamFirmwareVersionEditorViewModel editor in AbCtrlRamVersionEditors)
        {
            editor.ApplyText(Text);
        }
    }
}
