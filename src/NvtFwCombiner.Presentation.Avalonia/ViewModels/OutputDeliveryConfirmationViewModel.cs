using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record OutputDeliveryDecision(
    string? OutputPath,
    string? AdditionalOutputPath,
    bool OutputPathUsesAutomaticName,
    bool AdditionalOutputPathUsesAutomaticName,
    CompositionOutputBundleIntent? BundleIntent);

internal sealed record OutputDeliveryRequest(
    CompositionOutputBundleProposal Proposal,
    bool IsReplaceOutput,
    CompositionAdditionalDeliveryPlan? AdditionalDelivery,
    Func<bool> IsCurrent,
    ReplacePresentationViewModel? CtrlRamOptions,
    Func<Task<bool>>? PrepareModeSpecificAsync,
    Action? Cancel,
    Func<OutputDeliveryDecision, Task> ExecuteAsync);

/// <summary>Shared pre-delivery confirmation state for every GUI Build entry point.</summary>
internal sealed partial class OutputDeliveryConfirmationViewModel : ObservableObject
{
    private readonly ICompositionOutputNaming _outputNaming;
    private readonly Func<ShellTextResources> _text;
    private OutputDeliveryRequest? _request;

    internal OutputDeliveryConfirmationViewModel(
        ICompositionOutputNaming outputNaming,
        Func<ShellTextResources> text)
    {
        _outputNaming = outputNaming ?? throw new ArgumentNullException(nameof(outputNaming));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        CancelCommand = new RelayCommand(Cancel);
    }

    public ShellTextResources Text => _text();

    public bool IsOpen { get; private set; }

    public bool BundleEnabled { get; private set; }

    public bool AdditionalDeliveryEnabled { get; private set; }

    public bool OffersAdditionalDelivery => _request?.AdditionalDelivery is not null;

    public bool IsReplaceOutput => _request?.IsReplaceOutput == true;

    public ReplacePresentationViewModel? CtrlRamOptions => _request?.CtrlRamOptions;

    public bool HasCtrlRamOptions => CtrlRamOptions is not null;

    public string AdditionalDeliveryLabel => BundleEnabled
        ? Text.OutputDeliveryAdditionalInBundleLabel
        : Text.OutputDeliveryAdditionalLabel;

    public string OutputFileName { get; private set; } = string.Empty;

    public string CanonicalOutputFileName =>
        _request?.Proposal.OutputPreparation.OutputName.FileName ?? string.Empty;

    public bool IsOutputFileNameEditing { get; private set; }

    public bool CanEditOutputFileName => !BundleEnabled;

    public bool OutputFileNameUsesAutomaticName => StringComparer.Ordinal.Equals(
        OutputFileName,
        CanonicalOutputFileName);

    public string AdditionalSuggestedFileName =>
        _request?.AdditionalDelivery?.SuggestedFileName ?? string.Empty;

    public IReadOnlyList<CompositionOutputBundleSourceSummary> Sources =>
        _request?.Proposal.Sources ?? [];

    public string BundleFolderName { get; private set; } = string.Empty;

    public string ParentDirectory { get; private set; } = string.Empty;

    public string ResolvedDirectoryPreview { get; private set; } = string.Empty;

    public string ValidationMessage { get; private set; } = string.Empty;

    public bool IsBundleDestinationValid { get; private set; }

    public bool CanConfirm => !BundleEnabled || IsBundleDestinationValid;

    public IRelayCommand CancelCommand { get; }

    internal void Open(OutputDeliveryRequest request, bool preserveDeliveryState = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool preserveCustomOutputName =
            preserveDeliveryState &&
            !OutputFileNameUsesAutomaticName &&
            !string.IsNullOrWhiteSpace(OutputFileName);
        _request = request;
        if (!preserveCustomOutputName || BundleEnabled)
        {
            ResetOutputFileName();
        }

        if (!preserveDeliveryState)
        {
            BundleFolderName = request.Proposal.FolderName;
        }

        IsOpen = true;
        AdditionalDeliveryEnabled = preserveDeliveryState &&
            AdditionalDeliveryEnabled &&
            request.AdditionalDelivery is not null;
        RefreshValidation();
        NotifyAll();
    }

