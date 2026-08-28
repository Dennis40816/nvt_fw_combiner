using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Metadata;
using System.Collections.ObjectModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>UI file slot for one firmware input artifact.</summary>
internal sealed partial class FirmwareSlotViewModel : ObservableObject
{
    private string RequiredText { get; set; } = "Required";
    private string OptionalText { get; set; } = "Optional";
    private string EmptyDisplayName { get; set; } = "No BIN selected";
    private bool _isSelectionLinked;
    private string _selectionLinkLabel = string.Empty;

    /// <summary>Creates a firmware slot.</summary>
    public FirmwareSlotViewModel(
        string slotId,
        string title,
        string description,
        FirmwareSlotKind kind,
        bool isOptional = false,
        string? regionId = null,
        string? addressSpaceId = null,
        ReplaceRegionGroup regionGroup = ReplaceRegionGroup.Common,
        ReplaceInputRole replaceInputRole = ReplaceInputRole.None,
        string? compiledSlotId = null,
        CtrlRamInputDescriptionFacts? ctrlRamDescriptionFacts = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        SlotId = slotId;
        DeclaredTitle = title;
        DeclaredDescription = description;
        Title = title;
        Description = description;
        SlotKind = kind;
        DeclaredIsOptional = isOptional;
        IsOptional = isOptional;
        RegionId = string.IsNullOrWhiteSpace(regionId) ? null : regionId;
        AddressSpaceId = string.IsNullOrWhiteSpace(addressSpaceId) ? null : addressSpaceId;
        CompiledSlotId = string.IsNullOrWhiteSpace(compiledSlotId) ? null : compiledSlotId;
        RegionGroup = regionGroup;
        ReplaceInputRole = replaceInputRole;
        CtrlRamDescriptionFacts = ctrlRamDescriptionFacts;
    }

    public string SlotId { get; }

    /// <summary>Invariant producer text retained so language changes never translate a prior projection.</summary>
    internal string DeclaredTitle { get; }

    /// <summary>Invariant producer detail retained for deterministic relocalization.</summary>
    internal string DeclaredDescription { get; }

    public ReplaceInputRole ReplaceInputRole { get; }

    public ReplaceRegionGroup RegionGroup { get; }

