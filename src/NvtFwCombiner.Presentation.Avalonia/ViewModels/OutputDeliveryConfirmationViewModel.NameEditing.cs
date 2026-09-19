namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class OutputDeliveryConfirmationViewModel
{
    private string _acceptedOutputFileName = string.Empty;
    private string _acceptedBundleFolderName = string.Empty;
    private bool _nameEditReverted;
    private bool IsOutputNameValid { get; set; }

    internal void BeginBundleDestinationEdit()
    {
        if (!IsOpen || !CanEditBundleDestination || IsBundleDestinationEditing) { return; }
        _nameEditReverted = false;
        IsBundleDestinationEditing = true;
        OnPropertyChanged(nameof(IsBundleDestinationEditing));
        NotifyValidation();
    }

    internal void CompleteBundleDestinationEdit()
    {
        if (!IsBundleDestinationEditing) { return; }
        _nameEditReverted = !IsEditedNameAcceptable(outputName: false);
        if (_nameEditReverted) { BundleFolderName = _acceptedBundleFolderName; }
        else { _acceptedBundleFolderName = BundleFolderName; }
        IsBundleDestinationEditing = false;
        OnPropertyChanged(nameof(BundleFolderName));
        OnPropertyChanged(nameof(IsBundleDestinationEditing));
        _ = RefreshValidation();
    }

    internal void BeginOutputFileNameEdit()
    {
        if (!CanEditOutputFileName || IsOutputFileNameEditing) { return; }
        _nameEditReverted = false;
        IsOutputFileNameEditing = true;
        OnPropertyChanged(nameof(IsOutputFileNameEditing));
        NotifyValidation();
    }

    internal void CompleteOutputFileNameEdit()
    {
        if (!IsOutputFileNameEditing) { return; }
        _nameEditReverted = !IsEditedNameAcceptable(outputName: true);
        if (_nameEditReverted) { OutputFileName = _acceptedOutputFileName; }
        else { _acceptedOutputFileName = OutputFileName; }
        IsOutputFileNameEditing = false;
        OnPropertyChanged(nameof(IsOutputFileNameEditing));
        OnPropertyChanged(nameof(OutputFileName));
        OnPropertyChanged(nameof(OutputFileNameUsesAutomaticName));
        _ = RefreshValidation();
    }

    internal void SetOutputFileName(string value)
    {
        if (!IsOutputFileNameEditing || !CanEditOutputFileName ||
            StringComparer.Ordinal.Equals(OutputFileName, value)) { return; }
        _nameEditReverted = false;
        OutputFileName = value ?? string.Empty;
        _ = RefreshValidation();
        OnPropertyChanged(nameof(OutputFileName));
        OnPropertyChanged(nameof(OutputFileNameUsesAutomaticName));
    }

    private bool IsEditedNameAcceptable(bool outputName)
    {
        string value = outputName ? OutputFileName : BundleFolderName;
        if (_outputNaming.ValidateName(value) is not null) { return false; }
        if (!BundleEnabled || _request is null || string.IsNullOrWhiteSpace(ParentDirectory)) { return true; }

        // Commit one field against the other committed value, never its unrelated draft.
        string folder = outputName ? _acceptedBundleFolderName : value;
        string primary = outputName ? value : _acceptedOutputFileName;
        try
        {
            CompositionOutputBundleIntent intent = _request.Proposal.CreateIntent(
                ParentDirectory, folder,
                AdditionalDeliveryEnabled ? _request.AdditionalDelivery?.DeliveryKind : null,
                outputFileNameOverride: StringComparer.Ordinal.Equals(primary, CanonicalOutputFileName) ? null : primary);
            return !_outputNaming.ValidateBundleDestination(intent).Issues.Any(static issue =>
                issue.Code is CompositionOutputBundleValidationIssueCodes.NameInvalid or
                    CompositionOutputBundleValidationIssueCodes.NameReserved or
                    CompositionOutputBundleValidationIssueCodes.PathTooLong);
        }
        catch (ArgumentException)
        {
            // Canonical intent admission can reject an override even when its component is legal.
            return false;
        }
    }
}
