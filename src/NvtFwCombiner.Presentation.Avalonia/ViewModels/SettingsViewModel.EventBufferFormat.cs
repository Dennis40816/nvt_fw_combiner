using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Configuration;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record EventBufferFormatOutputPresentation(string Effect, string Applicability);

/// <summary>One editable row whose identity and declared output effect remain host-owned facts.</summary>
internal sealed partial class EventBufferFormatDraftRowViewModel : ObservableObject
{
    internal EventBufferFormatDraftRowViewModel(
        IReadOnlyList<EventBufferFormatIdentity> identities,
        EventBufferFormatDraftEntry? entry,
        Func<string?, EventBufferFormatOutputPresentation> formatOutputEffect,
        Func<ShellTextResources> textProvider)
    {
        IdentityChoices = identities ?? throw new ArgumentNullException(nameof(identities));
        _formatOutputEffect = formatOutputEffect ?? throw new ArgumentNullException(nameof(formatOutputEffect));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        SelectedIdentity = IdentityChoices.SingleOrDefault(identity =>
            StringComparer.Ordinal.Equals(identity.UniqueId, entry?.UniqueId));
        AliasName = entry?.AliasName;
        RecognitionValues = new ObservableCollection<int>(entry?.RecognitionValues ?? []);
        RecognitionValues.CollectionChanged += RecognitionValues_OnCollectionChanged;
        ApplyOutputEffect(SelectedIdentity?.UniqueId);
    }

    private readonly Func<string?, EventBufferFormatOutputPresentation> _formatOutputEffect;
    private readonly Func<ShellTextResources> _textProvider;

    public IReadOnlyList<EventBufferFormatIdentity> IdentityChoices { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UniqueId))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial EventBufferFormatIdentity? SelectedIdentity { get; set; }

    public string? UniqueId => SelectedIdentity?.UniqueId;

    public string DisplayName => SelectedIdentity?.DisplayName ?? string.Empty;

    [ObservableProperty]
    public partial string? AliasName { get; set; }

    public ObservableCollection<int> RecognitionValues { get; }

    [ObservableProperty]
    public partial string OutputEffect { get; private set; }

    [ObservableProperty]
    public partial string OutputApplicability { get; private set; }

    [ObservableProperty]
    public partial string RecognitionValueDraft { get; set; } = "0x";

    [ObservableProperty]
    public partial bool IsAddingRecognitionValue { get; private set; }

    public string AddRecognitionValueLabel => _textProvider().EventBufferFormatAddValueLabel;

    public string RemoveRecognitionValueLabel => _textProvider().EventBufferFormatRemoveValueLabel;

    public string RecognitionValueLabel => _textProvider().EventBufferFormatRecognitionValueLabel;

    public string IdentityHint => _textProvider().EventBufferFormatIdentityHint;

    public string AliasHint => _textProvider().EventBufferFormatAliasHint;

    public string RecognitionValuesHint => _textProvider().EventBufferFormatRecognitionValuesHint;

    [RelayCommand(CanExecute = nameof(CanEditRecognitionValues))]
    private void BeginAddRecognitionValue()
    {
        RecognitionValueDraft = "0x";
        RecognitionValueValidationMessage = string.Empty;
        IsAddingRecognitionValue = true;
    }

    [RelayCommand(CanExecute = nameof(CanEditRecognitionValues))]
    private void AddRecognitionValue()
    {
        string value = RecognitionValueDraft.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        if (!int.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int parsed))
        {
            RecognitionValueValidationMessage = _textProvider().EventBufferFormatInvalidHexLabel;
            return;
        }

        RecognitionValues.Add(parsed);
        RecognitionValueDraft = "0x";
        RecognitionValueValidationMessage = string.Empty;
        IsAddingRecognitionValue = false;
    }

    [RelayCommand(CanExecute = nameof(CanEditRecognitionValues))]
    private void CancelAddRecognitionValue()
    {
        RecognitionValueDraft = "0x";
        RecognitionValueValidationMessage = string.Empty;
        IsAddingRecognitionValue = false;
    }

    [RelayCommand(CanExecute = nameof(CanEditRecognitionValues))]
    private void RemoveRecognitionValue(int value)
    {
        _ = RecognitionValues.Remove(value);
    }

    [ObservableProperty]
    public partial string RecognitionValueValidationMessage { get; private set; } = string.Empty;

    internal EventBufferFormatDraftEntry ToDraft()
    {
        return new EventBufferFormatDraftEntry(UniqueId, AliasName, [.. RecognitionValues]);
    }

    partial void OnSelectedIdentityChanged(EventBufferFormatIdentity? value)
    {
        ApplyOutputEffect(value?.UniqueId);
    }

    internal void RefreshText()
    {
        OnPropertyChanged(nameof(AddRecognitionValueLabel));
        OnPropertyChanged(nameof(RemoveRecognitionValueLabel));
        OnPropertyChanged(nameof(RecognitionValueLabel));
        OnPropertyChanged(nameof(IdentityHint));
        OnPropertyChanged(nameof(AliasHint));
        OnPropertyChanged(nameof(RecognitionValuesHint));
        if (!string.IsNullOrEmpty(RecognitionValueValidationMessage))
        {
            RecognitionValueValidationMessage = _textProvider().EventBufferFormatInvalidHexLabel;
        }

        ApplyOutputEffect(SelectedIdentity?.UniqueId);
    }

    internal bool CanEditRecognitionValues { get; private set; } = true;

    internal void SetEditingEnabled(bool enabled)
    {
        CanEditRecognitionValues = enabled;
        if (!enabled)
        {
            IsAddingRecognitionValue = false;
        }

        BeginAddRecognitionValueCommand.NotifyCanExecuteChanged();
        AddRecognitionValueCommand.NotifyCanExecuteChanged();
        CancelAddRecognitionValueCommand.NotifyCanExecuteChanged();
        RemoveRecognitionValueCommand.NotifyCanExecuteChanged();
    }

    private void ApplyOutputEffect(string? uniqueId)
    {
        EventBufferFormatOutputPresentation output = _formatOutputEffect(uniqueId);
        OutputEffect = output.Effect;
        OutputApplicability = output.Applicability;
    }

    private void RecognitionValues_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(RecognitionValues));
    }
}