    internal void SetBundleEnabled(bool enabled)
    {
        BundleEnabled = enabled;
        if (enabled)
        {
            ResetOutputFileName();
        }

        RefreshValidation();
        OnPropertyChanged(nameof(BundleEnabled));
        OnPropertyChanged(nameof(CanEditOutputFileName));
        OnPropertyChanged(nameof(AdditionalDeliveryLabel));
        OnPropertyChanged(nameof(CanConfirm));
    }

    internal void BeginOutputFileNameEdit()
    {
        if (!CanEditOutputFileName)
        {
            return;
        }

        IsOutputFileNameEditing = true;
        OnPropertyChanged(nameof(IsOutputFileNameEditing));
    }

    internal void SetOutputFileName(string value)
    {
        if (!IsOutputFileNameEditing || !CanEditOutputFileName)
        {
            return;
        }

        OutputFileName = value ?? string.Empty;
        OnPropertyChanged(nameof(OutputFileName));
        OnPropertyChanged(nameof(OutputFileNameUsesAutomaticName));
    }

    internal void SetAdditionalDeliveryEnabled(bool enabled)
    {
        AdditionalDeliveryEnabled = OffersAdditionalDelivery && enabled;
        OnPropertyChanged(nameof(AdditionalDeliveryEnabled));
        OnPropertyChanged(nameof(CanConfirm));
    }

    internal void SetBundleFolderName(string value)
    {
        BundleFolderName = value ?? string.Empty;
        RefreshValidation();
        OnPropertyChanged(nameof(BundleFolderName));
    }

    internal void SetParentDirectory(string value)
    {
        ParentDirectory = value ?? string.Empty;
        RefreshValidation();
        OnPropertyChanged(nameof(ParentDirectory));
    }

    internal async Task ConfirmLooseAsync(
        string outputPath,
        string? additionalOutputPath,
        bool outputPathUsesAutomaticName,
        bool additionalOutputPathUsesAutomaticName,
        bool prepareModeSpecific = true)
    {
        if (prepareModeSpecific && !await PrepareModeSpecificAsync())
        {
            return;
        }

        OutputDeliveryRequest request = RequireOpenRequest();
        if (!EnsureCurrent(request))
        {
            return;
        }

        IsOpen = false;
        OnPropertyChanged(nameof(IsOpen));
        await request.ExecuteAsync(new OutputDeliveryDecision(
            outputPath,
            additionalOutputPath,
            outputPathUsesAutomaticName,
            additionalOutputPathUsesAutomaticName,
            BundleIntent: null));
    }

    internal async Task ConfirmBundleAsync()
    {
        if (!await PrepareModeSpecificAsync())
        {
            return;
        }

        OutputDeliveryRequest request = RequireOpenRequest();
        if (!EnsureCurrent(request))
        {
            return;
        }

        CompositionOutputBundleIntent intent = request.Proposal.CreateIntent(
            ParentDirectory,
            BundleFolderName,
            AdditionalDeliveryEnabled
                ? request.AdditionalDelivery?.DeliveryKind
                : null);
        CompositionOutputBundleDestinationValidation validation =
            _outputNaming.ValidateBundleDestination(intent);
        ApplyValidation(validation);
        if (!validation.IsValid)
        {
            return;
        }

        IsOpen = false;
        OnPropertyChanged(nameof(IsOpen));
        await request.ExecuteAsync(new OutputDeliveryDecision(
            OutputPath: null,
            AdditionalOutputPath: null,
            OutputPathUsesAutomaticName: false,
            AdditionalOutputPathUsesAutomaticName: false,
            intent));
    }

    internal void ApplyLanguageChanged()
    {
        OnPropertyChanged(nameof(Text));
        RefreshValidation();
    }

