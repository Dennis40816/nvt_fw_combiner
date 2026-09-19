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
    private bool _preserveCancelledDeliveryState;
    private long _preparationGeneration;
    private bool ProposalIsCurrent { get; set; } = true;

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

    public bool CanEditOutputFileName => IsOpen;

    public bool OutputFileNameUsesAutomaticName => StringComparer.Ordinal.Equals(
        OutputFileName,
        CanonicalOutputFileName);

    public string AdditionalSuggestedFileName =>
        _request?.AdditionalDelivery?.SuggestedFileName ?? string.Empty;

    public IReadOnlyList<CompositionOutputBundleSourceSummary> Sources =>
        _request?.Proposal.Sources ?? [];

    public bool AreSourcesExpanded { get; private set; }

    public string SourcesSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        Text.OutputDeliverySourcesSummaryFormat,
        Sources.Count);

    public string BundleFolderName { get; private set; } = string.Empty;

    public string ParentDirectory { get; private set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public bool IsBundleDestinationEditing { get; private set; }

    public bool CanEditBundleDestination => BundleEnabled;

    public string ValidationMessage
    {
        get => field.Length != 0 ? field :
            _nameEditReverted ? Text.OutputDeliveryNameRestored : string.Empty;
        private set;
    } = string.Empty;

    public bool IsBundleDestinationValid { get; private set; }

    public bool CanConfirm => ProposalIsCurrent && IsOutputNameValid && (!BundleEnabled || IsBundleDestinationValid);

    public IRelayCommand CancelCommand { get; }

    internal long BeginPreparation()
    {
        return ++_preparationGeneration;
    }

    internal bool IsPreparationCurrent(long generation)
    {
        return generation == _preparationGeneration;
    }

    internal void Open(OutputDeliveryRequest request, bool preserveDeliveryState = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        _preparationGeneration++;
        ProposalIsCurrent = true;
        preserveDeliveryState |= _preserveCancelledDeliveryState &&
            _request is { } previous && previous.IsReplaceOutput == request.IsReplaceOutput && previous.IsCurrent();
        _preserveCancelledDeliveryState = false;
        _nameEditReverted = false;
        bool preserveCustomOutputName =
            preserveDeliveryState &&
            !OutputFileNameUsesAutomaticName;
        _request = request;
        AreSourcesExpanded = false;
        IsBundleDestinationEditing = false;
        if (!preserveCustomOutputName)
        {
            ResetOutputFileName();
        }

        if (!preserveDeliveryState)
        {
            BundleFolderName = request.Proposal.FolderName;
            _acceptedBundleFolderName = BundleFolderName;
        }

        IsOpen = true;
        AdditionalDeliveryEnabled = preserveDeliveryState &&
            AdditionalDeliveryEnabled &&
            request.AdditionalDelivery is not null;
        _ = RefreshValidation();
        NotifyAll();
    }

    internal void SetBundleEnabled(bool enabled)
    {
        BundleEnabled = enabled;
        if (!enabled)
        {
            IsBundleDestinationEditing = false;
        }

        _ = RefreshValidation();
        OnPropertyChanged(nameof(BundleEnabled));
        OnPropertyChanged(nameof(CanEditOutputFileName));
        OnPropertyChanged(nameof(CanEditBundleDestination));
        OnPropertyChanged(nameof(IsBundleDestinationEditing));
        OnPropertyChanged(nameof(AdditionalDeliveryLabel));
        NotifySummary();
        OnPropertyChanged(nameof(CanConfirm));
    }

    internal void SetSourcesExpanded(bool expanded)
    {
        AreSourcesExpanded = expanded;
        OnPropertyChanged(nameof(AreSourcesExpanded));
    }

    internal void SetAdditionalDeliveryEnabled(bool enabled)
    {
        AdditionalDeliveryEnabled = OffersAdditionalDelivery && enabled;
        OnPropertyChanged(nameof(AdditionalDeliveryEnabled));
        NotifySummary();
        OnPropertyChanged(nameof(CanConfirm));
    }

    internal void SetBundleFolderName(string value)
    {
        if (StringComparer.Ordinal.Equals(BundleFolderName, value)) { return; }
        _nameEditReverted = false;
        BundleFolderName = value ?? string.Empty;
        _ = RefreshValidation();
        OnPropertyChanged(nameof(BundleFolderName));
    }

    internal void SetParentDirectory(string value)
    {
        ParentDirectory = value ?? string.Empty;
        _ = RefreshValidation();
        OnPropertyChanged(nameof(ParentDirectory));
    }

    internal async Task ConfirmLooseAsync(
        string outputPath,
        string? additionalOutputPath,
        bool outputPathUsesAutomaticName,
        bool additionalOutputPathUsesAutomaticName,
        bool prepareModeSpecific = true)
    {
        _ = RefreshValidation();
        if (!CanConfirm) { return; }

        if (prepareModeSpecific && !await PrepareModeSpecificAsync())
        {
            return;
        }

        OutputDeliveryRequest request = RequireOpenRequest();
        if (!await EnsureCurrentAsync(request))
        {
            return;
        }

        _ = RefreshValidation();
        if (!CanConfirm) { return; }

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
        if (!await EnsureCurrentAsync(request))
        {
            return;
        }

        CompositionOutputBundleIntent? intent = RefreshValidation();
        if (intent is null || !IsBundleDestinationValid)
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
        OnPropertyChanged(nameof(SourcesSummary));
        NotifySummary();
        _ = RefreshValidation();
    }

    private void Cancel()
    {
        _preparationGeneration++;
        _preserveCancelledDeliveryState |= IsOpen;
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

    private async Task<bool> EnsureCurrentAsync(OutputDeliveryRequest request)
    {
        bool current = request.IsCurrent() &&
            await _outputNaming.IsProposalCurrentAsync(request.Proposal, CancellationToken.None);
        if (!ReferenceEquals(_request, request) || !IsOpen)
        {
            return false;
        }

        if (current && request.IsCurrent())
        {
            return true;
        }

        IsBundleDestinationValid = false;
        ProposalIsCurrent = false;
        ValidationMessage = Text.OutputDeliveryStaleAcceptedSession;
        NotifyValidation();
        return false;
    }

    private CompositionOutputBundleIntent? RefreshValidation()
    {
        CompositionOutputBundleValidationIssue? nameIssue = _outputNaming.ValidateName(OutputFileName);
        IsOutputNameValid = nameIssue is null;
        if (!ProposalIsCurrent)
        {
            IsBundleDestinationValid = false;
            ValidationMessage = Text.OutputDeliveryStaleAcceptedSession;
            NotifyValidation();
            return null;
        }

        if (nameIssue is not null)
        {
            IsBundleDestinationValid = false;
            ValidationMessage = nameIssue.Message;
            NotifyValidation();
            return null;
        }

        if (!BundleEnabled || _request is null ||
            string.IsNullOrWhiteSpace(ParentDirectory) ||
            string.IsNullOrWhiteSpace(BundleFolderName))
        {
            IsBundleDestinationValid = false;
            ValidationMessage = BundleEnabled
                ? Text.OutputDeliveryDestinationRequired
                : string.Empty;
            NotifyValidation();
            return null;
        }

        try
        {
            CompositionOutputBundleIntent intent = _request.Proposal.CreateIntent(
                ParentDirectory,
                BundleFolderName,
                AdditionalDeliveryEnabled
                    ? _request.AdditionalDelivery?.DeliveryKind
                    : null,
                outputFileNameOverride: OutputFileNameUsesAutomaticName ? null : OutputFileName);
            ApplyValidation(_outputNaming.ValidateBundleDestination(intent));
            return intent;
        }
        catch (ArgumentException exception)
        {
            IsBundleDestinationValid = false;
            ValidationMessage = exception.Message;
            NotifyValidation();
            return null;
        }
    }

    private void ResetOutputFileName()
    {
        OutputFileName = CanonicalOutputFileName;
        _acceptedOutputFileName = OutputFileName;
        IsOutputFileNameEditing = false;
        OnPropertyChanged(nameof(OutputFileName));
        OnPropertyChanged(nameof(IsOutputFileNameEditing));
        OnPropertyChanged(nameof(OutputFileNameUsesAutomaticName));
    }

    private void ApplyValidation(CompositionOutputBundleDestinationValidation validation)
    {
        IsBundleDestinationValid = validation.IsValid;
        ValidationMessage = validation.Issues.Count == 0
            ? string.Empty
            : validation.Issues[0].Message;
        NotifyValidation();
    }

    private void NotifyValidation()
    {
        OnPropertyChanged(nameof(IsBundleDestinationValid));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationMessage));
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
        OnPropertyChanged(nameof(AreSourcesExpanded));
        OnPropertyChanged(nameof(SourcesSummary));
        OnPropertyChanged(nameof(BundleFolderName));
        OnPropertyChanged(nameof(ParentDirectory));
        OnPropertyChanged(nameof(BundleEnabled));
        OnPropertyChanged(nameof(IsBundleDestinationEditing));
        OnPropertyChanged(nameof(CanEditBundleDestination));
        OnPropertyChanged(nameof(AdditionalDeliveryEnabled));
        NotifySummary();
        NotifyValidation();
    }
}
