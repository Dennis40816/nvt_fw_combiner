using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    private string _selectedIc = string.Empty;

    private IReadOnlyList<string> AbMergeIcChoices { get; set; } = [];

    internal string DeviceContextRefreshSummary { get; private set; } = string.Empty;

    public IReadOnlyList<string> IcChoices => !IsCanonicalCatalogReady
        ? []
        : IsAbMergeContextActive
            ? AbMergeIcChoices
            : _compositionServices.Capabilities.GetIcIds();

    /// <summary>True after the canonical capability publication is ready for workflow authoring.</summary>
    public bool IsCanonicalCatalogReady { get; private set; }

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

    public string SelectedIc
    {
        get => _selectedIc;
        set
        {
            if (SetProperty(ref _selectedIc, value))
            {
                OnPropertyChanged(nameof(DeviceContextStatus));
                if (_isActivatingWorkflowPageContext)
                {
                    return;
                }

                WorkflowInspectionOwner? owner = ActiveWorkflowOwner;
                StoreWorkflowPageContext(owner, value, SelectedNumber);
                OnSelectedIcChanged(value, owner);
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedNumber { get; set; } = IcNumberSelectionTokens.SingleChip;

    public string DeviceContextStatus => IsNumberSelectorVisible
        ? $"{_stateBindings.DisplayedDeviceIc()} / {_stateBindings.DisplayedDeviceNumber()}: {_stateBindings.DisplayedDeviceContextRefreshSummary()}"
        : $"{_stateBindings.DisplayedDeviceIc()}: {_stateBindings.DisplayedDeviceContextRefreshSummary()}";

    public bool IsNumberSelectorVisible => _stateBindings.IsRunInProgress()
        ? _stateBindings.ActiveRunShowsNumberSelector()
        : ShouldShowNumberSelectorForSelectedPage();

    public bool IsNumberSelectorPlaceholderVisible =>
        (_stateBindings.IsRunInProgress() || _stateBindings.SelectedPage() is ShellPage.Merge or ShellPage.Replace) &&
        !IsNumberSelectorVisible;

    public bool IsDeviceContextSelectionVisible => !_stateBindings.IsRunInProgress();

    public bool IsDeviceContextNumberSelectionVisible =>
        IsNumberSelectorVisible && !_stateBindings.IsRunInProgress();

    public bool IsDeviceContextFamilyBadgeVisible =>
        !_stateBindings.IsRunInProgress() && HasSelectedIcFamily;

    /// <summary>Owner-defined IC-family relationship shown without changing firmware maps.</summary>
    public CapabilityFamilySummary SelectedIcFamilySummary =>
        _compositionServices.Capabilities.GetIcFamilySummary(SelectedIc);

    /// <summary>Localized label for an owner-defined IC family.</summary>
    public string SelectedIcFamilyLabel => Text.GetIcFamilyLabel(SelectedIcFamilySummary.Relationship);

    public string SelectedIcFamilyTooltip => Text.GetIcFamilyTooltip(SelectedIcFamilySummary);

    /// <summary>True when the selected IC has an owner-defined family relation.</summary>
    public bool HasSelectedIcFamily => SelectedIcFamilySummary.FamilyId is not null;

    public string SelectedIcDetailFamily => Text.GetIcDetailFamilyValue(SelectedIcFamilySummary);

    /// <summary>Owner-declared fact reuse scope shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailReuse => Text.GetIcDetailReuseValue(SelectedIcFamilySummary);

    /// <summary>True when canonical policy permits the Home DP Replace entry.</summary>
    public bool IsDpReplaceAvailable => _compositionServices.Capabilities
        .IsReplaceWorkflowAvailable(SelectedIc, ExperienceIds.DpReplace);

    public string SelectedIcDetailRuntime => Text.GetIcDetailRuntimeValue(
        _merge.IsStandardMergeSupported,
        _merge.IsAbMergeSupported,
        _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, ExperienceIds.DpReplace).IsAvailable,
        _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, ExperienceIds.CtrlRamReplace).IsAvailable,
        _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, ExperienceIds.GeneralReplace).IsAvailable);

    /// <summary>Evidence summary shown without badge clusters.</summary>
    public string SelectedIcDetailEvidence => Text.GetIcDetailEvidenceValue(
        _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, ExperienceIds.DpReplace),
        _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, ExperienceIds.CtrlRamReplace),
        _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, ExperienceIds.GeneralReplace));

    public string SelectedIcDetailSupport => Text.GetIcDetailSupportValue(_merge.IsAbMergeSupported);

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

    internal void PublishCanonicalCatalogState()
    {
        if (IsCanonicalCatalogReady)
        {
            return;
        }

        string defaultIcId = _compositionServices.Capabilities.DefaultIcId;
        IReadOnlyList<string> abMergeIcChoices = Array.AsReadOnly(
        [
            .. _compositionServices.Capabilities.GetAbMergeProfileSummaries()
                .Select(static profile => profile.IcId),
        ]);
        AbMergeIcChoices = abMergeIcChoices;
        _selectedIc = defaultIcId;
        InitializeWorkflowPageContexts(defaultIcId);
        IsCanonicalCatalogReady = true;
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IcChoices)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(SelectedIc)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IsCanonicalCatalogReady)));
    }

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
                _compositionServices.GeneralAuthoring.GetDefaultOutputLength(SelectedIc);
            _merge.GeneralMergeOutputFillByte =
                _compositionServices.GeneralAuthoring.GetDefaultOutputFillByte(SelectedIc);
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
            ? [.. _compositionServices.AbMergeAuthoring.GetTopologyChoices(SelectedIc)
                .Select(static choice => new IcNumberChoiceViewModel(choice.Token, choice.DisplayLabel))]
            : UiCompositionRunner.GetNumberSelectionChoices(_compositionServices, SelectedIc);
        NumberSelectionChoices = nextDisplayChoices;
        if (nextDisplayChoices.Count == 0)
        {
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        if (!nextDisplayChoices.Any(choice => string.Equals(choice.Token, SelectedNumber, StringComparison.Ordinal)))
        {
            SelectedNumber = nextDisplayChoices.FirstOrDefault(choice =>
                string.Equals(choice.Token, IcNumberSelectionTokens.SingleChip, StringComparison.Ordinal))?.Token ??
                nextDisplayChoices[0].Token;
        }

        OnPropertyChanged(nameof(SelectedNumberChoice));
    }

    internal void RefreshContextState(WorkflowInspectionOwner? owner = null, bool resetRunResult = false,
        bool preserveReplaceSlotFiles = false)
    {
        EnsureWorkflowLoaded();
        if (owner is null or WorkflowInspectionOwner.Merge)
        {
            _merge.RefreshMergeSlotRequirements();
            _merge.ApplyFirmwareSlotText();
            _merge.RefreshMergeMemoryMapState();
        }
        if (owner is null or WorkflowInspectionOwner.Replace)
        {
            _replace.RefreshContextState(preserveSlotFiles: preserveReplaceSlotFiles);
            _replace.ApplyFirmwareSlotText();
        }
        _stateBindings.RefreshCommandState();
        NotifyContextTextChanged(owner, notifyIcChoices: false);
        if (resetRunResult)
        {
            _stateBindings.ResetRunResult();
        }
    }

    internal void RefreshCanonicalCatalogState()
    {
        if (!IsWorkflowLoaded)
        {
            return;
        }

        _merge.InvalidateCanonicalCatalogSessions();
        _replace.InvalidateCanonicalCatalogSessions();
        InvalidateFirmwareInspection(clearBaseProjection: true, clearSlotProjections: true);
        _replace.InvalidateCtrlRamFirmwareVersionContextState();
        RefreshContextState(preserveReplaceSlotFiles: true);

        if (_stateBindings.SelectedPage() == ShellPage.Merge &&
            (_merge.IsNormalMergeModeSelected || _merge.IsAbCodeMergeModeSelected) &&
            _merge.MergeSlots.Any(static slot => slot.HasFile))
        {
            _ = RefreshSelectedMergeFirmwareInspectionsAsync();
        }
        else if (_stateBindings.SelectedPage() == ShellPage.Replace &&
            _replace.IsStructuredReplaceModeSelected &&
            _replace.ReplaceSlots.Concat([_replace.ReplaceBaseSlot]).Any(static slot => slot.HasFile))
        {
            _ = RefreshSelectedReplaceFirmwareInspectionsAsync();
        }
    }

    internal void NotifyContextTextChanged(WorkflowInspectionOwner? owner = null, bool notifyIcChoices = true)
    {
        if (owner is null or WorkflowInspectionOwner.Merge)
        {
            _merge.NotifyContextChanged();
        }
        if (owner is null or WorkflowInspectionOwner.Replace)
        {
            _replace.NotifyContextChanged();
        }
        if (notifyIcChoices)
        {
            OnPropertyChanged(nameof(IcChoices));
        }
        OnPropertyChanged(nameof(SelectedIcFamilySummary));
        OnPropertyChanged(nameof(SelectedIcFamilyLabel));
        OnPropertyChanged(nameof(SelectedIcFamilyTooltip));
        OnPropertyChanged(nameof(HasSelectedIcFamily));
        OnPropertyChanged(nameof(SelectedIcDetailFamily));
        OnPropertyChanged(nameof(SelectedIcDetailReuse));
        OnPropertyChanged(nameof(IsDpReplaceAvailable));
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
        InvalidateFirmwareInspection(WorkflowInspectionOwner.Replace);
        _replace.InvalidateCtrlRamFirmwareVersionContextState();
        RefreshContextState(WorkflowInspectionOwner.Replace, resetRunResult: true);
        RefreshCtrlRamDisplayFromInspection();
    }

    private bool IsAbMergeContextActive =>
        _stateBindings.SelectedPage() == ShellPage.Merge && _merge.IsAbCodeMergeModeSelected;

    private void OnSelectedIcChanged(string value, WorkflowInspectionOwner? owner)
    {
        InvalidateFirmwareNumberMismatch();
        AcceptedFirmwareMismatchSelection? acceptedMismatch = ConsumeAcceptedFirmwareMismatchSelection();
        InvalidateFirmwareInspection(
            owner,
            clearBaseProjection: owner is null or WorkflowInspectionOwner.Replace,
            clearSlotProjections: true);
        if (owner is null or WorkflowInspectionOwner.Replace)
        {
            _replace.InvalidateCtrlRamFirmwareVersionContextState();
        }
        if (owner is not WorkflowInspectionOwner.Replace &&
            _merge.IsAbCodeMergeModeSelected &&
            !_compositionServices.AbMergeAuthoring.IsAvailable(value))
        {
            _merge.SelectMergeMode(ExperienceIds.StandardMerge);
        }

        IsRefreshingFirmwareInspectionContext = true;
        try
        {
            RefreshNumberChoicesForSelectedIc();
            if (owner is not WorkflowInspectionOwner.Replace)
            {
                _merge.GeneralMergeOutputLength =
                    _compositionServices.GeneralAuthoring.GetDefaultOutputLength(value);
                _merge.GeneralMergeOutputFillByte =
                    _compositionServices.GeneralAuthoring.GetDefaultOutputFillByte(value);
            }
        }
        finally
        {
            IsRefreshingFirmwareInspectionContext = false;
        }

        RefreshContextState(
            owner,
            resetRunResult: true,
            preserveReplaceSlotFiles: owner == WorkflowInspectionOwner.Replace &&
                acceptedMismatch?.Context.Owner == WorkflowInspectionOwner.Replace);
        WorkflowInspectionContext? refreshContext = acceptedMismatch is { } accepted
            ? InspectionContext(accepted.Context.Owner)
            : ActiveInspectionContext;
        string? acceptedMismatchSlotId = null;
        if (acceptedMismatch is { } selection &&
            refreshContext is { } retainedContext &&
            FindInspectionSlot(retainedContext, selection.SlotId) is { } acceptedSlot &&
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

        _ = refreshContext?.Owner switch
        {
            WorkflowInspectionOwner.Merge => RefreshSelectedMergeFirmwareInspectionsAsync(acceptedMismatchSlotId),
            WorkflowInspectionOwner.Replace => RefreshSelectedReplaceFirmwareInspectionsAsync(acceptedMismatchSlotId),
            null => Task.CompletedTask,
            _ => throw new InvalidOperationException("Unknown workflow inspection owner."),
        };
        StoreWorkflowPageContext(owner, SelectedIc, SelectedNumber);
    }

    partial void OnSelectedNumberChanged(string value)
    {
        if (_isActivatingWorkflowPageContext)
        {
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        WorkflowInspectionOwner? owner = ActiveWorkflowOwner;
        StoreWorkflowPageContext(owner, SelectedIc, value);
        InvalidateFirmwareNumberMismatch();
        if (IsRefreshingFirmwareInspectionContext)
        {
            if (owner is null or WorkflowInspectionOwner.Replace)
            {
                _replace.InvalidateCtrlRamFirmwareVersionContextState();
            }
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        bool clearSlotProjections = owner is not WorkflowInspectionOwner.Replace &&
            _merge.IsAbCodeMergeModeSelected && _merge.HasAbMergeTopologyChoices;
        InvalidateFirmwareInspection(owner, clearSlotProjections: clearSlotProjections);
        if (owner is null or WorkflowInspectionOwner.Replace)
        {
            _replace.InvalidateCtrlRamFirmwareVersionContextState();
        }
        OnPropertyChanged(nameof(SelectedNumberChoice));
        RefreshContextState(
            owner,
            resetRunResult: true,
            preserveReplaceSlotFiles: owner is null or WorkflowInspectionOwner.Replace);
        if (owner is not WorkflowInspectionOwner.Replace)
        {
            RefreshAbMergeInputsAfterTopologyChange();
        }
        bool refreshCtrlRamInputs = owner is not WorkflowInspectionOwner.Merge &&
            ActiveInspectionContext is { IsCtrlRamReplace: true } &&
            ReplaceSlots.Append(ReplaceBaseSlot).Any(static slot => slot.HasFile);
        if (refreshCtrlRamInputs)
        {
            _ = RefreshSelectedReplaceFirmwareInspectionsAsync();
        }

        if (owner is not WorkflowInspectionOwner.Merge &&
            !IsApplyingFirmwareInspectionContext && !refreshCtrlRamInputs)
        {
            RefreshCtrlRamDisplayFromInspection();
        }
    }

    private void RefreshAbMergeInputsAfterTopologyChange()
    {
        if (ActiveInspectionContext is { IsAbMerge: true } &&
            _merge.HasAbMergeTopologyChoices &&
            _merge.MergeSlots.Any(slot => slot.HasFile))
        {
            _ = RefreshSelectedMergeFirmwareInspectionsAsync();
        }
    }
}