    private void Cancel()
    {
        _request?.Cancel?.Invoke();
        IsOpen = false;
        OnPropertyChanged(nameof(IsOpen));
    }

    internal async Task<bool> PrepareModeSpecificAsync()
    {
        OutputDeliveryRequest request = RequireOpenRequest();
        return request.PrepareModeSpecificAsync is null ||
            await request.PrepareModeSpecificAsync();
    }

    private OutputDeliveryRequest RequireOpenRequest()
    {
        return IsOpen && _request is { } request
            ? request
            : throw new InvalidOperationException("Output delivery confirmation is not open.");
    }

    private bool EnsureCurrent(OutputDeliveryRequest request)
    {
        if (request.IsCurrent())
        {
            return true;
        }

        IsBundleDestinationValid = false;
        ValidationMessage = Text.OutputDeliveryStaleAcceptedSession;
        NotifyValidation();
        return false;
    }

    private void RefreshValidation()
    {
        if (!BundleEnabled || _request is null ||
            string.IsNullOrWhiteSpace(ParentDirectory) ||
            string.IsNullOrWhiteSpace(BundleFolderName))
        {
            IsBundleDestinationValid = false;
            ResolvedDirectoryPreview = string.Empty;
            ValidationMessage = BundleEnabled
                ? Text.OutputDeliveryDestinationRequired
                : string.Empty;
            NotifyValidation();
            return;
        }

        try
        {
            CompositionOutputBundleIntent intent = _request.Proposal.CreateIntent(
                ParentDirectory,
                BundleFolderName,
                AdditionalDeliveryEnabled
                    ? _request.AdditionalDelivery?.DeliveryKind
                    : null);
            ApplyValidation(_outputNaming.ValidateBundleDestination(intent));
        }
        catch (ArgumentException exception)
        {
            IsBundleDestinationValid = false;
            ResolvedDirectoryPreview = string.Empty;
            ValidationMessage = exception.Message;
            NotifyValidation();
        }
    }

    private void ResetOutputFileName()
    {
        OutputFileName = CanonicalOutputFileName;
        IsOutputFileNameEditing = false;
        OnPropertyChanged(nameof(OutputFileName));
        OnPropertyChanged(nameof(IsOutputFileNameEditing));
        OnPropertyChanged(nameof(OutputFileNameUsesAutomaticName));
    }

    private void ApplyValidation(CompositionOutputBundleDestinationValidation validation)
    {
        IsBundleDestinationValid = validation.IsValid;
        ResolvedDirectoryPreview = validation.ResolvedDirectoryPreview ?? string.Empty;
        ValidationMessage = validation.Issues.Count == 0
            ? string.Empty
            : validation.Issues[0].Message;
        NotifyValidation();
    }

    private void NotifyValidation()
    {
        OnPropertyChanged(nameof(IsBundleDestinationValid));
        OnPropertyChanged(nameof(ResolvedDirectoryPreview));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(CanConfirm));
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(OffersAdditionalDelivery));
        OnPropertyChanged(nameof(IsReplaceOutput));
        OnPropertyChanged(nameof(CtrlRamOptions));
        OnPropertyChanged(nameof(HasCtrlRamOptions));
        OnPropertyChanged(nameof(AdditionalDeliveryLabel));
        OnPropertyChanged(nameof(OutputFileName));
        OnPropertyChanged(nameof(CanonicalOutputFileName));
        OnPropertyChanged(nameof(IsOutputFileNameEditing));
        OnPropertyChanged(nameof(CanEditOutputFileName));
        OnPropertyChanged(nameof(OutputFileNameUsesAutomaticName));
        OnPropertyChanged(nameof(AdditionalSuggestedFileName));
        OnPropertyChanged(nameof(Sources));
        OnPropertyChanged(nameof(BundleFolderName));
        OnPropertyChanged(nameof(ParentDirectory));
        OnPropertyChanged(nameof(BundleEnabled));
        OnPropertyChanged(nameof(AdditionalDeliveryEnabled));
        NotifyValidation();
    }
}