    internal CtrlRamInputDescriptionFacts? CtrlRamDescriptionFacts { get; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    /// <summary>Display-only slot kind used by the slot card icon.</summary>
    public FirmwareSlotKind SlotKind { get; }

    /// <summary>Profile-owned physical region identity used only for coverage selection projection.</summary>
    public string? RegionId { get; }

    /// <summary>Canonical composition address space used by Application selection readiness.</summary>
    public string? AddressSpaceId { get; }

    /// <summary>Compiler-owned slot identity used by Application authoring sessions.</summary>
    public string? CompiledSlotId { get; }

    public bool DeclaredIsOptional { get; }

    public string RequirementLabel => IsOptional ? OptionalText : RequiredText;

    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    public bool IsGuidanceVisible => !HasFile;

    public string DisplayName => HasFile ? Path.GetFileName(FilePath!) : EmptyDisplayName;

    /// <summary>Filename plus a Presentation-owned linked-selection marker when the picker is delegated.</summary>
    public string DisplayNameWithSelectionContext => _isSelectionLinked
        ? $"{DisplayName} · {_selectionLinkLabel}"
        : DisplayName;

    public string DisplayDetail => HasFile ? FirmwarePathDisplay.Normalize(FilePath!) : string.Empty;

    /// <summary>Current immutable Application inspection used only by this selected slot projection.</summary>
    internal FirmwareInspectionSnapshot? CurrentInspectionProjection { get; private set; }

    internal long? InspectedFileLength =>
        CurrentInspectionProjection?.FileStamp?.AcceptedLength;

    /// <summary>Firmware facts decoded from the selected file, when the active IC has a FWConfig map.</summary>
    public ObservableCollection<FirmwareSlotFactViewModel> FirmwareFacts { get; } = [];

    public IReadOnlyList<FirmwareSlotFactViewModel> PrimaryFirmwareFacts => [.. FirmwareFacts.Take(4)];

    /// <summary>Facts disclosed on demand after the four primary facts.</summary>
    public IReadOnlyList<FirmwareSlotFactViewModel> AdditionalFirmwareFacts => [.. FirmwareFacts.Skip(4)];

    /// <summary>True when the slot has decoded firmware facts to show.</summary>
    public bool HasFirmwareFacts => FirmwareFacts.Count > 0;

    /// <summary>True when decoded firmware facts exceed the four-card primary limit.</summary>
    public bool HasAdditionalFirmwareFacts => FirmwareFacts.Count > 4;

    public bool HasInputInspectionStatus =>
        IsInputInspectionPending || InputInspectionSeverity is not null || IsBaseDiscoveryInspected;

    public bool BlocksBuild =>
        InputInspectionSeverity == FirmwareInputInspectionSeverity.Blocking ||
        SelectionReadinessState == ResolvedChildReadiness.Blocked;

    public bool IsInputInspectionPending { get; private set; }

    /// <summary>True only for Application-owned non-terminal CtrlRAM Base discovery.</summary>
    public bool IsBaseDiscoveryInspected { get; private set; }

    /// <summary>Highest completed typed input health, or null before inspection.</summary>
    public FirmwareInputInspectionSeverity? InputInspectionSeverity { get; private set; }

    public string InputInspectionStatus { get; private set; } = string.Empty;

    public bool HasSelectionReadinessStatus => SelectionReadinessState is not null;

    /// <summary>Application-owned applicability state projected for display.</summary>
    public ResolvedChildReadiness? SelectionReadinessState { get; private set; }

    /// <summary>Application-owned admission for an independent picker transition.</summary>
    public bool? SelectionReadinessCanSelect { get; private set; }

    public bool CanSelectFile => !_isSelectionLinked && (SelectionReadinessCanSelect ?? true);

    public string SelectionReadinessLabel { get; private set; } = string.Empty;

    public string SelectionReadinessDetail { get; private set; } = string.Empty;

    public string SelectionReadinessAutomationText { get; private set; } = string.Empty;

    public bool IsInputInspectionBlocking => InputInspectionSeverity == FirmwareInputInspectionSeverity.Blocking;

    public bool IsInputInspectionWarning => InputInspectionSeverity == FirmwareInputInspectionSeverity.Warning;

    public bool IsInputInspectionValid => InputInspectionSeverity == FirmwareInputInspectionSeverity.Valid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(IsGuidanceVisible))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(DisplayNameWithSelectionContext))]
    [NotifyPropertyChangedFor(nameof(DisplayDetail))]
    public partial string? FilePath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequirementLabel))]
    public partial bool IsOptional { get; set; }

    /// <summary>Updates localizable display-only slot text without changing the stable slot id or file state.</summary>
    public void ApplyDisplayText(
        string title,
        string description,
        string requiredLabel,
        string optionalLabel,
        string emptyDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionalLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyDisplayName);

        Title = title;
        Description = description;
        RequiredText = requiredLabel;
        OptionalText = optionalLabel;
        EmptyDisplayName = emptyDisplayName;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(RequirementLabel));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(DisplayNameWithSelectionContext));
        NotifySemanticStateChanged();
    }

    /// <summary>Delegates this card's picker to a peer while retaining its independent logical slot.</summary>
    internal void SetLinkedSelection(bool isLinked, string label)
    {
        if (isLinked)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
        }

        string nextLabel = isLinked ? label : string.Empty;
        if (_isSelectionLinked == isLinked &&
            string.Equals(_selectionLinkLabel, nextLabel, StringComparison.Ordinal))
        {
            return;
        }

        _isSelectionLinked = isLinked;
        _selectionLinkLabel = nextLabel;
        OnPropertyChanged(nameof(CanSelectFile));
        OnPropertyChanged(nameof(DisplayNameWithSelectionContext));
    }

    /// <summary>Replaces decoded firmware facts for this slot.</summary>
    public void SetFirmwareFacts(IEnumerable<FirmwareSlotFactViewModel> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        FirmwareFacts.Clear();
        foreach (FirmwareSlotFactViewModel fact in facts)
        {
            if (!fact.IsNotApplicable)
            {
                FirmwareFacts.Add(fact);
            }
        }

        IsAdditionalFirmwareFactsExpanded = false;
        OnPropertyChanged(nameof(HasFirmwareFacts));
        OnPropertyChanged(nameof(PrimaryFirmwareFacts));
        OnPropertyChanged(nameof(AdditionalFirmwareFacts));
        OnPropertyChanged(nameof(HasAdditionalFirmwareFacts));
        OnPropertyChanged(nameof(AdditionalFirmwareFactsLabel));
    }

    internal void SetCurrentInspectionProjection(FirmwareInspectionSnapshot inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (!HasFile)
        {
            throw new InvalidOperationException(
                "A firmware inspection projection requires one current selected file.");
        }

        CurrentInspectionProjection = inspection;
    }

    internal void ClearCurrentInspectionProjection()
    {
        CurrentInspectionProjection = null;
    }

    /// <summary>Reprojects cached facts after a language change without collapsing overflow disclosure state.</summary>
    internal void RelocalizeFirmwareFacts(IEnumerable<FirmwareSlotFactViewModel> facts)
    {
        bool areAdditionalFactsExpanded = IsAdditionalFirmwareFactsExpanded;
        SetFirmwareFacts(facts);
        IsAdditionalFirmwareFactsExpanded = areAdditionalFactsExpanded;
    }

    /// <summary>Marks the current selected source as awaiting a fresh typed inspection.</summary>
    public void SetInputInspectionPending(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        IsInputInspectionPending = true;
        IsBaseDiscoveryInspected = false;
        InputInspectionSeverity = null;
        InputInspectionStatus = status;
        NotifyInputInspectionChanged();
    }

    public void SetInputInspection(
        FirmwareInputInspectionSeverity severity,
        string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
        }

        IsInputInspectionPending = false;
        IsBaseDiscoveryInspected = false;
        InputInspectionSeverity = severity;
        InputInspectionStatus = status;
        NotifyInputInspectionChanged();
    }

    /// <summary>Projects a typed base-only discovery without claiming terminal input health.</summary>
    public void SetBaseDiscoveryInspected(string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        IsInputInspectionPending = false;
        IsBaseDiscoveryInspected = true;
        InputInspectionSeverity = null;
        InputInspectionStatus = detail;
        NotifyInputInspectionChanged();
    }

    /// <summary>Clears stale input health when the selected path or compiled context changes.</summary>
    public void ClearInputInspection()
    {
        IsInputInspectionPending = false;
        IsBaseDiscoveryInspected = false;
        InputInspectionSeverity = null;
        InputInspectionStatus = string.Empty;
        NotifyInputInspectionChanged();
    }

    /// <summary>Applies one localized projection of Application input-selection readiness.</summary>
    public void SetSelectionReadiness(
        ResolvedChildReadiness state,
        string label,
        string detail,
        string automationText,
        bool canSelect = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(automationText);
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }

        if (SelectionReadinessState == state &&
            SelectionReadinessCanSelect == canSelect &&
            string.Equals(SelectionReadinessLabel, label, StringComparison.Ordinal) &&
            string.Equals(SelectionReadinessDetail, detail, StringComparison.Ordinal) &&
            string.Equals(SelectionReadinessAutomationText, automationText, StringComparison.Ordinal))
        {
            return;
        }

        SelectionReadinessState = state;
        SelectionReadinessCanSelect = canSelect;
        SelectionReadinessLabel = label;
        SelectionReadinessDetail = detail;
        SelectionReadinessAutomationText = automationText;
        NotifySelectionReadinessChanged();
    }

    /// <summary>Clears selection readiness when the active workflow has no matching group member.</summary>
    public void ClearSelectionReadiness()
    {
        if (SelectionReadinessState is null &&
            SelectionReadinessCanSelect is null &&
            SelectionReadinessLabel.Length == 0 &&
            SelectionReadinessDetail.Length == 0 &&
            SelectionReadinessAutomationText.Length == 0)
        {
            return;
        }

        SelectionReadinessState = null;
        SelectionReadinessCanSelect = null;
        SelectionReadinessLabel = string.Empty;
        SelectionReadinessDetail = string.Empty;
        SelectionReadinessAutomationText = string.Empty;
        NotifySelectionReadinessChanged();
    }

    private void NotifyInputInspectionChanged()
    {
        OnPropertyChanged(nameof(HasInputInspectionStatus));
        OnPropertyChanged(nameof(BlocksBuild));
        OnPropertyChanged(nameof(IsInputInspectionPending));
        OnPropertyChanged(nameof(IsBaseDiscoveryInspected));
        OnPropertyChanged(nameof(InputInspectionSeverity));
        OnPropertyChanged(nameof(InputInspectionStatus));
        OnPropertyChanged(nameof(IsInputInspectionBlocking));
        OnPropertyChanged(nameof(IsInputInspectionWarning));
        OnPropertyChanged(nameof(IsInputInspectionValid));
        NotifySemanticStateChanged();
    }

    private void NotifySelectionReadinessChanged()
    {
        OnPropertyChanged(nameof(HasSelectionReadinessStatus));
        OnPropertyChanged(nameof(SelectionReadinessState));
        OnPropertyChanged(nameof(SelectionReadinessCanSelect));
        OnPropertyChanged(nameof(CanSelectFile));
        OnPropertyChanged(nameof(SelectionReadinessLabel));
        OnPropertyChanged(nameof(SelectionReadinessDetail));
        OnPropertyChanged(nameof(SelectionReadinessAutomationText));
        OnPropertyChanged(nameof(BlocksBuild));
        NotifySemanticStateChanged();
    }

}
