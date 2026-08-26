using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    private static readonly CapabilityFamilySummary EmptyFamilySummary = new(
        null,
        CapabilityFamilyRelationship.Standalone,
        null);
    private string _selectedIc = string.Empty;
    private bool _isPublishingCanonicalCatalogChoices;
    private string? _generalMergeDefaultsIc;
    private string _generalMergeDefaultLength = string.Empty;
    private string _generalMergeDefaultFillByte = string.Empty;
    private string? _preparedGeneralMergeDefaultsIc;
    private string _preparedGeneralMergeDefaultLength = string.Empty;
    private string _preparedGeneralMergeDefaultFillByte = string.Empty;

    internal string DeviceContextRefreshSummary { get; private set; } = string.Empty;

    public IReadOnlyList<string> IcChoices => _selectorPublication is null
        ? []
        : ActiveWorkflowOwner switch
        {
            WorkflowInspectionOwner.Merge when IsAbMergeContextActive =>
                GetPublishedWorkflowIcChoices(ExperienceIds.AbMerge),
            WorkflowInspectionOwner.Merge => GetPublishedPageIcChoices(ShellPage.Merge),
            WorkflowInspectionOwner.Replace => GetPublishedPageIcChoices(ShellPage.Replace),
            null => _selectorPublication.IcIds,
            _ => throw new InvalidOperationException("Unknown workflow inspection owner."),
        };

    /// <summary>True after the canonical capability publication is ready for workflow authoring.</summary>
    public bool IsCanonicalCatalogReady => _selectorPublication is not null;

    internal ResolutionToken? SelectorResolutionToken => _selectorPublication?.ResolutionToken;

    /// <summary>True when the current publication exposes any workflow-authorable IC.</summary>
    public bool HasWorkflowAuthoringChoices => _selectorPublication?.IcIds.Count > 0;

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
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!_isPublishingCanonicalCatalogChoices)
                {
                    OnPropertyChanged(nameof(SelectedIc));
                }
                return;
            }

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
        string.IsNullOrWhiteSpace(SelectedIc)
            ? EmptyFamilySummary
            : _compositionServices.Capabilities.GetIcFamilySummary(SelectedIc);

    /// <summary>Localized label for an owner-defined IC family.</summary>
    public string SelectedIcFamilyLabel => Text.GetIcFamilyLabel(SelectedIcFamilySummary.Relationship);

    public string SelectedIcFamilyTooltip => Text.GetIcFamilyTooltip(SelectedIcFamilySummary);

    /// <summary>True when the selected IC has an owner-defined family relation.</summary>
    public bool HasSelectedIcFamily => SelectedIcFamilySummary.FamilyId is not null;

    public string SelectedIcDetailFamily => Text.GetIcDetailFamilyValue(SelectedIcFamilySummary);

    /// <summary>Owner-declared fact reuse scope shown inside the IC selector detail card.</summary>
    public string SelectedIcDetailReuse => Text.GetIcDetailReuseValue(SelectedIcFamilySummary);

    /// <summary>True when canonical policy permits the Home DP Replace entry.</summary>
    public bool IsDpReplaceAvailable => !string.IsNullOrWhiteSpace(SelectedIc) &&
        IsPublishedWorkflowAuthorable(SelectedIc, ExperienceIds.DpReplace);

    internal bool IsPublishedWorkflowAuthorable(string icId, string workflowId)
    {
        return !string.IsNullOrWhiteSpace(icId) && _selectorPublication is { } publication &&
            publication.IsWorkflowAuthorable(icId, workflowId);
    }

    internal IReadOnlyList<CapabilityTopologyChoice> GetPublishedAbMergeTopologyChoices(
        string icId)
    {
        return string.IsNullOrWhiteSpace(icId) || _selectorPublication is null
            ? []
            : _selectorPublication.GetAbMergeTopologyChoices(icId);
    }

    public string SelectedIcDetailRuntime => string.IsNullOrWhiteSpace(SelectedIc)
        ? string.Empty
        : Text.GetIcDetailRuntimeValue(
            _merge.IsStandardMergeSupported,
            _merge.IsAbMergeSupported,
            IsPublishedWorkflowAuthorable(SelectedIc, ExperienceIds.DpReplace),
            IsPublishedWorkflowAuthorable(SelectedIc, ExperienceIds.CtrlRamReplace),
            IsPublishedWorkflowAuthorable(SelectedIc, ExperienceIds.GeneralReplace));

    /// <summary>Evidence summary shown without badge clusters.</summary>
    public string SelectedIcDetailEvidence => string.IsNullOrWhiteSpace(SelectedIc)
        ? string.Empty
        : Text.GetIcDetailEvidenceValue(
            _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, ExperienceIds.DpReplace),
            _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, ExperienceIds.CtrlRamReplace),
            _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, ExperienceIds.GeneralReplace));

    public string SelectedIcDetailSupport => string.IsNullOrWhiteSpace(SelectedIc)
        ? string.Empty
        : Text.GetIcDetailSupportValue(_merge.IsAbMergeSupported);

    public string SelectedIcDetailAutomationText => string.IsNullOrWhiteSpace(SelectedIc)
        ? string.Empty
        : string.Join(
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

        CapabilitySelectorPublication publication =
            _compositionServices.Capabilities.GetSelectorPublication();
        _selectorPublication = publication;
        _selectedIc = publication.DefaultIcId ?? string.Empty;
        InitializeWorkflowPageContexts(publication.DefaultIcId);
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IcChoices)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(SelectedIc)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(IsCanonicalCatalogReady)));
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(HasWorkflowAuthoringChoices)));
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
        CapabilitySelectorPublication publication =
            _compositionServices.Capabilities.GetSelectorPublication();
        if (_selectorPublication is null ||
            _selectorPublication.ResolutionToken != publication.ResolutionToken)
        {
            if (IsWorkflowLoaded)
            {
                RefreshCanonicalCatalogState(publication);
            }
            else
            {
                ApplySelectorPublication(publication);
            }
        }

        if (IsWorkflowLoaded)
        {
            return;
        }

        if (!HasWorkflowAuthoringChoices)
        {
            return;
        }

        IsLoadingWorkflow = true;
        try
        {
            RefreshNumberChoicesForSelectedIc();
            if (ActiveWorkflowOwner == WorkflowInspectionOwner.Merge)
            {
                RefreshGeneralMergeDefaults(GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
            }
            if (_replace.GeneralReplaceMappings.Count == 0)
            {
                _replace.AddGeneralReplaceMapping();
            }
            if (_merge.GeneralMergeMappings.Count == 0)
            {
                _merge.AddGeneralMergeMapping();
            }
            IsWorkflowLoaded = true;
        }
        finally
        {
            IsLoadingWorkflow = false;
        }
    }

    internal void RefreshNumberChoicesForSelectedIc()
    {
        if (_selectorPublication is null)
        {
            NumberSelectionChoices = [];
            ReconcilePublishedNumberChoice(string.Empty);
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        string contextIc = ActiveWorkflowOwner is { } owner
            ? GetWorkflowPageIc(owner)
            : SelectedIc;
        if (string.IsNullOrWhiteSpace(contextIc))
        {
            NumberSelectionChoices = [];
            ReconcilePublishedNumberChoice(string.Empty);
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        IReadOnlyList<IcNumberChoiceViewModel> nextDisplayChoices =
            GetPublishedNumberDisplayChoices(contextIc);
        NumberSelectionChoices = nextDisplayChoices;
        if (nextDisplayChoices.Count == 0)
        {
            ReconcilePublishedNumberChoice(ResolvePublishedNumber(
                contextIc,
                SelectedNumber,
                useAbTopology: IsAbMergeContextActive));
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        if (!nextDisplayChoices.Any(choice => string.Equals(choice.Token, SelectedNumber, StringComparison.Ordinal)))
        {
            ReconcilePublishedNumberChoice(nextDisplayChoices.FirstOrDefault(choice =>
                string.Equals(choice.Token, IcNumberSelectionTokens.SingleChip, StringComparison.Ordinal))?.Token ??
                nextDisplayChoices[0].Token);
        }

        OnPropertyChanged(nameof(SelectedNumberChoice));
    }

    private IReadOnlyList<IcNumberChoiceViewModel> GetPublishedNumberDisplayChoices(string icId)
    {
        CapabilitySelectorPublication publication = _selectorPublication ??
            throw new InvalidOperationException("Canonical selector publication is not ready.");
        return IsAbMergeContextActive
            ? [.. publication.GetAbMergeTopologyChoices(icId)
                .Select(static choice => new IcNumberChoiceViewModel(choice.Token, choice.DisplayLabel))]
            : UiCompositionRunner.GetNumberSelectionChoices(publication, icId);
    }

    internal void RefreshContextState(WorkflowInspectionOwner? owner = null, bool resetRunResult = false,
        bool preserveReplaceSlotFiles = false)
    {
        EnsureWorkflowLoaded();
        if (!HasWorkflowAuthoringChoices || string.IsNullOrWhiteSpace(SelectedIc))
        {
            if (owner is null or WorkflowInspectionOwner.Merge)
            {
                _merge.RefreshContextState();
            }
            if (owner is null or WorkflowInspectionOwner.Replace)
            {
                _replace.ClearUnavailableContextState();
            }
            _stateBindings.RefreshCommandState();
            NotifyContextTextChanged(owner, notifyIcChoices: false);
            if (resetRunResult)
            {
                _stateBindings.ResetRunResult();
            }
            return;
        }

        if (owner == WorkflowInspectionOwner.Merge ||
            (owner is null && ActiveWorkflowOwner == WorkflowInspectionOwner.Merge))
        {
            RefreshGeneralMergeDefaults(GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
        }

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
        _refreshCommandAvailability();
        NotifyContextTextChanged(owner, notifyIcChoices: false);
        if (resetRunResult)
        {
            _stateBindings.ResetRunResult();
        }
    }

    internal void RefreshCanonicalCatalogState()
    {
        CapabilitySelectorPublication publication =
            _compositionServices.Capabilities.GetSelectorPublication();
        RefreshCanonicalCatalogState(publication);
    }

    private void RefreshCanonicalCatalogState(CapabilitySelectorPublication publication)
    {
        if (_selectorPublication is not null &&
            _selectorPublication.ResolutionToken == publication.ResolutionToken)
        {
            return;
        }

        bool workflowWasLoaded = IsWorkflowLoaded;
        if (workflowWasLoaded && ActiveWorkflowOwner is { } validatingOwner)
        {
            ValidateWorkflowContextRefresh(
                publication,
                validatingOwner == WorkflowInspectionOwner.Merge
                    ? ShellPage.Merge
                    : ShellPage.Replace);
        }
        ApplySelectorPublication(publication);
        if (workflowWasLoaded)
        {
            _merge.InvalidateCanonicalCatalogSessions();
            _replace.InvalidateCanonicalCatalogSessions();
            InvalidateFirmwareInspection(clearBaseProjection: true, clearSlotProjections: true);
            _replace.InvalidateCtrlRamFirmwareVersionContextState();
        }

        if (workflowWasLoaded)
        {
            // A fresh token invalidates both page projections even when the
            // retained selector values did not change. The active page is
            // rebuilt below; the hidden page is rebuilt on its next activation.
            _mergeWorkflowContextNeedsRefresh = true;
            _replaceWorkflowContextNeedsRefresh = true;
        }
        if (!workflowWasLoaded || !HasWorkflowAuthoringChoices)
        {
            _stateBindings.RefreshCommandState();
            NotifyContextTextChanged(notifyIcChoices: false);
            return;
        }

        WorkflowInspectionOwner? activeOwner = ActiveWorkflowOwner;
        if (activeOwner is null)
        {
            _stateBindings.RefreshCommandState();
            return;
        }

        CompiledAuthoringSelectionSnapshot? stagedDpProjection;
        try
        {
            RefreshContextState(
                activeOwner,
                preserveReplaceSlotFiles: activeOwner == WorkflowInspectionOwner.Replace);
            stagedDpProjection = _replace.TakeCatalogRefreshDpProjection();
        }
        catch
        {
            _replace.CompleteCatalogRefreshProjection();
            throw;
        }
        if (activeOwner == WorkflowInspectionOwner.Merge)
        {
            _mergeWorkflowContextNeedsRefresh = false;
        }
        else
        {
            _replaceWorkflowContextNeedsRefresh = false;
        }

        try
        {
            RefreshRetainedFirmwareInspections(activeOwner.Value, stagedDpProjection);
        }
        finally
        {
            _replace.CompleteCatalogRefreshProjection();
        }
    }

    private void RefreshRetainedFirmwareInspections(
        WorkflowInspectionOwner owner,
        CompiledAuthoringSelectionSnapshot? stagedDpProjection = null)
    {
        WorkflowInspectionContext context = InspectionContext(owner);
        string icId = GetWorkflowPageIc(owner);
        if (!IsPublishedWorkflowAuthorable(icId, context.Mode) ||
            !InspectionSlots(context).Any(static slot => slot.HasFile))
        {
            return;
        }

        _ = owner == WorkflowInspectionOwner.Merge
            ? RefreshSelectedMergeFirmwareInspectionsAsync()
            : RefreshSelectedFirmwareInspectionsAsync(
                context,
                InspectionSlots(context),
                applyVerifiedContextSlotId: null,
                CancellationToken.None,
                stagedDpProjection);
    }

    private void PublishActiveSelectorState(string icId, string number)
    {
        PublishCanonicalCatalogIcChoices();
        _isActivatingWorkflowPageContext = true;
        try
        {
            _selectedIc = icId;
            OnPropertyChanged(nameof(SelectedIc));
            OnPropertyChanged(nameof(DeviceContextStatus));
            NumberSelectionChoices = string.IsNullOrWhiteSpace(icId)
                ? []
                : GetPublishedNumberDisplayChoices(icId);
            SelectedNumber = number;
            OnPropertyChanged(nameof(SelectedNumberChoice));
        }
        finally
        {
            _isActivatingWorkflowPageContext = false;
        }
    }

    private void ReconcilePublishedNumberChoice(string number)
    {
        bool wasActivatingWorkflowPageContext = _isActivatingWorkflowPageContext;
        _isActivatingWorkflowPageContext = true;
        try
        {
            SelectedNumber = number;
        }
        finally
        {
            _isActivatingWorkflowPageContext = wasActivatingWorkflowPageContext;
        }

        StoreWorkflowPageContext(ActiveWorkflowOwner, SelectedIc, SelectedNumber);
    }

    private void PublishCurrentCatalogChoices()
    {
        OnPropertyChanged(nameof(IsCanonicalCatalogReady));
        OnPropertyChanged(nameof(HasWorkflowAuthoringChoices));
    }

    private void PublishCanonicalCatalogIcChoices()
    {
        _isPublishingCanonicalCatalogChoices = true;
        try
        {
            OnPropertyChanged(nameof(IcChoices));
        }
        finally
        {
            _isPublishingCanonicalCatalogChoices = false;
        }
    }

    internal void ReplaceModeChanged()
    {
        InvalidateFirmwareNumberMismatch();
        InvalidateFirmwareInspection(WorkflowInspectionOwner.Replace);
        _replace.InvalidateCtrlRamFirmwareVersionContextState();
        RefreshContextState(WorkflowInspectionOwner.Replace, resetRunResult: true);
        if (!TryRefreshRetainedReplaceFirmwareInspectionsIfStale())
        {
            RefreshCtrlRamDisplayFromInspection();
        }
    }

    private bool IsAbMergeContextActive =>
        _stateBindings.SelectedPage() == ShellPage.Merge && _merge.IsAbCodeMergeModeSelected;

    private void OnSelectedIcChanged(string value, WorkflowInspectionOwner? owner)
    {
        _recordActivity(new SystemActivityDraft(
            SystemActivityCodes.IcSelected,
            SystemActivityImportance.Debug,
            SystemActivityCategory.Workflow,
            SystemActivitySeverity.Information,
            value));
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
        bool mergeModeReconciled = owner == WorkflowInspectionOwner.Merge &&
            _merge.StageAuthorableModeForCatalogReconciliation(
                workflowId => IsPublishedWorkflowAuthorable(value, workflowId));
        bool replaceModeReconciled = owner == WorkflowInspectionOwner.Replace &&
            _replace.StageAuthorableModeForCatalogReconciliation(
                workflowId => IsPublishedWorkflowAuthorable(value, workflowId));
        if (mergeModeReconciled)
        {
            _merge.PublishCatalogReconciledMergeMode();
        }
        if (replaceModeReconciled)
        {
            _replace.PublishCatalogReconciledReplaceMode();
        }

        IsRefreshingFirmwareInspectionContext = true;
        try
        {
            RefreshNumberChoicesForSelectedIc();
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

        _recordActivity(new SystemActivityDraft(
            SystemActivityCodes.NumberSelected,
            SystemActivityImportance.Debug,
            SystemActivityCategory.Workflow,
            SystemActivitySeverity.Information,
            value,
            SelectedIc));

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