internal sealed partial class SettingsViewModel
{
    private IEventBufferFormatConfigurationSession? _eventBufferFormatConfigurationSession;
    private IReadOnlyList<EventBufferFormatDraftEntry?>? _eventBufferFormatBaseline;
    private EventBufferFormatConfigurationFailure? _eventBufferFormatOperationFailure;
    private IReadOnlyList<EventBufferFormatConfigurationIssue> _eventBufferFormatOperationIssues = [];
    private bool _eventBufferFormatLoadStarted;
    private bool _eventBufferFormatReapplyFailed;

    internal Func<Task<bool>>? ReapplyEventBufferFormatAsync { get; set; }

    public ObservableCollection<EventBufferFormatDraftRowViewModel> EventBufferFormatRows { get; } = [];

    public IReadOnlyList<EventBufferFormatIdentity> EventBufferFormatIdentities =>
        _eventBufferFormatConfigurationSession?.Catalog.Identities ?? [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EventBufferFormatFooterStatus))]
    [NotifyPropertyChangedFor(nameof(EventBufferFormatDraftStatus))]
    [NotifyPropertyChangedFor(nameof(CanSaveEventBufferFormat))]
    [NotifyPropertyChangedFor(nameof(CanDiscardEventBufferFormat))]
    [NotifyPropertyChangedFor(nameof(CanRestoreEventBufferFormat))]
    [NotifyPropertyChangedFor(nameof(CanConfirmEventBufferFormatClose))]
    [NotifyPropertyChangedFor(nameof(HasEventBufferFormatUnsavedChanges))]
    public partial bool IsEventBufferFormatLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EventBufferFormatFooterStatus))]
    [NotifyPropertyChangedFor(nameof(EventBufferFormatDraftStatus))]
    [NotifyPropertyChangedFor(nameof(CanSaveEventBufferFormat))]
    [NotifyPropertyChangedFor(nameof(CanDiscardEventBufferFormat))]
    [NotifyPropertyChangedFor(nameof(CanRestoreEventBufferFormat))]
    [NotifyPropertyChangedFor(nameof(CanConfirmEventBufferFormatClose))]
    [NotifyPropertyChangedFor(nameof(HasEventBufferFormatUnsavedChanges))]
    public partial bool IsEventBufferFormatAvailable { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EventBufferFormatFooterStatus))]
    [NotifyPropertyChangedFor(nameof(EventBufferFormatDraftStatus))]
    [NotifyPropertyChangedFor(nameof(CanSaveEventBufferFormat))]
    [NotifyPropertyChangedFor(nameof(CanDiscardEventBufferFormat))]
    [NotifyPropertyChangedFor(nameof(CanRestoreEventBufferFormat))]
    [NotifyPropertyChangedFor(nameof(CanConfirmEventBufferFormatClose))]
    [NotifyPropertyChangedFor(nameof(HasEventBufferFormatUnsavedChanges))]
    public partial bool IsEventBufferFormatBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirmEventBufferFormatClose))]
    public partial bool IsEventBufferFormatCloseConfirmationOpen { get; private set; }

    [ObservableProperty]
    public partial string EventBufferFormatStatus { get; private set; } = string.Empty;

    public bool HasEventBufferFormatUnsavedChanges => IsEventBufferFormatAvailable &&
        !DraftEquals(EventBufferFormatRows.Select(static row => row.ToDraft()), _eventBufferFormatBaseline);

    public bool CanSaveEventBufferFormat => IsEventBufferFormatAvailable && !IsEventBufferFormatBusy &&
        (HasEventBufferFormatUnsavedChanges || IsEventBufferFormatMissingOrInvalid);

    public bool CanDiscardEventBufferFormat => HasEventBufferFormatUnsavedChanges && !IsEventBufferFormatBusy;

    public bool CanRestoreEventBufferFormat => IsEventBufferFormatAvailable && !IsEventBufferFormatBusy;

    public bool CanConfirmEventBufferFormatClose => IsEventBufferFormatCloseConfirmationOpen && !IsEventBufferFormatBusy;

    public bool CanReloadEventBufferFormat => _eventBufferFormatConfigurationSessionFactory is not null &&
        !IsEventBufferFormatLoading && !IsEventBufferFormatBusy && !HasEventBufferFormatUnsavedChanges &&
        !IsEventBufferFormatCloseConfirmationOpen;

    public string EventBufferFormatFooterStatus => IsEventBufferFormatLoading
        ? _textProvider().EventBufferFormatLoadingLabel
        : IsEventBufferFormatBusy
        ? _textProvider().EventBufferFormatSavingLabel
        : HasEventBufferFormatUnsavedChanges
            ? _textProvider().EventBufferFormatUnsavedChangesLabel
            : _textProvider().EventBufferFormatNoUnsavedChangesLabel;

    /// <summary>Editor footer state, distinct from configuration admission and firmware runtime readiness.</summary>
    public string EventBufferFormatDraftStatus => EventBufferFormatFooterStatus;

    public bool IsEventBufferFormatMissingOrInvalid => _eventBufferFormatConfigurationSession?.Current.Status is
        EventBufferFormatConfigurationStatus.Missing or EventBufferFormatConfigurationStatus.Invalid;

    internal Task EventBufferFormatLoadTask { get; private set; } = Task.CompletedTask;

    internal event EventHandler? EventBufferFormatCloseAccepted;

    internal bool RequestSettingsClose()
    {
        if (IsEventBufferFormatBusy || IsToolchainBusy)
        {
            return false;
        }

        if (!HasEventBufferFormatUnsavedChanges)
        {
            if (HasToolchainUnsavedChanges)
            {
                SelectedSection = SettingsSection.Toolchain;
                IsToolchainCloseConfirmationOpen = true;
                return false;
            }
            InvalidateToolchainOperations();
            return true;
        }

        SelectedSection = SettingsSection.EventBufferFormat;
        IsEventBufferFormatCloseConfirmationOpen = true;
        return false;
    }

    private void BeginEventBufferFormatLoad()
    {
        if (_eventBufferFormatLoadStarted)
        {
            return;
        }

        _eventBufferFormatLoadStarted = true;
        EventBufferFormatLoadTask = LoadEventBufferFormatAsync();
    }

    private async Task LoadEventBufferFormatAsync(bool reapply = false)
    {
        if (_eventBufferFormatConfigurationSessionFactory is null)
        {
            return;
        }

        IsEventBufferFormatLoading = true;
        try
        {
            _eventBufferFormatConfigurationSession ??= await _eventBufferFormatConfigurationSessionFactory(CancellationToken.None);
            EventBufferFormatConfigurationOperationResult result = await _eventBufferFormatConfigurationSession
                .ReloadAsync(CancellationToken.None);
            ApplyEventBufferFormatState(result, preferDefaultsForUnavailable: true);
            if (reapply)
            {
                await ReapplyEventBufferFormatStateAsync(preferDefaultsForUnavailable: true);
            }
        }
        catch (Exception)
        {
            _eventBufferFormatLoadStarted = false;
            EventBufferFormatStatus = _textProvider().EventBufferFormatLoadFailedLabel;
        }
        finally
        {
            IsEventBufferFormatLoading = false;
            ReloadEventBufferFormatCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanReloadEventBufferFormat))]
    private async Task ReloadEventBufferFormatAsync()
    {
        if (!CanReloadEventBufferFormat) { return; }
        IsEventBufferFormatBusy = true;
        try
        {
            await LoadEventBufferFormatAsync(reapply: true);
        }
        finally
        {
            IsEventBufferFormatBusy = false;
        }
    }

    private async Task ReapplyEventBufferFormatStateAsync(bool preferDefaultsForUnavailable)
    {
        long generation = _eventBufferFormatConfigurationSession?.Current.Generation ?? 0;
        try
        {
            _eventBufferFormatReapplyFailed = ReapplyEventBufferFormatAsync is not null &&
                !await ReapplyEventBufferFormatAsync();
        }
        catch (Exception)
        {
            // The configuration publication is already complete; report refresh failure separately.
            _eventBufferFormatReapplyFailed = true;
        }
        if (_eventBufferFormatConfigurationSession?.Current is { } current && current.Generation != generation)
        {
            ApplyEventBufferFormatState(new(current, null, []), preferDefaultsForUnavailable);
        }
        RefreshEventBufferFormatLabels();
    }

    [RelayCommand(CanExecute = nameof(CanSaveEventBufferFormat))]
    private async Task SaveEventBufferFormatAsync()
    {
        if (!CanSaveEventBufferFormat || _eventBufferFormatConfigurationSession is null)
        {
            return;
        }

        IsEventBufferFormatBusy = true;
        try
        {
            EventBufferFormatConfigurationOperationResult result = await _eventBufferFormatConfigurationSession.SaveAsync(
                [.. EventBufferFormatRows.Select(static row => row.ToDraft())], CancellationToken.None);
            if (result.Succeeded)
            {
                _eventBufferFormatReapplyFailed = false;
                ApplyEventBufferFormatState(result, preferDefaultsForUnavailable: false);
                await ReapplyEventBufferFormatStateAsync(preferDefaultsForUnavailable: false);
            }
            else
            {
                _eventBufferFormatOperationFailure = result.Failure;
                _eventBufferFormatOperationIssues = result.Issues;
                EventBufferFormatStatus = FormatEventBufferFormatOperationFailure();
            }
        }
        finally
        {
            IsEventBufferFormatBusy = false;
            SetEventBufferFormatRowsEditingEnabled(true);
            SaveEventBufferFormatCommand.NotifyCanExecuteChanged();
            RestoreEventBufferFormatDefaultsCommand.NotifyCanExecuteChanged();
            DiscardEventBufferFormatChangesCommand.NotifyCanExecuteChanged();
            ConfirmEventBufferFormatCloseCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestoreEventBufferFormat))]
    private void RestoreEventBufferFormatDefaults()
    {
        if (CanRestoreEventBufferFormat && _eventBufferFormatConfigurationSession is not null)
        {
            ReplaceEventBufferFormatRows(_eventBufferFormatConfigurationSession.CreateDefaultsDraft());
        }
    }

    [RelayCommand(CanExecute = nameof(CanDiscardEventBufferFormat))]
    private void DiscardEventBufferFormatChanges()
    {
        if (!CanDiscardEventBufferFormat)
        {
            return;
        }

        ReplaceEventBufferFormatRows(_eventBufferFormatBaseline ??
            _eventBufferFormatConfigurationSession?.CreateDefaultsDraft() ?? []);
    }

    [RelayCommand(CanExecute = nameof(CanConfirmEventBufferFormatClose))]
    private void ConfirmEventBufferFormatClose()
    {
        if (!CanConfirmEventBufferFormatClose)
        {
            return;
        }

        DiscardEventBufferFormatChanges();
        IsEventBufferFormatCloseConfirmationOpen = false;
        if (RequestSettingsClose())
        {
            EventBufferFormatCloseAccepted?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void CancelEventBufferFormatClose()
    {
        IsEventBufferFormatCloseConfirmationOpen = false;
    }

    private void ApplyEventBufferFormatState(
        EventBufferFormatConfigurationOperationResult result,
        bool preferDefaultsForUnavailable)
    {
        EventBufferFormatConfigurationState state = result.State;
        _eventBufferFormatOperationFailure = result.Failure;
        _eventBufferFormatOperationIssues = result.Issues;
        IsEventBufferFormatAvailable = true;
        IReadOnlyList<EventBufferFormatDraftEntry?>? saved = _eventBufferFormatConfigurationSession?.CreateSavedDraft();
        _eventBufferFormatBaseline = saved ?? _eventBufferFormatConfigurationSession?.CreateDefaultsDraft();
        IReadOnlyList<EventBufferFormatDraftEntry?> editor = state.Status == EventBufferFormatConfigurationStatus.Ready
            ? saved ?? []
            : preferDefaultsForUnavailable
                ? _eventBufferFormatConfigurationSession?.CreateDefaultsDraft() ?? []
                : _eventBufferFormatBaseline ?? [];
        ReplaceEventBufferFormatRows(editor);
        EventBufferFormatStatus = state.Status switch
        {
            EventBufferFormatConfigurationStatus.Ready => _textProvider().EventBufferFormatSavedLabel,
            EventBufferFormatConfigurationStatus.Missing => _textProvider().EventBufferFormatMissingLabel,
            EventBufferFormatConfigurationStatus.Invalid => _textProvider().EventBufferFormatInvalidLabel,
            EventBufferFormatConfigurationStatus.NotLoaded => _textProvider().EventBufferFormatInvalidLabel,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
        if (result.Failure is not null)
        {
            EventBufferFormatStatus = FormatEventBufferFormatOperationFailure();
        }
        OnPropertyChanged(nameof(IsEventBufferFormatMissingOrInvalid));
        OnPropertyChanged(nameof(CanSaveEventBufferFormat));
        OnPropertyChanged(nameof(CanDiscardEventBufferFormat));
    }

    private void ReplaceEventBufferFormatRows(IEnumerable<EventBufferFormatDraftEntry?> draft)
    {
        EventBufferFormatRows.Clear();
        if (_eventBufferFormatConfigurationSession is null)
        {
            return;
        }

        foreach (EventBufferFormatDraftEntry? entry in draft)
        {
            var row = new EventBufferFormatDraftRowViewModel(
                _eventBufferFormatConfigurationSession.Catalog.Identities,
                entry,
                FormatEventBufferFormatEffects,
                _textProvider);
            row.SetEditingEnabled(!IsEventBufferFormatBusy);
            row.PropertyChanged += EventBufferFormatRow_OnPropertyChanged;
            EventBufferFormatRows.Add(row);
        }

        OnPropertyChanged(nameof(HasEventBufferFormatUnsavedChanges));
        OnPropertyChanged(nameof(CanSaveEventBufferFormat));
        OnPropertyChanged(nameof(CanDiscardEventBufferFormat));
        OnPropertyChanged(nameof(EventBufferFormatDraftStatus));
        SaveEventBufferFormatCommand.NotifyCanExecuteChanged();
        DiscardEventBufferFormatChangesCommand.NotifyCanExecuteChanged();
        RestoreEventBufferFormatDefaultsCommand.NotifyCanExecuteChanged();
        ReloadEventBufferFormatCommand.NotifyCanExecuteChanged();
    }

    private EventBufferFormatOutputPresentation FormatEventBufferFormatEffects(string? uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            return new(string.Empty, string.Empty);
        }

        EventBufferFormatOutputEffect[] effects = [.. _eventBufferFormatConfigurationSession!.Catalog.OutputEffects
            .Where(effect => StringComparer.Ordinal.Equals(effect.UniqueId, uniqueId))];
        return effects.Length == 0
            ? new(string.Empty, string.Empty)
            : effects.Select(static effect => (effect.AddressSpaceId, effect.TpBRange.Start))
                .Distinct()
                .Count() == 1
                ? new(
                    $"{_textProvider().EventBufferFormatOutputStartLabel}\n0x{effects[0].TpBRange.Start:X}",
                    $"{_textProvider().EventBufferFormatOutputAppliesToLabel} {string.Join(" / ", effects.Select(static effect => effect.MemberId).Distinct(StringComparer.Ordinal))} · AB Code")
                : new(
                    string.Join("\n", effects.Select(effect =>
                        $"{_textProvider().EventBufferFormatOutputStartLabel} · 0x{effect.TpBRange.Start:X}")),
                    string.Join("\n", effects.Select(static effect => $"{effect.MemberId}/{effect.MapId}")));
    }

    private void EventBufferFormatRow_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EventBufferFormatDraftRowViewModel.AliasName) or
            nameof(EventBufferFormatDraftRowViewModel.RecognitionValues) or
            nameof(EventBufferFormatDraftRowViewModel.SelectedIdentity))
        {
            OnPropertyChanged(nameof(HasEventBufferFormatUnsavedChanges));
            OnPropertyChanged(nameof(CanSaveEventBufferFormat));
            OnPropertyChanged(nameof(CanDiscardEventBufferFormat));
            OnPropertyChanged(nameof(EventBufferFormatDraftStatus));
            SaveEventBufferFormatCommand.NotifyCanExecuteChanged();
            DiscardEventBufferFormatChangesCommand.NotifyCanExecuteChanged();
            RestoreEventBufferFormatDefaultsCommand.NotifyCanExecuteChanged();
            ReloadEventBufferFormatCommand.NotifyCanExecuteChanged();
        }
    }

    private void RefreshEventBufferFormatLabels()
    {
        if (!IsEventBufferFormatAvailable || _eventBufferFormatConfigurationSession is null)
        {
            return;
        }

        foreach (EventBufferFormatDraftRowViewModel row in EventBufferFormatRows)
        {
            row.RefreshText();
        }

        EventBufferFormatStatus = _eventBufferFormatOperationFailure is not null
            ? FormatEventBufferFormatOperationFailure()
            : _eventBufferFormatConfigurationSession.Current.Status switch
            {
                EventBufferFormatConfigurationStatus.Ready => _eventBufferFormatReapplyFailed
                    ? _textProvider().EventBufferFormatReapplyFailedLabel
                    : _textProvider().EventBufferFormatSavedLabel,
                EventBufferFormatConfigurationStatus.Missing => _textProvider().EventBufferFormatMissingLabel,
                EventBufferFormatConfigurationStatus.Invalid => _textProvider().EventBufferFormatInvalidLabel,
                EventBufferFormatConfigurationStatus.NotLoaded => _textProvider().EventBufferFormatInvalidLabel,
                _ => throw new ArgumentOutOfRangeException(),
            };
        OnPropertyChanged(nameof(EventBufferFormatFooterStatus));
        OnPropertyChanged(nameof(EventBufferFormatDraftStatus));
    }

    partial void OnIsEventBufferFormatBusyChanged(bool value)
    {
        ReloadEventBufferFormatCommand.NotifyCanExecuteChanged();
        SetEventBufferFormatRowsEditingEnabled(!value);
        SaveEventBufferFormatCommand.NotifyCanExecuteChanged();
        RestoreEventBufferFormatDefaultsCommand.NotifyCanExecuteChanged();
        DiscardEventBufferFormatChangesCommand.NotifyCanExecuteChanged();
        ConfirmEventBufferFormatCloseCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsEventBufferFormatCloseConfirmationOpenChanged(bool value)
    {
        ReloadEventBufferFormatCommand.NotifyCanExecuteChanged();
        ConfirmEventBufferFormatCloseCommand.NotifyCanExecuteChanged();
    }

    private void SetEventBufferFormatRowsEditingEnabled(bool enabled)
    {
        foreach (EventBufferFormatDraftRowViewModel row in EventBufferFormatRows)
        {
            row.SetEditingEnabled(enabled);
        }
    }

    private string FormatEventBufferFormatOperationFailure()
    {
        return _eventBufferFormatOperationFailure switch
        {
            EventBufferFormatConfigurationFailure.SaveFailed => _textProvider().EventBufferFormatSaveFailedLabel,
            EventBufferFormatConfigurationFailure.ReadFailed => _textProvider().EventBufferFormatReadFailedLabel,
            EventBufferFormatConfigurationFailure.ScopeMismatch => _textProvider().EventBufferFormatScopeMismatchLabel,
            EventBufferFormatConfigurationFailure.InvalidDocument => _textProvider().EventBufferFormatInvalidDocumentLabel,
            EventBufferFormatConfigurationFailure.Missing => _textProvider().EventBufferFormatMissingLabel,
            EventBufferFormatConfigurationFailure.InvalidValues => FormatEventBufferFormatIssues(_eventBufferFormatOperationIssues),
            null => _textProvider().EventBufferFormatInvalidLabel,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private string FormatEventBufferFormatIssues(IReadOnlyList<EventBufferFormatConfigurationIssue> issues)
    {
        return issues.Count == 0
            ? _textProvider().EventBufferFormatInvalidLabel
            : string.Join(" ", issues.Select(issue => issue.Code switch
        {
            EventBufferFormatConfigurationIssueCode.MissingEntries => _textProvider().EventBufferFormatEntryRequiredLabel,
            EventBufferFormatConfigurationIssueCode.MissingEntry =>
                $"{_textProvider().EventBufferFormatEntryLabel} {issue.EntryIndex + 1}: {_textProvider().EventBufferFormatEntryRequiredLabel}",
            EventBufferFormatConfigurationIssueCode.UnknownIdentity =>
                $"{_textProvider().EventBufferFormatEntryLabel} {issue.EntryIndex + 1}: {_textProvider().EventBufferFormatIdentityRequiredLabel}",
            EventBufferFormatConfigurationIssueCode.ValueOutOfRange =>
                $"{_textProvider().EventBufferFormatEntryLabel} {issue.EntryIndex + 1}: {issue.RecognitionValue} {_textProvider().EventBufferFormatValueOutOfRangeLabel}",
            EventBufferFormatConfigurationIssueCode.MissingValues =>
                $"{_textProvider().EventBufferFormatEntryLabel} {issue.EntryIndex + 1}: {_textProvider().EventBufferFormatValuesRequiredLabel}",
            EventBufferFormatConfigurationIssueCode.DuplicateValue =>
                $"{_textProvider().EventBufferFormatEntryLabel} {issue.EntryIndex + 1}: {_textProvider().EventBufferFormatDuplicateValueLabel} 0x{issue.RecognitionValue:X2}.",
            EventBufferFormatConfigurationIssueCode.ConflictingValue =>
                $"{_textProvider().EventBufferFormatEntryLabel} {issue.EntryIndex + 1}: 0x{issue.RecognitionValue:X2} {_textProvider().EventBufferFormatConflictingValueLabel}",
            EventBufferFormatConfigurationIssueCode.DuplicateIdentity =>
                $"{_textProvider().EventBufferFormatEntryLabel} {issue.EntryIndex + 1}: {_textProvider().EventBufferFormatDuplicateIdentityLabel}",
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
        }));
    }

    private static bool DraftEquals(
        IEnumerable<EventBufferFormatDraftEntry?> left,
        IReadOnlyList<EventBufferFormatDraftEntry?>? right)
    {
        EventBufferFormatDraftEntry?[] leftEntries = [.. left];
        return right is not null && leftEntries.Length == right.Count && leftEntries.Zip(right).All(pair =>
            pair.First?.UniqueId == pair.Second?.UniqueId &&
            pair.First?.AliasName == pair.Second?.AliasName &&
            (pair.First?.RecognitionValues ?? []).SequenceEqual(pair.Second?.RecognitionValues ?? []));
    }
}
