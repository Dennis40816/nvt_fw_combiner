namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class OutputDeliveryConfirmationViewModel
{
    private string _acceptedAdditionalOutputFileName = string.Empty;

    public string AdditionalOutputFileName { get; private set; } = string.Empty;
    public bool IsAdditionalOutputFileNameEditing { get; private set; }
    public bool CanEditAdditionalOutputFileName => IsOpen && AdditionalDeliveryEnabled;
    public bool AdditionalOutputFileNameUsesAutomaticName =>
        StringComparer.Ordinal.Equals(AdditionalOutputFileName, AdditionalSuggestedFileName);

    internal void BeginAdditionalOutputFileNameEdit()
    {
        if (!CanEditAdditionalOutputFileName || IsAdditionalOutputFileNameEditing) { return; }
        _nameEditReverted = false;
        IsAdditionalOutputFileNameEditing = true;
        NotifyAdditionalName();
    }

    internal void SetAdditionalOutputFileName(string value)
    {
        if (!IsAdditionalOutputFileNameEditing || !CanEditAdditionalOutputFileName ||
            StringComparer.Ordinal.Equals(AdditionalOutputFileName, value)) { return; }
        _nameEditReverted = false;
        AdditionalOutputFileName = value ?? string.Empty;
        _ = RefreshValidation();
        NotifyAdditionalName();
    }

    internal void CompleteAdditionalOutputFileNameEdit()
    {
        if (!IsAdditionalOutputFileNameEditing) { return; }
        _nameEditReverted = !IsEditedNameAcceptable(OutputNameField.Additional);
        if (_nameEditReverted) { AdditionalOutputFileName = _acceptedAdditionalOutputFileName; }
        else { _acceptedAdditionalOutputFileName = AdditionalOutputFileName; }
        IsAdditionalOutputFileNameEditing = false;
        _ = RefreshValidation();
        NotifyAdditionalName();
    }

    private void ResetAdditionalOutputFileName()
    {
        AdditionalOutputFileName = AdditionalSuggestedFileName;
        _acceptedAdditionalOutputFileName = AdditionalOutputFileName;
        IsAdditionalOutputFileNameEditing = false;
    }

    private void NotifyAdditionalName()
    {
        OnPropertyChanged(nameof(AdditionalOutputFileName));
        OnPropertyChanged(nameof(IsAdditionalOutputFileNameEditing));
        OnPropertyChanged(nameof(CanEditAdditionalOutputFileName));
        OnPropertyChanged(nameof(AdditionalOutputFileNameUsesAutomaticName));
        NotifyValidation();
    }
}
