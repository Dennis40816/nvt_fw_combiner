using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Configuration;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class SettingsViewModel
{
    private readonly Func<CancellationToken, Task<IToolchainRuntimeConfigurationSession>>? _toolchainSessionFactory;
    private IToolchainRuntimeConfigurationSession? _toolchainSession;
    private ToolchainRuntimeSelection? _toolchainBaseline;
    private ToolchainRuntimeSelection? _toolchainDraft;
    private ToolchainRuntimeCandidateInspection? _toolchainInspection;
    private IReadOnlyList<ToolchainRuntimeConfigurationIssue> _toolchainIssues = [];
    private bool _toolchainLoadStarted;
    internal Func<CancellationToken, Task>? ToolchainAppliedAsync { get; set; }
    internal event EventHandler? ToolchainBrowseRequested;
    internal Task ToolchainLoadTask { get; private set; } = Task.CompletedTask;
    internal long ToolchainOperationGeneration { get; private set; }

    public ObservableCollection<ToolchainRuntimeCandidateInspection> ToolchainCandidates { get; } = [];

    [ObservableProperty]
    public partial bool IsToolchainBusy { get; private set; }

    [ObservableProperty]
    public partial bool IsToolchainCloseConfirmationOpen { get; private set; }

    [ObservableProperty]
    public partial string ToolchainOperationStatus { get; private set; } = string.Empty;

    public bool IsBundledToolchainSelected => _toolchainDraft?.Source == ToolchainRuntimeSource.Bundled;
    public bool IsUserToolchainSelected => _toolchainDraft?.Source == ToolchainRuntimeSource.User;
    public bool HasToolchainUnsavedChanges => _toolchainDraft != _toolchainBaseline;
    public bool CanEditToolchain => _toolchainSession is not null && !IsToolchainBusy && !IsToolchainCloseConfirmationOpen;
    public bool CanSaveToolchain => CanEditToolchain && HasToolchainUnsavedChanges &&
        (IsBundledToolchainSelected || _toolchainInspection is { Verification: ToolchainRuntimeCandidateVerification.Verified, Identity: not null });
    public bool CanDiscardToolchain => CanEditToolchain && HasToolchainUnsavedChanges;
    public bool HasToolchainIssues => _toolchainIssues.Count > 0 || _toolchainInspection?.Verification == ToolchainRuntimeCandidateVerification.Rejected;
    public string ToolchainIssueText => string.Join("\n", _toolchainIssues.Select(issue => $"{issue.Code}: {issue.Message}"));
    public string ToolchainRuntime => _toolchainInspection?.Identity is { } identity ? System.IO.Path.GetFileName(identity.Path) : "—";
    public string ToolchainVersion => _toolchainInspection?.Identity?.FileVersion ?? "—";
    public string ToolchainArchitecture => _toolchainInspection?.Identity?.Architecture ?? "—";
    public string ToolchainSource => IsBundledToolchainSelected ? _textProvider().ToolchainIncludedLabel : _toolchainDraft?.Path ?? "—";
    public string ToolchainVerification => HasToolchainIssues ? _textProvider().ToolchainFailedLabel :
        _toolchainInspection?.Verification == ToolchainRuntimeCandidateVerification.Verified ? _textProvider().ToolchainVerifiedLabel :
        IsBundledToolchainSelected && _toolchainInspection?.Identity is not null ? _textProvider().ToolchainIncludedLabel : _textProvider().ToolchainUnknownLabel;
    public string ToolchainDraftStatus => IsToolchainBusy ? _textProvider().EventBufferFormatLoadingLabel :
        HasToolchainUnsavedChanges ? _textProvider().EventBufferFormatUnsavedChangesLabel : _textProvider().EventBufferFormatNoUnsavedChangesLabel;

    private void BeginToolchainLoad()
    {
        if (_toolchainLoadStarted || HasToolchainUnsavedChanges || _toolchainSessionFactory is null) { return; }
        _toolchainLoadStarted = true;
        ToolchainLoadTask = LoadToolchainAsync();
    }

    private async Task LoadToolchainAsync()
    {
        long operation = ++ToolchainOperationGeneration;
        IsToolchainBusy = true;
        try
        {
            _toolchainSession ??= await _toolchainSessionFactory!(CancellationToken.None);
            ToolchainRuntimeConfigurationOperationResult result = await _toolchainSession.ReloadAsync(CancellationToken.None);
            if (operation != ToolchainOperationGeneration) { return; }
            _toolchainBaseline = result.Snapshot.RequestedSelection;
            _toolchainDraft = _toolchainBaseline;
            _toolchainIssues = result.Issues;
            await InspectToolchainDraftAsync(operation, result.Snapshot.Generation);
        }
        catch (Exception)
        {
            if (operation == ToolchainOperationGeneration) { ToolchainOperationStatus = _textProvider().ToolchainOperationFailedLabel; _toolchainLoadStarted = false; }
        }
        finally { if (operation == ToolchainOperationGeneration) { IsToolchainBusy = false; RefreshToolchainLabels(); } }
    }

    private async Task InspectToolchainDraftAsync(long operation, long generation)
    {
        ToolchainRuntimeCandidateInspection? inspection = _toolchainDraft switch
        {
            { Source: ToolchainRuntimeSource.Bundled } => await _toolchainSession!.InspectBundledAsync(CancellationToken.None),
            { Source: ToolchainRuntimeSource.User, Path: { } path } => await _toolchainSession!.InspectAsync(path, CancellationToken.None),
            _ => null,
        };
        if (operation != ToolchainOperationGeneration || generation != _toolchainSession!.Current.Generation) { return; }
        _toolchainInspection = inspection;
        _toolchainIssues = [.. _toolchainIssues, .. inspection?.Issues ?? []];
        RefreshToolchainLabels();
    }

    [RelayCommand(CanExecute = nameof(CanEditToolchain))]
    private async Task SelectBundledToolchainAsync()
    {
        if (!CanEditToolchain) { return; }
        _toolchainDraft = new(ToolchainRuntimeSource.Bundled);
        _toolchainInspection = null;
        _toolchainIssues = [];
        ToolchainOperationStatus = string.Empty;
        long operation = ++ToolchainOperationGeneration;
        IsToolchainBusy = true;
        try { await InspectToolchainDraftAsync(operation, _toolchainSession!.Current.Generation); }
        catch (Exception) { if (operation == ToolchainOperationGeneration) { ToolchainOperationStatus = _textProvider().ToolchainOperationFailedLabel; } }
        finally { if (operation == ToolchainOperationGeneration) { IsToolchainBusy = false; RefreshToolchainLabels(); } }
    }

    [RelayCommand(CanExecute = nameof(CanEditToolchain))]
    private void SelectUserToolchain()
    {
        if (!CanEditToolchain || IsUserToolchainSelected) { return; }
        ++ToolchainOperationGeneration;
        _toolchainDraft = new(ToolchainRuntimeSource.User);
        _toolchainInspection = null;
        _toolchainIssues = [];
        ToolchainOperationStatus = string.Empty;
        RefreshToolchainLabels();
    }

    [RelayCommand(CanExecute = nameof(CanEditToolchain))]
    private void BrowseToolchain()
    {
        if (CanEditToolchain) { ToolchainBrowseRequested?.Invoke(this, EventArgs.Empty); }
    }

    internal async Task InspectToolchainPathAsync(string path)
    {
        if (!CanEditToolchain) { return; }
        long operation = ++ToolchainOperationGeneration;
        long generation = _toolchainSession!.Current.Generation;
        _toolchainDraft = new(ToolchainRuntimeSource.User, path);
        _toolchainInspection = null;
        _toolchainIssues = [];
        ToolchainOperationStatus = string.Empty;
        IsToolchainBusy = true;
        try
        {
            ToolchainRuntimeCandidateInspection inspection = await _toolchainSession.InspectAsync(path, CancellationToken.None);
            if (operation != ToolchainOperationGeneration || generation != _toolchainSession.Current.Generation) { return; }
            ApplyToolchainCandidate(inspection, path);
        }
        catch (Exception) { if (operation == ToolchainOperationGeneration) { ToolchainOperationStatus = _textProvider().ToolchainOperationFailedLabel; } }
        finally { if (operation == ToolchainOperationGeneration) { IsToolchainBusy = false; RefreshToolchainLabels(); } }
    }

    [RelayCommand(CanExecute = nameof(CanEditToolchain))]
    private async Task DetectToolchainAsync()
    {
        if (!CanEditToolchain) { return; }
        long operation = ++ToolchainOperationGeneration;
        long generation = _toolchainSession!.Current.Generation;
        IsToolchainBusy = true;
        ToolchainOperationStatus = string.Empty;
        try
        {
            IReadOnlyList<ToolchainRuntimeCandidateInspection> candidates = await _toolchainSession.DetectAsync(CancellationToken.None);
            if (operation != ToolchainOperationGeneration || generation != _toolchainSession.Current.Generation) { return; }
            ToolchainCandidates.Clear();
            foreach (ToolchainRuntimeCandidateInspection candidate in candidates)
            {
                if (candidate.Identity is not null && candidate.Verification == ToolchainRuntimeCandidateVerification.Verified)
                {
                    ToolchainCandidates.Add(candidate);
                }
            }
            ToolchainRuntimeConfigurationIssue[] rejectedIssues = [.. candidates.SelectMany(static candidate => candidate.Issues)];
            if (rejectedIssues.Length > 0)
            {
                ToolchainOperationStatus = string.Join(Environment.NewLine,
                    rejectedIssues.Select(static issue => $"{issue.Code}: {issue.Message}"));
            }
            else if (ToolchainCandidates.Count == 0)
            {
                ToolchainOperationStatus = _textProvider().ToolchainNoCandidatesLabel;
            }
        }
        catch (Exception) { if (operation == ToolchainOperationGeneration) { ToolchainOperationStatus = _textProvider().ToolchainOperationFailedLabel; } }
        finally { if (operation == ToolchainOperationGeneration) { IsToolchainBusy = false; RefreshToolchainLabels(); } }
    }

    [RelayCommand(CanExecute = nameof(CanEditToolchain))]
    private void SelectToolchainCandidate(ToolchainRuntimeCandidateInspection candidate)
    {
        if (!CanEditToolchain) { return; }
        ++ToolchainOperationGeneration;
        ApplyToolchainCandidate(candidate, candidate.Identity?.Path);
    }

    private void ApplyToolchainCandidate(ToolchainRuntimeCandidateInspection candidate, string? path)
    {
        _toolchainDraft = new(ToolchainRuntimeSource.User, candidate.Identity?.Path ?? path,
            candidate.Verification == ToolchainRuntimeCandidateVerification.Verified ? candidate.Identity?.Sha256 : null);
        _toolchainInspection = candidate;
        _toolchainIssues = candidate.Issues;
        RefreshToolchainLabels();
    }

    [RelayCommand(CanExecute = nameof(CanSaveToolchain))]
    private async Task SaveToolchainAsync()
    {
        if (!CanSaveToolchain) { return; }
        long operation = ++ToolchainOperationGeneration;
        IsToolchainBusy = true;
        try
        {
            ToolchainRuntimeConfigurationOperationResult result = await _toolchainSession!.SaveAsync(_toolchainDraft!, CancellationToken.None);
            if (operation != ToolchainOperationGeneration) { return; }
            _toolchainIssues = result.Issues;
            if (result.Succeeded)
            {
                _toolchainBaseline = _toolchainDraft = result.Snapshot.RequestedSelection;
                ToolchainOperationStatus = string.Empty;
                try { if (ToolchainAppliedAsync is not null) { await ToolchainAppliedAsync(CancellationToken.None); } }
                catch (Exception) { if (operation == ToolchainOperationGeneration) { ToolchainOperationStatus = _textProvider().ToolchainRefreshFailedLabel; } }
            }
        }
        catch (Exception) { if (operation == ToolchainOperationGeneration) { ToolchainOperationStatus = _textProvider().ToolchainOperationFailedLabel; } }
        finally { if (operation == ToolchainOperationGeneration) { IsToolchainBusy = false; RefreshToolchainLabels(); } }
    }

    [RelayCommand(CanExecute = nameof(CanDiscardToolchain))]
    private async Task DiscardToolchainChangesAsync()
    {
        if (!CanDiscardToolchain) { return; }
        _toolchainDraft = _toolchainBaseline;
        _toolchainInspection = null;
        _toolchainIssues = _toolchainSession!.Current.Issues;
        ToolchainOperationStatus = string.Empty;
        IsToolchainBusy = true;
        long operation = ++ToolchainOperationGeneration;
        try { await InspectToolchainDraftAsync(operation, _toolchainSession.Current.Generation); }
        catch (Exception) { if (operation == ToolchainOperationGeneration) { ToolchainOperationStatus = _textProvider().ToolchainOperationFailedLabel; } }
        finally { if (operation == ToolchainOperationGeneration) { IsToolchainBusy = false; RefreshToolchainLabels(); } }
    }

    [RelayCommand]
    private void CancelToolchainClose()
    {
        IsToolchainCloseConfirmationOpen = false;
    }

    [RelayCommand]
    private async Task ConfirmToolchainCloseAsync()
    {
        if (!IsToolchainCloseConfirmationOpen || IsToolchainBusy) { return; }
        IsToolchainCloseConfirmationOpen = false;
        await DiscardToolchainChangesAsync();
        if (RequestSettingsClose()) { EventBufferFormatCloseAccepted?.Invoke(this, EventArgs.Empty); }
    }

    internal void InvalidateToolchainOperations()
    {
        ++ToolchainOperationGeneration;
        _toolchainLoadStarted = false;
        IsToolchainBusy = false;
    }

    internal void ReportToolchainBrowseFailure()
    {
        ToolchainOperationStatus = _textProvider().ToolchainOperationFailedLabel;
    }

    partial void OnIsToolchainBusyChanged(bool value) => RefreshToolchainLabels();
    partial void OnIsToolchainCloseConfirmationOpenChanged(bool value) => RefreshToolchainLabels();

    private void RefreshToolchainLabels()
    {
        foreach (string property in new[] { nameof(IsBundledToolchainSelected), nameof(IsUserToolchainSelected), nameof(HasToolchainUnsavedChanges),
            nameof(CanEditToolchain), nameof(CanSaveToolchain), nameof(CanDiscardToolchain), nameof(HasToolchainIssues), nameof(ToolchainIssueText),
            nameof(ToolchainRuntime), nameof(ToolchainVersion), nameof(ToolchainArchitecture), nameof(ToolchainSource), nameof(ToolchainVerification), nameof(ToolchainDraftStatus) })
        { OnPropertyChanged(property); }
        SelectBundledToolchainCommand.NotifyCanExecuteChanged();
        SelectUserToolchainCommand.NotifyCanExecuteChanged();
        BrowseToolchainCommand.NotifyCanExecuteChanged();
        DetectToolchainCommand.NotifyCanExecuteChanged();
        SelectToolchainCandidateCommand.NotifyCanExecuteChanged();
        SaveToolchainCommand.NotifyCanExecuteChanged();
        DiscardToolchainChangesCommand.NotifyCanExecuteChanged();
    }
}
