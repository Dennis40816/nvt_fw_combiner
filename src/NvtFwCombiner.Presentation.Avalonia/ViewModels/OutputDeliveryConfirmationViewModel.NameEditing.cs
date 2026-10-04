namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class OutputDeliveryConfirmationViewModel
{
    private string _acceptedOutputFileName = string.Empty;
    private string _acceptedBundleFolderName = string.Empty;
    private bool _nameEditReverted;
    private bool IsOutputNameValid { get; set; }
    private enum OutputNameField { Primary, Additional, Folder }

    internal void BeginBundleDestinationEdit()
    {
        if (!BeginNameEdit(IsOpen && CanEditBundleDestination, IsBundleDestinationEditing)) { return; }
        IsBundleDestinationEditing = true;
        OnPropertyChanged(nameof(IsBundleDestinationEditing));
        NotifyValidation();
    }

    internal void CompleteBundleDestinationEdit()
    {
        if (!IsBundleDestinationEditing) { return; }
        (BundleFolderName, IsBundleDestinationEditing) = CompleteNameEdit(OutputNameField.Folder, BundleFolderName, ref _acceptedBundleFolderName);
        OnPropertyChanged(nameof(BundleFolderName));
        OnPropertyChanged(nameof(IsBundleDestinationEditing));
        _ = RefreshValidation();
    }

    internal void BeginOutputFileNameEdit()
    {
        if (!BeginNameEdit(CanEditOutputFileName, IsOutputFileNameEditing)) { return; }
        IsOutputFileNameEditing = true;
        OnPropertyChanged(nameof(IsOutputFileNameEditing));
        NotifyValidation();
    }

    internal void CompleteOutputFileNameEdit()
    {
        if (!IsOutputFileNameEditing) { return; }
        (OutputFileName, IsOutputFileNameEditing) = CompleteNameEdit(OutputNameField.Primary, OutputFileName, ref _acceptedOutputFileName);
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

    private bool BeginNameEdit(bool canEdit, bool isEditing)
    {
        if (!canEdit || isEditing) { return false; }
        _nameEditReverted = false;
        return true;
    }

    private (string Name, bool Editing) CompleteNameEdit(OutputNameField field, string name, ref string accepted)
    {
        _nameEditReverted = !IsEditedNameAcceptable(field);
        return (_nameEditReverted ? accepted : accepted = name, false);
    }

    private bool IsEditedNameAcceptable(OutputNameField field)
    {
        string value = field switch
        {
            OutputNameField.Primary => OutputFileName,
            OutputNameField.Additional => AdditionalOutputFileName,
            OutputNameField.Folder => BundleFolderName,
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        if (_outputNaming.ValidateName(value) is not null) { return false; }
        if (!BundleEnabled || _request is null || string.IsNullOrWhiteSpace(ParentDirectory)) { return true; }

        // Commit one field against the other committed value, never its unrelated draft.
        string folder = field == OutputNameField.Folder ? value : _acceptedBundleFolderName;
        string primary = field == OutputNameField.Primary ? value : _acceptedOutputFileName;
        string additional = field == OutputNameField.Additional ? value : _acceptedAdditionalOutputFileName;
        try
        {
            CompositionOutputBundleIntent intent = _request.Proposal.CreateIntent(
                ParentDirectory, folder,
                AdditionalDeliveryEnabled ? _request.AdditionalDelivery?.DeliveryKind : null,
                outputFileNameOverride: StringComparer.Ordinal.Equals(primary, CanonicalOutputFileName) ? null : primary,
                additionalOutputFileNameOverride: AdditionalDeliveryEnabled &&
                    !StringComparer.Ordinal.Equals(additional, AdditionalSuggestedFileName) ? additional : null);
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
