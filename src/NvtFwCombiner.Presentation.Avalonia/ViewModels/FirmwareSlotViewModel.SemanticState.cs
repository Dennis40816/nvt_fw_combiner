using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Metadata;
using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class FirmwareSlotViewModel
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

    public FirmwareSlotSemanticState SemanticState =>
        InputInspectionSeverity == FirmwareInputInspectionSeverity.Blocking
            ? FirmwareSlotSemanticState.Error
            : SelectionReadinessState switch
            {
                ResolvedChildReadiness.Blocked => FirmwareSlotSemanticState.Error,
                ResolvedChildReadiness.NotApplicable => FirmwareSlotSemanticState.NotApplicable,
                ResolvedChildReadiness.PendingInput => FirmwareSlotSemanticState.Checking,
                ResolvedChildReadiness.Ready or null when IsInputInspectionPending => FirmwareSlotSemanticState.Checking,
                ResolvedChildReadiness.Ready or null => InputInspectionSeverity switch
                {
                    FirmwareInputInspectionSeverity.Blocking => FirmwareSlotSemanticState.Error,
                    FirmwareInputInspectionSeverity.Warning => FirmwareSlotSemanticState.Warning,
                    FirmwareInputInspectionSeverity.Valid => FirmwareSlotSemanticState.Verified,
                    null when HasFile => FirmwareSlotSemanticState.Checking,
                    null => FirmwareSlotSemanticState.Empty,
                    _ => FirmwareSlotSemanticState.Error,
                },
                _ => FirmwareSlotSemanticState.Error,
            };

    private bool SelectionReadinessOwnsSemanticText =>
        InputInspectionSeverity != FirmwareInputInspectionSeverity.Blocking &&
        SelectionReadinessState is ResolvedChildReadiness.Blocked or
            ResolvedChildReadiness.NotApplicable or
            ResolvedChildReadiness.PendingInput;

    public bool HasSemanticState => SemanticState != FirmwareSlotSemanticState.Empty;

    public bool IsRequirementLabelVisible =>
        !HasFile && !HasSemanticState;

    public bool IsSemanticStateChecking => SemanticState == FirmwareSlotSemanticState.Checking;

    public bool IsSemanticStatePendingInput =>
        SemanticState == FirmwareSlotSemanticState.Checking &&
        SelectionReadinessState == ResolvedChildReadiness.PendingInput;

    public bool IsSemanticStateVerified => SemanticState == FirmwareSlotSemanticState.Verified;

    public bool IsSemanticStateWarning => SemanticState == FirmwareSlotSemanticState.Warning;

    public bool IsSemanticStateError => SemanticState == FirmwareSlotSemanticState.Error;

    public bool IsSemanticStateNotApplicable => SemanticState == FirmwareSlotSemanticState.NotApplicable;

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

    public string SemanticStateDetail => SelectionReadinessOwnsSemanticText
        ? SelectionReadinessDetail
        : HasInputInspectionStatus
            ? InputInspectionStatus
            : Description;

    public string SemanticStateAutomationText => SelectionReadinessOwnsSemanticText
        ? SelectionReadinessAutomationText
        : string.Join(": ", new[] { SemanticStateLabel, SemanticStateDetail }.Where(static value => value.Length > 0));

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

    [ObservableProperty]
    public partial bool IsSemanticStateDetailExpanded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirmwareFactsDisclosureLabel))]
    public partial bool IsFirmwareFactsExpanded { get; set; }

    /// <summary>True when facts after the four primary values are disclosed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AdditionalFirmwareFactsLabel))]
    public partial bool IsAdditionalFirmwareFactsExpanded { get; set; }

    public string FirmwareFactsDisclosureLabel => IsFirmwareFactsExpanded
        ? _hideDetailsLabel
        : _showDetailsLabel;

    public string AdditionalFirmwareFactsLabel => IsAdditionalFirmwareFactsExpanded
        ? _showFewerFactsLabel
        : string.Format(
            CultureInfo.CurrentCulture,
            _showMoreFactsTemplate,
            AdditionalFirmwareFacts.Count);

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
        ClearCurrentInspectionProjection();
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
