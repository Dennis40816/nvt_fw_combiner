using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Bootstrap;
using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class FirmwareSlotViewModel
{
    private string _checkingLabel = "Checking";
    private string _verifiedLabel = "Verified";
    private string _warningLabel = "Warning";
    private string _errorLabel = "Error";
    private string _notApplicableLabel = "Not applicable";
    private string _showDetailsLabel = "Show details";
    private string _hideDetailsLabel = "Hide details";
    private string _showMoreFactsTemplate = "Show {0} more details";
    private string _showFewerFactsLabel = "Show fewer details";

    /// <summary>True only for the DP Replace pilot until desktop adoption issue #208.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesLegacySlotPresentation))]
    [NotifyPropertyChangedFor(nameof(IsLegacyInputInspectionBlocking))]
    [NotifyPropertyChangedFor(nameof(IsLegacyInputInspectionPending))]
    [NotifyPropertyChangedFor(nameof(IsLegacyInputInspectionValid))]
    [NotifyPropertyChangedFor(nameof(IsLegacyInputInspectionWarning))]
    public partial bool UsesSharedSlotPresentation { get; set; }

    /// <summary>True while an existing workflow retains the pre-pilot slot presentation.</summary>
    public bool UsesLegacySlotPresentation => !UsesSharedSlotPresentation;

    /// <summary>Preserves blocking card chrome outside the DP Replace pilot.</summary>
    public bool IsLegacyInputInspectionBlocking =>
        UsesLegacySlotPresentation && IsInputInspectionBlocking;

    /// <summary>Preserves pending card chrome outside the DP Replace pilot.</summary>
    public bool IsLegacyInputInspectionPending =>
        UsesLegacySlotPresentation && IsInputInspectionPending;

    /// <summary>Preserves valid card chrome outside the DP Replace pilot.</summary>
    public bool IsLegacyInputInspectionValid =>
        UsesLegacySlotPresentation && IsInputInspectionValid;

    /// <summary>Preserves warning card chrome outside the DP Replace pilot.</summary>
    public bool IsLegacyInputInspectionWarning =>
        UsesLegacySlotPresentation && IsInputInspectionWarning;

    /// <summary>One presentation state composed from selection readiness and typed input inspection.</summary>
    public FirmwareSlotSemanticState SemanticState =>
        InputInspectionSeverity == WorkbenchInputInspectionSeverity.Blocking
            ? FirmwareSlotSemanticState.Error
            : SelectionReadinessState switch
            {
                ResolvedChildReadiness.Blocked => FirmwareSlotSemanticState.Error,
                ResolvedChildReadiness.NotApplicable => FirmwareSlotSemanticState.NotApplicable,
                ResolvedChildReadiness.PendingInput => FirmwareSlotSemanticState.Checking,
                ResolvedChildReadiness.Ready or null when IsInputInspectionPending => FirmwareSlotSemanticState.Checking,
                ResolvedChildReadiness.Ready or null => InputInspectionSeverity switch
                {
                    WorkbenchInputInspectionSeverity.Blocking => FirmwareSlotSemanticState.Error,
                    WorkbenchInputInspectionSeverity.Warning => FirmwareSlotSemanticState.Warning,
                    WorkbenchInputInspectionSeverity.Valid => FirmwareSlotSemanticState.Verified,
                    null when HasFile => FirmwareSlotSemanticState.Checking,
                    null => FirmwareSlotSemanticState.Empty,
                    _ => FirmwareSlotSemanticState.Error,
                },
                _ => FirmwareSlotSemanticState.Error,
            };

    private bool SelectionReadinessOwnsSemanticText =>
        InputInspectionSeverity != WorkbenchInputInspectionSeverity.Blocking &&
        SelectionReadinessState is ResolvedChildReadiness.Blocked or
            ResolvedChildReadiness.NotApplicable or
            ResolvedChildReadiness.PendingInput;

    /// <summary>True when the slot has a semantic state beyond its empty requirement guidance.</summary>
    public bool HasSemanticState => SemanticState != FirmwareSlotSemanticState.Empty;

    /// <summary>True while an empty applicable slot still needs its requirement label.</summary>
    public bool IsRequirementLabelVisible =>
        !HasFile && !HasSemanticState;

    /// <summary>True when the composed state is checking.</summary>
    public bool IsSemanticStateChecking => SemanticState == FirmwareSlotSemanticState.Checking;

    /// <summary>True when Checking represents a prerequisite-owned pending input.</summary>
    public bool IsSemanticStatePendingInput =>
        SemanticState == FirmwareSlotSemanticState.Checking &&
        SelectionReadinessState == ResolvedChildReadiness.PendingInput;

    /// <summary>True when the composed state is verified.</summary>
    public bool IsSemanticStateVerified => SemanticState == FirmwareSlotSemanticState.Verified;

    /// <summary>True when the composed state is warning.</summary>
    public bool IsSemanticStateWarning => SemanticState == FirmwareSlotSemanticState.Warning;

    /// <summary>True when the composed state is error.</summary>
    public bool IsSemanticStateError => SemanticState == FirmwareSlotSemanticState.Error;

    /// <summary>True when the compiled selection excludes this slot.</summary>
    public bool IsSemanticStateNotApplicable => SemanticState == FirmwareSlotSemanticState.NotApplicable;

    /// <summary>Localized visible label for the composed slot state.</summary>
    public string SemanticStateLabel => SelectionReadinessOwnsSemanticText
        ? SelectionReadinessLabel
        : SemanticState switch
        {
            FirmwareSlotSemanticState.Empty => string.Empty,
            FirmwareSlotSemanticState.Checking => _checkingLabel,
            FirmwareSlotSemanticState.Verified => _verifiedLabel,
            FirmwareSlotSemanticState.Warning => _warningLabel,
            FirmwareSlotSemanticState.Error => _errorLabel,
            FirmwareSlotSemanticState.NotApplicable => _notApplicableLabel,
            _ => _errorLabel,
        };

    /// <summary>Localized reason or next action for the composed slot state.</summary>
    public string SemanticStateDetail => SelectionReadinessOwnsSemanticText
        ? SelectionReadinessDetail
        : HasInputInspectionStatus
            ? InputInspectionStatus
            : Description;

    /// <summary>Screen-reader equivalent of the composed visible state.</summary>
    public string SemanticStateAutomationText => SelectionReadinessOwnsSemanticText
        ? SelectionReadinessAutomationText
        : string.Join(": ", new[] { SemanticStateLabel, SemanticStateDetail }.Where(static value => value.Length > 0));

    /// <summary>Vector icon path for the composed slot state.</summary>
    public string SemanticStateIconPathData => SemanticState switch
    {
        FirmwareSlotSemanticState.Checking => "M12 3A9 9 0 1 0 21 12 M12 7V12L15 14",
        FirmwareSlotSemanticState.Verified => "M4 12L9 17L20 6",
        FirmwareSlotSemanticState.Warning => "M12 3L22 20H2L12 3 M12 9V14 M12 17H12.01",
        FirmwareSlotSemanticState.Error => "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 7V13 M12 17H12.01",
        FirmwareSlotSemanticState.NotApplicable => "M5 12H19",
        FirmwareSlotSemanticState.Empty => string.Empty,
        _ => "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 7V13 M12 17H12.01",
    };

    /// <summary>True when the operator has pinned the state reason open.</summary>
    [ObservableProperty]
    public partial bool IsSemanticStateDetailExpanded { get; set; }

    /// <summary>True when additional facts beyond the first four are expanded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirmwareFactsDisclosureLabel))]
    public partial bool IsFirmwareFactsExpanded { get; set; }

    /// <summary>True when facts after the four primary values are disclosed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AdditionalFirmwareFactsLabel))]
    public partial bool IsAdditionalFirmwareFactsExpanded { get; set; }

    /// <summary>Localized quiet-disclosure label for the compact metadata body.</summary>
    public string FirmwareFactsDisclosureLabel => IsFirmwareFactsExpanded
        ? _hideDetailsLabel
        : _showDetailsLabel;

    /// <summary>Localized quiet-disclosure label for facts beyond the first four.</summary>
    public string AdditionalFirmwareFactsLabel => IsAdditionalFirmwareFactsExpanded
        ? _showFewerFactsLabel
        : string.Format(
            CultureInfo.CurrentCulture,
            _showMoreFactsTemplate,
            AdditionalFirmwareFacts.Count);

    /// <summary>Updates localized labels owned by the shared slot presentation.</summary>
    public void ApplyExperienceText(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _checkingLabel = text.FirmwareSlotCheckingLabel;
        _verifiedLabel = text.FirmwareSlotVerifiedLabel;
        _warningLabel = text.FirmwareSlotWarningLabel;
        _errorLabel = text.FirmwareSlotErrorLabel;
        _notApplicableLabel = text.FirmwareSlotNotApplicableLabel;
        _showDetailsLabel = text.FirmwareSlotShowDetailsLabel;
        _hideDetailsLabel = text.FirmwareSlotHideDetailsLabel;
        _showMoreFactsTemplate = text.FirmwareSlotShowMoreFactsTemplate;
        _showFewerFactsLabel = text.FirmwareSlotShowFewerFactsLabel;
        NotifySemanticStateChanged();
        OnPropertyChanged(nameof(FirmwareFactsDisclosureLabel));
        OnPropertyChanged(nameof(AdditionalFirmwareFactsLabel));
    }

    partial void OnFilePathChanged(string? value)
    {
        IsSemanticStateDetailExpanded = false;
        NotifySemanticStateChanged();
    }

    private void NotifySemanticStateChanged()
    {
        OnPropertyChanged(nameof(SemanticState));
        OnPropertyChanged(nameof(HasSemanticState));
        OnPropertyChanged(nameof(IsRequirementLabelVisible));
        OnPropertyChanged(nameof(IsSemanticStateChecking));
        OnPropertyChanged(nameof(IsSemanticStatePendingInput));
        OnPropertyChanged(nameof(IsSemanticStateVerified));
        OnPropertyChanged(nameof(IsSemanticStateWarning));
        OnPropertyChanged(nameof(IsSemanticStateError));
        OnPropertyChanged(nameof(IsSemanticStateNotApplicable));
        OnPropertyChanged(nameof(SemanticStateLabel));
        OnPropertyChanged(nameof(SemanticStateDetail));
        OnPropertyChanged(nameof(SemanticStateAutomationText));
        OnPropertyChanged(nameof(SemanticStateIconPathData));
    }
}
