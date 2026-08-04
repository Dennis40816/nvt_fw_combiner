using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class WorkflowSessionPresentationViewModel
{
    private static readonly IReadOnlyList<string> s_abMergeIcChoices =
        Array.AsReadOnly([.. WorkbenchCompositionService.GetAbMergeProfileSummaries().Select(static profile => profile.IcId)]);

    internal string DeviceContextRefreshSummary { get; private set; } = string.Empty;

    /// <summary>Gets IC choices admitted by the active authoring context.</summary>
    public IReadOnlyList<string> IcChoices => IsAbMergeContextActive
        ? s_abMergeIcChoices
        : WorkbenchCompositionService.GetSupportedIcIds();

    /// <summary>Gets grouped display choices for the IC-count control.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<IcNumberChoiceViewModel> NumberSelectionChoices { get; set; } = [];

    /// <summary>Gets or sets the selected displayed IC-count choice while retaining its planner token.</summary>
    public IcNumberChoiceViewModel? SelectedNumberChoice
    {
        get => NumberSelectionChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, SelectedNumber, StringComparison.Ordinal));
        set
        {
            if (value is not null && !string.Equals(SelectedNumber, value.Token, StringComparison.Ordinal))
            {
                SelectedNumber = value.Token;
            }
        }
    }

    /// <summary>Gets or sets the selected IC id in the shared workflow context.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedIc { get; set; } = WorkbenchCompositionService.GetDefaultIcId();

    /// <summary>Gets or sets the selected IC count/variant in the shared workflow context.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedNumber { get; set; } = WorkbenchIcNumberTokens.SingleChip;

    /// <summary>Gets the shared device-context status.</summary>
    public string DeviceContextStatus => IsNumberSelectorVisible
        ? $"{_stateBindings.DisplayedDeviceIc()} / {_stateBindings.DisplayedDeviceNumber()}: {_stateBindings.DisplayedDeviceContextRefreshSummary()}"
        : $"{_stateBindings.DisplayedDeviceIc()}: {_stateBindings.DisplayedDeviceContextRefreshSummary()}";

    /// <summary>True when the shared context row should expose the IC Number selector.</summary>
    public bool IsNumberSelectorVisible => _stateBindings.IsRunInProgress()
        ? _stateBindings.ActiveRunShowsNumberSelector()
        : ShouldShowNumberSelectorForSelectedPage();

    /// <summary>True when the hidden IC Number selector should keep its layout space.</summary>
    public bool IsNumberSelectorPlaceholderVisible =>
        (_stateBindings.IsRunInProgress() || _stateBindings.SelectedPage() is ShellPage.Merge or ShellPage.Replace) &&
        !IsNumberSelectorVisible;

    /// <summary>True when the mutable shell selection controls may be shown.</summary>
    public bool IsDeviceContextSelectionVisible => !_stateBindings.IsRunInProgress();

    /// <summary>True when the mutable IC Number selection control may be shown.</summary>
    public bool IsDeviceContextNumberSelectionVisible =>
        IsNumberSelectorVisible && !_stateBindings.IsRunInProgress();

    /// <summary>True when the selected-family badge describes the visible mutable context.</summary>
    public bool IsDeviceContextFamilyBadgeVisible =>
        !_stateBindings.IsRunInProgress() && HasSelectedIcFamily;

    /// <summary>Owner-defined IC-family relationship shown without changing firmware maps.</summary>
    public WorkbenchIcFamilySummary SelectedIcFamilySummary =>
        WorkbenchCompositionService.GetIcFamilySummary(SelectedIc);

    /// <summary>Localized label for an owner-defined IC family.</summary>
    public string SelectedIcFamilyLabel => Text.GetIcFamilyLabel(SelectedIcFamilySummary.Relationship);

    /// <summary>Localized boundary of reusable family facts.</summary>
    public string SelectedIcFamilyTooltip => Text.GetIcFamilyTooltip(SelectedIcFamilySummary);

    /// <summary>True when the selected IC has an owner-defined family relation.</summary>
    public bool HasSelectedIcFamily => SelectedIcFamilySummary.FamilyId is not null;

    /// <summary>Concise family value shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailFamily => Text.GetIcDetailFamilyValue(SelectedIcFamilySummary);

    /// <summary>Owner-declared fact reuse scope shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailReuse => Text.GetIcDetailReuseValue(SelectedIcFamilySummary);

    /// <summary>Typed executable workflow inventory shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailRuntime => Text.GetIcDetailRuntimeValue(
        _merge.IsStandardMergeSupported,
        _merge.IsAbMergeSupported,
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, WorkbenchReplaceModes.Dp).IsAvailable,
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, WorkbenchReplaceModes.CtrlRam).IsAvailable,
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, WorkbenchReplaceModes.General).IsAvailable);

    /// <summary>Evidence summary shown without badge clusters.</summary>
    public string SelectedIcDetailEvidence => Text.GetIcDetailEvidenceValue(
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, WorkbenchReplaceModes.Dp),
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, WorkbenchReplaceModes.CtrlRam),
        WorkbenchCompositionService.GetReplaceWorkflowReadiness(SelectedIc, WorkbenchReplaceModes.General));

    /// <summary>Support boundary shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailSupport => Text.GetIcDetailSupportValue(_merge.IsAbMergeSupported);

    /// <summary>Screen-reader equivalent of the visible IC detail card.</summary>
    public string SelectedIcDetailAutomationText => string.Join(
        Environment.NewLine,
        SelectedIc,
        $"{Text.IcDetailFamilyLabel}: {SelectedIcDetailFamily}",
        $"{Text.IcDetailReuseLabel}: {SelectedIcDetailReuse}",
        $"{Text.IcDetailRuntimeLabel}: {SelectedIcDetailRuntime}",
        $"{Text.IcDetailEvidenceLabel}: {SelectedIcDetailEvidence}",
        $"{Text.IcDetailSupportLabel}: {SelectedIcDetailSupport}");

    internal bool IsWorkflowLoaded { get; private set; }

    internal bool IsLoadingWorkflow { get; private set; }

    internal bool ShouldShowNumberSelectorForSelectedPage()
    {
        return _stateBindings.SelectedPage() == ShellPage.Replace ||
            (_stateBindings.SelectedPage() == ShellPage.Merge &&
             _merge.IsAbCodeMergeModeSelected &&
             _merge.HasAbMergeTopologyChoices);
    }

    internal void EnsureWorkflowLoaded()
    {
        if (IsWorkflowLoaded)
        {
            return;
        }

        IsLoadingWorkflow = true;
        try
        {
            RefreshNumberChoicesForSelectedIc();
            _merge.GeneralMergeOutputLength =
                WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength(SelectedIc);
            _merge.GeneralMergeOutputFillByte =
                WorkbenchCompositionService.GetGeneralMergeDefaultOutputFillByte(SelectedIc);
            _replace.AddGeneralReplaceMapping();
            _merge.AddGeneralMergeMapping();
            IsWorkflowLoaded = true;
        }
        finally
        {
            IsLoadingWorkflow = false;
        }
    }

    internal void RefreshNumberChoicesForSelectedIc()
    {
        IReadOnlyList<IcNumberChoiceViewModel> nextDisplayChoices = IsAbMergeContextActive
            ? [.. AbMergeWorkbenchCompositionService.GetTopologyChoices(SelectedIc)
                .Select(static choice => new IcNumberChoiceViewModel(choice.Token, choice.DisplayLabel))]
            : UiCompositionRunner.GetNumberSelectionChoices(SelectedIc);
        NumberSelectionChoices = nextDisplayChoices;
        if (nextDisplayChoices.Count == 0)
        {
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        if (!nextDisplayChoices.Any(choice =>
                string.Equals(choice.Token, SelectedNumber, StringComparison.Ordinal)))
        {
            SelectedNumber = nextDisplayChoices.FirstOrDefault(choice =>
                string.Equals(choice.Token, WorkbenchIcNumberTokens.SingleChip, StringComparison.Ordinal))?.Token ??
                nextDisplayChoices[0].Token;
        }

        OnPropertyChanged(nameof(SelectedNumberChoice));
    }

    internal void RefreshContextState(bool resetRunResult = false, bool preserveReplaceSlotFiles = false)
    {
        EnsureWorkflowLoaded();
        _merge.RefreshMergeSlotRequirements();
        _replace.RefreshContextState(preserveSlotFiles: preserveReplaceSlotFiles);
        _merge.ApplyFirmwareSlotText();
        _replace.ApplyFirmwareSlotText();
        _merge.RefreshMergeMemoryMapState();
        _stateBindings.RefreshCommandState();
        NotifyContextTextChanged();
        if (resetRunResult)
        {
            _stateBindings.ResetRunResult();
        }
    }

    internal void NotifyContextTextChanged()
    {
        _merge.NotifyContextChanged();
        _replace.NotifyContextChanged();
        OnPropertyChanged(nameof(IcChoices));
        OnPropertyChanged(nameof(SelectedIcFamilySummary));
        OnPropertyChanged(nameof(SelectedIcFamilyLabel));
        OnPropertyChanged(nameof(SelectedIcFamilyTooltip));
        OnPropertyChanged(nameof(HasSelectedIcFamily));
        OnPropertyChanged(nameof(SelectedIcDetailFamily));
        OnPropertyChanged(nameof(SelectedIcDetailReuse));
        OnPropertyChanged(nameof(SelectedIcDetailRuntime));
        OnPropertyChanged(nameof(SelectedIcDetailEvidence));
        OnPropertyChanged(nameof(SelectedIcDetailSupport));
        OnPropertyChanged(nameof(SelectedIcDetailAutomationText));
        NotifyRunStateChanged();
        _stateBindings.NotifyRunContextChanged();
    }

    internal void NotifyRunStateChanged()
    {
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        OnPropertyChanged(nameof(IsDeviceContextSelectionVisible));
        OnPropertyChanged(nameof(IsDeviceContextNumberSelectionVisible));
        OnPropertyChanged(nameof(IsDeviceContextFamilyBadgeVisible));
        OnPropertyChanged(nameof(DeviceContextStatus));
    }

    internal void ReplaceModeChanged()
    {
        InvalidateFirmwareNumberMismatch();
        InvalidateFirmwareInspection();
        _replace.InvalidateCtrlRamFirmwareVersionContextState();
        RefreshContextState(resetRunResult: true);
        RefreshCtrlRamDisplayFromInspection();
    }

    private bool IsAbMergeContextActive =>
        _stateBindings.SelectedPage() == ShellPage.Merge && _merge.IsAbCodeMergeModeSelected;

    partial void OnSelectedIcChanged(string value)
    {
        InvalidateFirmwareNumberMismatch();
        AcceptedFirmwareMismatchSelection? acceptedMismatch = ConsumeAcceptedFirmwareMismatchSelection();
        InvalidateFirmwareInspection(clearBaseCache: true, clearFileProjections: true);
        _replace.InvalidateCtrlRamFirmwareVersionContextState();
        if (_merge.IsAbCodeMergeModeSelected && !AbMergeWorkbenchCompositionService.IsAbMergeSupported(value))
        {
            _merge.SelectMergeMode(WorkbenchMergeModes.Standard);
        }

        IsRefreshingFirmwareInspectionContext = true;
        try
        {
            RefreshNumberChoicesForSelectedIc();
            _merge.GeneralMergeOutputLength = WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength(value);
            _merge.GeneralMergeOutputFillByte =
                WorkbenchCompositionService.GetGeneralMergeDefaultOutputFillByte(value);
        }
        finally
        {
            IsRefreshingFirmwareInspectionContext = false;
        }

        RefreshContextState(resetRunResult: true, preserveReplaceSlotFiles: acceptedMismatch is not null);
        string? acceptedMismatchSlotId = null;
        if (acceptedMismatch is { } selection &&
            FindSlot(selection.SlotId) is { } acceptedSlot &&
            string.Equals(acceptedSlot.FilePath, selection.Path, StringComparison.Ordinal))
        {
            acceptedMismatchSlotId = selection.SlotId;
        }
        else if (acceptedMismatch is { } missingSelection)
        {
            _showToast(
                Text.ContextUpdatedToastTitle,
                Text.FormatFirmwareSelectionNotRetainedToast(Path.GetFileName(missingSelection.Path)));
        }

        _ = RefreshAllSelectedFirmwareInspectionsAsync(acceptedMismatchSlotId);
        RememberReplaceWorkflowContext();
    }

    partial void OnSelectedNumberChanged(string value)
    {
        RememberReplaceWorkflowContext();
        InvalidateFirmwareNumberMismatch();
        if (IsRefreshingFirmwareInspectionContext)
        {
            _replace.InvalidateCtrlRamFirmwareVersionContextState();
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        bool clearFileProjections = _merge.IsAbCodeMergeModeSelected && _merge.HasAbMergeTopologyChoices;
        InvalidateFirmwareInspection(clearFileProjections: clearFileProjections);
        _replace.InvalidateCtrlRamFirmwareVersionContextState();
        OnPropertyChanged(nameof(SelectedNumberChoice));
        RefreshContextState(resetRunResult: true, preserveReplaceSlotFiles: true);
        RefreshAbMergeInputsAfterTopologyChange();
        bool refreshCtrlRamInputs = IsCtrlRamReplaceModeSelected &&
            ReplaceSlots.Append(ReplaceBaseSlot).Any(static slot => slot.HasFile);
        if (refreshCtrlRamInputs)
        {
            _ = RefreshSelectedReplaceFirmwareInspectionsAsync();
        }

        if (!IsApplyingFirmwareInspectionContext && !refreshCtrlRamInputs)
        {
            RefreshCtrlRamDisplayFromInspection();
        }
    }

    private void RefreshAbMergeInputsAfterTopologyChange()
    {
        if (_merge.IsAbCodeMergeModeSelected &&
            _merge.HasAbMergeTopologyChoices &&
            _merge.MergeSlots.Any(slot => slot.HasFile))
        {
            _ = RefreshSelectedMergeFirmwareInspectionsAsync();
        }
    }
}
