using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum PendingActivationRecoveryStatus
{
    Cleared,
    ConfirmedKept,
    Unknown,
}

internal sealed partial class SettingsViewModel
{
    private VersionManagementSnapshot? _versionSnapshot;
    private SettingsVersionRowViewModel? _pendingVersionRow;
    private VersionConfirmationAction _pendingConfirmation;
    private long _updateSourceBrowseGeneration;
    private VersionManagementSnapshot? _pendingDurableSnapshot;
    private bool _hasFailedStableLauncherHandoff;
    private long _handoffFailureGeneration;
    private Func<bool> _windowMayPublish = static () => true;
    internal Func<TimeSpan, CancellationTokenSource> RetryReadCancellationFactory { get; set; } =
        static duration => new CancellationTokenSource(duration);

    internal PendingActivationRecoveryStatus PendingRecoveryStatus { get; private set; } =
        PendingActivationRecoveryStatus.Cleared;
    public bool CanRetryPendingActivation =>
        PendingRecoveryStatus == PendingActivationRecoveryStatus.ConfirmedKept;
    [ObservableProperty]
    public partial bool HasPendingRecoveryNotice { get; private set; }

    [ObservableProperty]
    public partial string PendingRecoveryMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RetryPendingActivationLabel { get; private set; } = "Retry";

    internal void SetWindowPublication(Func<bool> mayPublish)
    {
        _windowMayPublish = mayPublish ?? throw new ArgumentNullException(nameof(mayPublish));
    }

    internal void PublishPendingRecoveryStatus()
    {
        if (!_windowMayPublish())
        {
            return;
        }
        HasPendingRecoveryNotice = _hasFailedStableLauncherHandoff;
        if (_pendingDurableSnapshot is { } durable)
        {
            ApplyVersionSnapshot(durable);
        }
        VersionOperationStatus = PendingRecoveryStatus switch
        {
            PendingActivationRecoveryStatus.Cleared => Localize(
                "The stable launcher could not be started. The app remains open; verify the managed folder and try again.",
                "無法啟動穩定啟動器。應用程式仍保持開啟；請檢查受管資料夾後重試。"),
            PendingActivationRecoveryStatus.ConfirmedKept => Localize(
                "The stable launcher could not be started. The pending version switch remains saved, including if you close the app. Retry starts the stable launcher; Close exits without starting it.",
                "無法啟動穩定啟動器。待處理的版本切換仍保存在設定中，即使關閉應用程式也是如此。重試會啟動穩定啟動器；關閉則不會啟動。"),
            PendingActivationRecoveryStatus.Unknown => Localize(
                "The stable launcher could not be started. The saved pending switch could not be confirmed. Retry is unavailable until its state is checked. Close exits without starting the launcher.",
                "無法啟動穩定啟動器。目前無法確認已儲存的待處理版本切換。確認狀態前無法重試；關閉不會啟動穩定啟動器。"),
            _ => throw new InvalidOperationException("Unknown pending activation recovery status."),
        };
        PendingRecoveryMessage = PendingRecoveryStatus switch
        {
            PendingActivationRecoveryStatus.ConfirmedKept => Localize(
                "The stable launcher could not be started. The version switch is still pending and was not rolled back; it stays pending if you close the app.",
                "無法啟動穩定啟動器。版本切換仍待處理，設定未還原；關閉程式後也會保留。"),
            PendingActivationRecoveryStatus.Unknown => Localize(
                "The stable launcher could not be started, and the pending version switch could not be confirmed. A clear already in progress may still change it. Closing the app starts no launcher and no new clear.",
                "無法啟動穩定啟動器，也無法確認待處理的版本切換狀態；已在執行的清除作業仍可能改變它。關閉程式不會啟動啟動器，也不會再開始清除。"),
            PendingActivationRecoveryStatus.Cleared => VersionOperationStatus,
            _ => throw new InvalidOperationException("Unknown pending activation recovery status."),
        };
        OnPropertyChanged(nameof(CanRetryPendingActivation));
        RetryPendingActivationCommand.NotifyCanExecuteChanged();
    }

    internal void MarkPendingRecoveryUnknown()
    {
        PendingRecoveryStatus = PendingActivationRecoveryStatus.Unknown;
        _pendingDurableSnapshot = null;
        PublishPendingRecoveryStatus();
    }

    [RelayCommand(CanExecute = nameof(CanRetryPendingActivation))]
    private async Task RetryPendingActivationAsync()
    {
        if (!CanRetryPendingActivation)
        {
            return;
        }
        using WindowOperationRegistration windowOperation = BeginWindowOperation();
        using CancellationTokenSource deadline = RetryReadCancellationFactory(TimeSpan.FromSeconds(5));
        if (await RecheckPendingActivationStatusAsync(deadline.Token) !=
                PendingActivationRecoveryStatus.ConfirmedKept ||
            !CanRetryPendingActivation || !_windowMayPublish())
        {
            return;
        }
        VersionOperationStatus = Localize(
            "Retrying through the stable launcher…",
            "正在透過穩定啟動器重試…");
        _hasFailedStableLauncherHandoff = false;
        HasPendingRecoveryNotice = false;
        ActivationRequested?.Invoke(this, EventArgs.Empty);
    }

    internal event EventHandler? UpdateSourceBrowseRequested;

    internal event EventHandler? ActivationRequested;

    public ObservableCollection<SettingsVersionRowViewModel> VersionRows { get; } = [];

    [ObservableProperty]
    public partial string VersionNavigationLabel { get; private set; } = "Version";

    [ObservableProperty]
    public partial string VersionPageTitle { get; private set; } = "Version management";

    [ObservableProperty]
    public partial string VersionPageSubtitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentVersionHeading { get; private set; } = "Current version";

    [ObservableProperty]
    public partial string CurrentVersionLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentStatusLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasManagedCurrentVersion { get; private set; }

    [ObservableProperty]
    public partial string CurrentActivityLabel { get; private set; } = "Active";

    [ObservableProperty]
    public partial string CurrentIntegrityLabel { get; private set; } = "Verified";

    [ObservableProperty]
    public partial string UpdateSourceHeading { get; private set; } = "Update source";

    [ObservableProperty]
    public partial string UpdateSourcePath { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string UpdateSourceDraft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsUpdateSourceEditing { get; private set; }

    partial void OnUpdateSourceDraftChanged(string value) => InvalidateUpdateSourceBrowse();

    partial void OnIsUpdateSourceEditingChanged(bool value) => InvalidateUpdateSourceBrowse();

    [ObservableProperty]
    public partial bool IsVersionBusy { get; private set; }

    [ObservableProperty]
    public partial bool IsVersionSelfTestRunning { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourceStatusIconVisible))]
    [NotifyPropertyChangedFor(nameof(IsSourceConnectedIndicator))]
    public partial bool IsSourceChecking { get; private set; }

    [ObservableProperty]
    public partial string SourceStatusText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourceDisconnected))]
    [NotifyPropertyChangedFor(nameof(IsSourceConnectedIndicator))]
    public partial bool IsSourceConnected { get; private set; }

    public bool IsSourceStatusIconVisible => !IsSourceChecking;

    public bool IsSourceDisconnected => !IsSourceConnected;

    public bool IsSourceConnectedIndicator => IsSourceConnected && !IsSourceChecking;

    [ObservableProperty]
    public partial string EditSourceLabel { get; private set; } = "Edit";

    [ObservableProperty]
    public partial string BrowseSourceLabel { get; private set; } = "Browse";

    [ObservableProperty]
    public partial string CheckNowLabel { get; private set; } = "Check now";

    [ObservableProperty]
    public partial string RunVersionSelfTestLabel { get; private set; } = "Run self-test";

    [ObservableProperty]
    public partial string ConfirmLabel { get; private set; } = "Confirm";

    [ObservableProperty]
    public partial string CancelLabel { get; private set; } = "Cancel";

    [ObservableProperty]
    public partial string AvailableVersionsHeading { get; private set; } = "Available versions";

    [ObservableProperty]
    public partial string VersionColumnLabel { get; private set; } = "Version";

    [ObservableProperty]
    public partial string StatusColumnLabel { get; private set; } = "Status";

    [ObservableProperty]
    public partial string PublishedColumnLabel { get; private set; } = "Published";

    [ObservableProperty]
    public partial string ActionColumnLabel { get; private set; } = "Action";

    [ObservableProperty]
    public partial string InventorySummary { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVersionConfirmationOpen { get; private set; }

    [ObservableProperty]
    public partial string VersionConfirmationTitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionConfirmationDetail { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionConfirmationActionLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVersionConfirmationDestructive { get; private set; }

    [ObservableProperty]
    public partial bool HasVerifiedUpdate { get; private set; }

    [ObservableProperty]
    public partial string VerifiedUpdateMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial SettingsVersionRowViewModel? VerifiedCandidateRow { get; private set; }

    [ObservableProperty]
    public partial bool IsVerifiedReleaseNotesVisible { get; private set; }

    [ObservableProperty]
    public partial string ViewReleaseNotesLabel { get; private set; } = "View release notes";

    [ObservableProperty]
    public partial string InstallUpdateLabel { get; private set; } = "Install update";

    [ObservableProperty]
    public partial string OfflineVersionHint { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionOperationStatus { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasRetentionReview { get; private set; }

    [ObservableProperty]
    public partial string RetentionReviewMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string KeepAllVersionsLabel { get; private set; } = "Keep all";

    public bool CanManageVersions => _versionManagement is not null;

    internal async Task RefreshVersionAsync(bool isAutomatic)
    {
        if (_versionManagement is null || IsVersionBusy)
        {
            return;
        }
        using WindowOperationRegistration windowOperation = BeginWindowOperation();
        IsVersionBusy = true;
        try
        {
            VersionManagementSnapshot initialized;
            if (PendingRecoveryStatus == PendingActivationRecoveryStatus.Unknown)
            {
                using CancellationTokenSource deadline = RetryReadCancellationFactory(TimeSpan.FromSeconds(5));
                if (await RecheckPendingActivationStatusAsync(deadline.Token) ==
                    PendingActivationRecoveryStatus.Unknown)
                {
                    return;
                }
                initialized = _pendingDurableSnapshot!;
            }
            else
            {
                initialized = await _versionManagement.InitializeAsync(CancellationToken.None);
            }
            if (!await WaitForWindowPublicationAsync())
            {
                return;
            }
            ApplyVersionSnapshot(initialized);
            if (initialized.State?.UpdateSource is not null)
            {
                IsSourceChecking = true;
                VersionManagementSnapshot checkedSnapshot = await _versionManagement.CheckAsync(
                    isAutomatic, CancellationToken.None);
                if (!await WaitForWindowPublicationAsync())
                {
                    return;
                }
                ApplyVersionSnapshot(checkedSnapshot);
            }
        }
        finally
        {
            CompleteSourceBusy();
        }
    }

    internal void ApplyVersionSnapshot(VersionManagementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _versionSnapshot = snapshot;
        UpdateSourcePath = snapshot.State?.UpdateSource ?? string.Empty;
        if (!IsUpdateSourceEditing)
        {
            UpdateSourceDraft = UpdateSourcePath;
        }
        ManagedAppVersion? activeVersion = snapshot.State?.ActiveVersion;
        CurrentVersionLabel = $"NVT FW Combiner {activeVersion?.ToString() ?? _appVersion}";
        bool inventoryAvailable = snapshot.StateIssue == VersionManagerStateLoadIssue.None &&
            snapshot.InventoryIssue == ManagedVersionInventoryReadIssue.None;
        InstalledVersionSnapshot? activeInstallation = activeVersion is { } managedVersion
            ? snapshot.Inventory.Find(managedVersion)
            : null;
        HasManagedCurrentVersion = inventoryAvailable &&
            activeInstallation?.AdmissionState == ManagedVersionAdmissionState.Admitted &&
            activeInstallation.Integrity == ManagedVersionIntegrity.Healthy;
        CurrentActivityLabel = Localize("Active", "使用中");
        CurrentIntegrityLabel = Localize("Verified", "已驗證");
        CurrentStatusLabel = snapshot.StateIssue != VersionManagerStateLoadIssue.None
            ? Localize("Recovery required", "需要復原")
            : snapshot.InventoryIssue != ManagedVersionInventoryReadIssue.None
                ? Localize("Inventory unavailable", "版本清單無法使用")
            : activeInstallation is null
                ? Localize("Current · Unmanaged", "目前版本 · 非受管安裝")
                : activeInstallation.AdmissionState != ManagedVersionAdmissionState.Admitted
                    ? Localize("Recovery required", "需要復原")
                    : activeInstallation.Integrity == ManagedVersionIntegrity.Damaged
                        ? Localize("Active · Damaged", "使用中 · 已損壞")
                        : Localize("Active · Verified", "使用中 · 已驗證");
        SourceStatusText = snapshot.SourceStatus switch
        {
            VersionSourceStatus.Checking => Localize("Checking", "檢查中"),
            VersionSourceStatus.Connected => Localize("Connected", "已連線"),
            VersionSourceStatus.Offline => Localize("Offline", "離線"),
            VersionSourceStatus.PermissionDenied => Localize("Permission denied", "權限不足"),
            VersionSourceStatus.Invalid => Localize("Verification failed", "驗證失敗"),
            VersionSourceStatus.NotConfigured => Localize("Not configured", "尚未設定"),
            _ => Localize("Not configured", "尚未設定"),
        };
        IsSourceConnected = snapshot.SourceStatus == VersionSourceStatus.Connected;
        string recoverySummary = snapshot.Inventory.UnadmittedCount > 0
            ? Localize(
                $" · {snapshot.Inventory.UnadmittedCount} need recovery",
                $" · {snapshot.Inventory.UnadmittedCount} 個需要復原")
            : string.Empty;
        InventorySummary = !inventoryAvailable
            ? Localize("Inventory unavailable", "版本清單無法使用")
            : Localize(
                $"{snapshot.Inventory.HealthyCount} healthy · {snapshot.Inventory.DamagedCount} damaged",
                $"{snapshot.Inventory.HealthyCount} 個正常 · {snapshot.Inventory.DamagedCount} 個已損壞") +
              recoverySummary;
        HasRetentionReview = inventoryAvailable && snapshot.State?.RetentionReviewDue == true;
        RetentionReviewMessage = HasRetentionReview
            ? Localize(
                $"More than {VersionManagementPolicy.DefaultHealthyVersionReminderThreshold} healthy versions are installed. Delete any non-active version below, or keep all.",
                $"已安裝超過 {VersionManagementPolicy.DefaultHealthyVersionReminderThreshold} 個正常版本。可在下方逐一刪除非使用中版本，或全部保留。")
            : string.Empty;
        if (inventoryAvailable)
        {
            ProjectVersionRows(snapshot);
        }
        else
        {
            VersionRows.Clear();
            CancelVersionConfirmation();
        }
        HasVerifiedUpdate = inventoryAvailable && snapshot.VerifiedCandidate is not null;
        VerifiedCandidateRow = inventoryAvailable && snapshot.VerifiedCandidate is { } verified
            ? VersionRows.FirstOrDefault(row => row.Version == verified.Version)
            : null;
        if (VerifiedCandidateRow is null)
        {
            IsVerifiedReleaseNotesVisible = false;
        }
        VerifiedUpdateMessage = HasVerifiedUpdate && snapshot.VerifiedCandidate is { } candidate
            ? Localize(
                $"Version {candidate.Version} is verified and available.",
                $"版本 {candidate.Version} 已驗證並可安裝。")
            : string.Empty;
        if (!IsVersionConfirmationOpen &&
            inventoryAvailable && snapshot.ShouldPromptForUpdate &&
            VersionRows.FirstOrDefault(row => row.Version == snapshot.VerifiedCandidate?.Version) is { } row)
        {
            BeginConfirmation(row, VersionConfirmationAction.Install);
        }
    }

    internal long? BeginUpdateSourceBrowse()
    {
        return IsUpdateSourceEditing ? ++_updateSourceBrowseGeneration : null;
    }

    internal void SetUpdateSourceDraft(string path, long browseGeneration)
    {
        if (IsUpdateSourceEditing &&
            browseGeneration == _updateSourceBrowseGeneration &&
            !string.IsNullOrWhiteSpace(path))
        {
            UpdateSourceDraft = path;
        }
    }

    internal void InvalidateUpdateSourceBrowse()
    {
        _updateSourceBrowseGeneration++;
    }

    private void RefreshVersionLabels()
    {
        VersionNavigationLabel = Localize("Version", "版本");
        VersionPageTitle = Localize("Version management", "版本管理");
        VersionPageSubtitle = Localize(
            "Check, install, and switch verified application versions.",
            "檢查、安裝及切換已驗證的應用程式版本。");
        CurrentVersionHeading = Localize("Current version", "目前版本");
        UpdateSourceHeading = Localize("Update source", "更新來源");
        EditSourceLabel = Localize("Edit", "編輯");
        BrowseSourceLabel = Localize("Browse", "瀏覽");
        CheckNowLabel = Localize("Check now", "立即檢查");
        RunVersionSelfTestLabel = Localize("Run self-test", "執行自我測試");
        ConfirmLabel = Localize("Confirm", "確認");
        CancelLabel = Localize("Cancel", "取消");
        AvailableVersionsHeading = Localize("Available versions", "可用版本");
        VersionColumnLabel = Localize("Version", "版本");
        StatusColumnLabel = Localize("Status", "狀態");
        PublishedColumnLabel = Localize("Published", "發布日期");
        ActionColumnLabel = Localize("Action", "動作");
        KeepAllVersionsLabel = Localize("Keep all", "全部保留");
        RetryPendingActivationLabel = Localize("Retry", "重試");
        ViewReleaseNotesLabel = Localize("View release notes", "檢視版本說明");
        InstallUpdateLabel = Localize("Install update", "安裝更新");
        OfflineVersionHint = Localize(
            "Offline, you can switch only to verified versions already installed on this PC.",
            "離線時，只能切換至此電腦上已安裝且驗證通過的版本。");
        if (_versionSnapshot is not null)
        {
            ApplyVersionSnapshot(_versionSnapshot);
        }
        if (_hasFailedStableLauncherHandoff)
        {
            PublishPendingRecoveryStatus();
        }
    }

    [RelayCommand]
    private void BeginEditUpdateSource()
    {
        UpdateSourceDraft = UpdateSourcePath;
        IsUpdateSourceEditing = true;
    }

    [RelayCommand]
    private void CancelEditUpdateSource()
    {
        UpdateSourceDraft = UpdateSourcePath;
        IsUpdateSourceEditing = false;
    }

    [RelayCommand]
    private void BrowseUpdateSource()
    {
        UpdateSourceBrowseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ConfirmUpdateSourceAsync()
    {
        if (_versionManagement is null || PendingRecoveryStatus == PendingActivationRecoveryStatus.Unknown ||
            string.IsNullOrWhiteSpace(UpdateSourceDraft))
        {
            return;
        }
        using WindowOperationRegistration windowOperation = BeginWindowOperation();
        IsVersionBusy = true;
        IsSourceChecking = true;
        try
        {
            IsUpdateSourceEditing = false;
            VersionManagementSnapshot committed = await _versionManagement.CommitUpdateSourceAsync(
                UpdateSourceDraft,
                CancellationToken.None);
            if (!await WaitForWindowPublicationAsync())
            {
                return;
            }
            ApplyVersionSnapshot(committed);
        }
        finally
        {
            CompleteSourceBusy();
        }
    }

    [RelayCommand]
    private async Task CheckNowAsync()
    {
        if (_versionManagement is null)
        {
            return;
        }
        using WindowOperationRegistration windowOperation = BeginWindowOperation();
        IsVersionBusy = true;
        IsSourceChecking = true;
        try
        {
            if (PendingRecoveryStatus == PendingActivationRecoveryStatus.Unknown)
            {
                using CancellationTokenSource deadline = RetryReadCancellationFactory(TimeSpan.FromSeconds(5));
                if (await RecheckPendingActivationStatusAsync(deadline.Token) ==
                    PendingActivationRecoveryStatus.Unknown)
                {
                    return;
                }
            }
            VersionManagementSnapshot checkedSnapshot = await _versionManagement.CheckAsync(
                isAutomatic: false,
                CancellationToken.None);
            if (!await WaitForWindowPublicationAsync())
            {
                return;
            }
            ApplyVersionSnapshot(checkedSnapshot);
        }
        finally
        {
            CompleteSourceBusy();
        }
    }

    private void CompleteSourceBusy()
    {
        if (MayPublishWindow)
        {
            IsSourceChecking = false;
            IsVersionBusy = false;
        }
    }

    internal void SetSourceChecking(bool isChecking)
    {
        IsSourceChecking = isChecking;
    }

    [RelayCommand]
    private async Task KeepAllVersionsAsync()
    {
        if (_versionManagement is null || PendingRecoveryStatus == PendingActivationRecoveryStatus.Unknown)
        {
            return;
        }
        using WindowOperationRegistration windowOperation = BeginWindowOperation();
        IsVersionBusy = true;
        try
        {
            VersionManagementSnapshot snapshot = await _versionManagement.AcknowledgeRetentionReviewAsync(
                CancellationToken.None);
            if (!await WaitForWindowPublicationAsync())
            {
                return;
            }
            ApplyVersionSnapshot(snapshot);
            VersionOperationStatus = snapshot.StateIssue == VersionManagerStateLoadIssue.None &&
                snapshot.State?.RetentionReviewDue == false
                ? Localize(
                    "All installed versions were kept.",
                    "已保留所有安裝版本。")
                : Localize(
                    "Version state is unavailable. The retention reminder was not cleared; restart Settings to try again.",
                    "版本狀態目前無法使用，保留提醒尚未清除；請重新開啟設定後再試一次。");
        }
        finally
        {
            if (MayPublishWindow)
            {
                IsVersionBusy = false;
            }
        }
    }

    [RelayCommand]
    private void ShowVerifiedReleaseNotes()
    {
        IsVerifiedReleaseNotesVisible = VerifiedCandidateRow is not null;
    }

    [RelayCommand]
    private void RequestVersionPrimaryAction(SettingsVersionRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        BeginConfirmation(
            row,
            row.PrimaryAction == SettingsVersionPrimaryAction.Install
                ? VersionConfirmationAction.Install
                : VersionConfirmationAction.Switch);
    }

    [RelayCommand]
    private void RequestDeleteVersion(SettingsVersionRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        BeginConfirmation(row, VersionConfirmationAction.Delete);
    }

    [RelayCommand]
    private void CancelVersionConfirmation()
    {
        IsVersionConfirmationOpen = false;
        _pendingVersionRow = null;
        _pendingConfirmation = VersionConfirmationAction.None;
    }

    [RelayCommand]
    private async Task ConfirmVersionActionAsync()
    {
        if (PendingRecoveryStatus == PendingActivationRecoveryStatus.Unknown)
        {
            CancelVersionConfirmation();
            PublishPendingRecoveryStatus();
            return;
        }
        if (_versionManagement is null || _pendingVersionRow is not { } row)
        {
            return;
        }
        VersionConfirmationAction action = _pendingConfirmation;
        long handoffFailureGeneration = _handoffFailureGeneration;
        CancelVersionConfirmation();
        using WindowOperationRegistration windowOperation = BeginWindowOperation();
        IsVersionBusy = true;
        try
        {
            if (action == VersionConfirmationAction.Delete && row.IsLastKnownGood)
            {
                BeginConfirmation(row, VersionConfirmationAction.DeleteLastKnownGood);
                return;
            }
            if (action is VersionConfirmationAction.Delete or VersionConfirmationAction.DeleteLastKnownGood)
            {
                VersionDeleteOperationResult deleted = await _versionManagement.DeleteAsync(
                    row.Version,
                    rollbackLossConfirmed: action == VersionConfirmationAction.DeleteLastKnownGood,
                    CancellationToken.None);
                if (!await WaitForWindowPublicationAsync())
                {
                    return;
                }
                if (deleted.OperationIssue == VersionDeleteOperationIssue.RollbackConfirmationRequired)
                {
                    ApplyVersionSnapshot(deleted.Snapshot);
                    BeginConfirmation(row, VersionConfirmationAction.DeleteLastKnownGood);
                    return;
                }
                ApplyVersionSnapshot(deleted.Snapshot);
                VersionOperationStatus = deleted.OperationIssue switch
                {
                    VersionDeleteOperationIssue.None =>
                        Localize($"Version {row.Version} deleted.", $"版本 {row.Version} 已刪除。"),
                    VersionDeleteOperationIssue.StateUnavailable => Localize(
                        "Version state is unavailable. No further action was taken; restart Settings to reconcile the operation.",
                        "版本狀態目前無法使用。未再執行其他動作；請重新開啟設定以收斂此操作。"),
                    VersionDeleteOperationIssue.PolicyBlocked or
                    VersionDeleteOperationIssue.RollbackConfirmationRequired or
                    VersionDeleteOperationIssue.RepositoryFailure =>
                        Localize("The version could not be deleted.", "無法刪除此版本。"),
                    _ => throw new InvalidOperationException("Unknown version delete operation outcome."),
                };
                return;
            }
            if (action == VersionConfirmationAction.Install)
            {
                VersionInstallOperationResult installed = await _versionManagement.InstallAsync(
                    row.Version,
                    CancellationToken.None);
                if (!await WaitForWindowPublicationAsync())
                {
                    return;
                }
                ApplyVersionSnapshot(installed.Snapshot);
                if (!installed.Install.IsSuccess)
                {
                    VersionOperationStatus = installed.Install.Issue switch
                    {
                        ManagedVersionInstallIssue.StateUnavailable => Localize(
                            "Version state is unavailable. Restart Settings to reconcile the installation.",
                            "版本狀態目前無法使用。請重新開啟設定以收斂安裝狀態。"),
                        ManagedVersionInstallIssue.CleanupIncomplete => Localize(
                            "Recovery required. Installation cleanup could not complete; restart the launcher before trying again.",
                            "需要復原。安裝清理未能完成；請重新啟動啟動器後再試。"),
                        ManagedVersionInstallIssue.None or
                        ManagedVersionInstallIssue.PackageUnavailable or
                        ManagedVersionInstallIssue.PackageMismatch or
                        ManagedVersionInstallIssue.UnsafeArchive or
                        ManagedVersionInstallIssue.InvalidPayload or
                        ManagedVersionInstallIssue.IdentityConflict or
                        ManagedVersionInstallIssue.PromotionFailed =>
                            Localize("Installation failed verification.", "安裝驗證失敗。"),
                        _ => throw new InvalidOperationException(
                            "Unknown version installation outcome."),
                    };
                    return;
                }
            }

            try
            {
                _ = await _versionManagement.PrepareActivationAsync(row.Version, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                if (!await WaitForWindowPublicationAsync())
                {
                    return;
                }
                VersionOperationStatus = Localize(
                    "Activation could not be prepared because version state is unavailable or changed.",
                    "版本狀態目前無法使用或已變更，因此無法準備啟用。");
                return;
            }
            // Durable acceptance is a window lifecycle decision even while screen publication is suspended.
            ActivationRequested?.Invoke(this, EventArgs.Empty);
            if (!await WaitForWindowPublicationAsync() ||
                handoffFailureGeneration != _handoffFailureGeneration)
            {
                return;
            }
            VersionOperationStatus = Localize("Restarting through the launcher…", "正在透過啟動器重新啟動…");
            _hasFailedStableLauncherHandoff = false;
            HasPendingRecoveryNotice = false;
        }
        finally
        {
            if (MayPublishWindow)
            {
                IsVersionBusy = false;
            }
        }
    }

    internal async Task<PendingActivationRecoveryStatus> HandleLauncherHandoffFailureAsync(
        CancellationToken recoveryToken)
    {
        _handoffFailureGeneration++;
        _hasFailedStableLauncherHandoff = true;
        return await RecheckPendingActivationStatusAsync(recoveryToken);
    }

    internal async Task<PendingActivationRecoveryStatus> RecheckPendingActivationStatusAsync(
        CancellationToken cancellationToken)
    {
        if (_versionManagement is null)
        {
            PendingRecoveryStatus = PendingActivationRecoveryStatus.Cleared;
        }
        else
        {
            try
            {
                Task<VersionManagementSnapshot> read = _versionManagement.InitializeAsync(cancellationToken).AsTask();
                _ = read.ContinueWith(completed => _ = completed.Exception,
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                VersionManagementSnapshot durable = await read.WaitAsync(cancellationToken);
                _pendingDurableSnapshot = durable;
                PendingRecoveryStatus = durable.StateIssue == VersionManagerStateLoadIssue.None &&
                    durable.State is { } state
                    ? state.PendingActivation is null
                        ? PendingActivationRecoveryStatus.Cleared
                        : PendingActivationRecoveryStatus.ConfirmedKept
                    : PendingActivationRecoveryStatus.Unknown;
            }
            catch (Exception)
            {
                PendingRecoveryStatus = PendingActivationRecoveryStatus.Unknown;
                _pendingDurableSnapshot = null;
            }
        }
        PublishPendingRecoveryStatus();
        return PendingRecoveryStatus;
    }

    private void BeginConfirmation(
        SettingsVersionRowViewModel row,
        VersionConfirmationAction action)
    {
        if (PendingRecoveryStatus == PendingActivationRecoveryStatus.Unknown)
        {
            PublishPendingRecoveryStatus();
            return;
        }
        _pendingVersionRow = row;
        _pendingConfirmation = action;
        IsVersionConfirmationDestructive = action is
            VersionConfirmationAction.Delete or VersionConfirmationAction.DeleteLastKnownGood;
        VersionConfirmationTitle = action switch
        {
            VersionConfirmationAction.Install => Localize($"Install {row.Version}?", $"安裝 {row.Version}？"),
            VersionConfirmationAction.Switch => Localize($"Switch to {row.Version}?", $"切換到 {row.Version}？"),
            VersionConfirmationAction.Delete => Localize($"Delete {row.Version}?", $"刪除 {row.Version}？"),
            VersionConfirmationAction.DeleteLastKnownGood => Localize(
                "Delete the rollback version?",
                "刪除回復版本？"),
            VersionConfirmationAction.None => string.Empty,
            _ => string.Empty,
        };
        VersionConfirmationDetail = action switch
        {
            VersionConfirmationAction.Install => row.ReleaseNotes,
            VersionConfirmationAction.Switch => Localize(
                "The app will close and the stable launcher will verify this version before starting it.",
                "應用程式將關閉，穩定啟動器會先驗證此版本再啟動。"),
            VersionConfirmationAction.Delete => Localize(
                $"Only the installed {row.Version} folder will be removed. This cannot be undone.",
                $"只會移除已安裝的 {row.Version} 資料夾，且無法復原。"),
            VersionConfirmationAction.DeleteLastKnownGood => Localize(
                $"Version {row.Version} is the last-known-good rollback target. Deleting it removes automatic recovery for the next failed activation.",
                $"版本 {row.Version} 是目前最後正常的回復目標。刪除後，下一次啟用失敗時將無法自動回復到此版本。"),
            VersionConfirmationAction.None => string.Empty,
            _ => string.Empty,
        };
        VersionConfirmationActionLabel = action switch
        {
            VersionConfirmationAction.Install => Localize("Install update", "安裝更新"),
            VersionConfirmationAction.Switch => Localize("Switch", "切換"),
            VersionConfirmationAction.Delete => Localize("Delete", "刪除"),
            VersionConfirmationAction.DeleteLastKnownGood => Localize("Delete anyway", "仍要刪除"),
            VersionConfirmationAction.None => string.Empty,
            _ => string.Empty,
        };
        IsVersionConfirmationOpen = true;
    }

    private void ProjectVersionRows(VersionManagementSnapshot snapshot)
    {
        Dictionary<ManagedAppVersion, UpdateCatalogVersionSnapshot> catalog =
            snapshot.Catalog?.Versions.ToDictionary(version => version.Version) ?? [];
        var installed =
            snapshot.Inventory.Versions.ToDictionary(version => version.Version);
        ManagedAppVersion[] versions = [.. catalog.Keys.Concat(installed.Keys).Distinct().OrderDescending()];
        ReplaceRows(VersionRows, versions.Select(version =>
        {
            _ = catalog.TryGetValue(version, out UpdateCatalogVersionSnapshot? available);
            _ = installed.TryGetValue(version, out InstalledVersionSnapshot? local);
            bool admitted = local?.AdmissionState == ManagedVersionAdmissionState.Admitted;
            bool recoveryCandidate = local?.AdmissionState == ManagedVersionAdmissionState.RecoveryCandidate;
            bool unadmitted = local?.AdmissionState == ManagedVersionAdmissionState.Unadmitted;
            bool active = admitted && local?.IsActive == true;
            bool damaged = admitted && local?.Integrity == ManagedVersionIntegrity.Damaged;
            bool verified = snapshot.VerifiedCandidate?.Version == version ||
                            (admitted && local?.Integrity == ManagedVersionIntegrity.Healthy);
            SettingsVersionPrimaryAction action = active
                ? SettingsVersionPrimaryAction.None
                : admitted && !damaged
                    ? SettingsVersionPrimaryAction.Switch
                : local is null && available is not null
                        ? SettingsVersionPrimaryAction.Install
                        : SettingsVersionPrimaryAction.None;
            string status = recoveryCandidate
                ? Localize("Recovery pending", "等待復原")
                : unadmitted
                    ? Localize("Unmanaged folder · Recovery required", "非受管資料夾 · 需要復原")
                    : active
                ? damaged
                    ? Localize("Active · Damaged", "使用中 · 已損壞")
                    : Localize("Active · Verified", "使用中 · 已驗證")
                : damaged
                    ? Localize("Damaged", "已損壞")
                    : admitted
                        ? Localize("Installed · Verified", "已安裝 · 已驗證")
                        : verified
                            ? Localize("Available · Verified", "可用 · 已驗證")
                            : Localize("Available", "可用");
            return new SettingsVersionRowViewModel(
                version,
                version.ToString(),
                status,
                available?.PublishedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "—",
                available?.ReleaseNotes ?? string.Empty,
                action,
                action == SettingsVersionPrimaryAction.Install
                    ? Localize("Install", "安裝")
                    : action == SettingsVersionPrimaryAction.Switch
                        ? Localize("Switch", "切換")
                        : Localize("Current", "目前版本"),
                Localize($"Delete installed version {version}", $"刪除已安裝版本 {version}"),
                active,
                admitted,
                damaged,
                admitted && !active,
                admitted && local?.IsLastKnownGood == true);
        }));
    }

    private string Localize(string english, string chinese)
    {
        return _textProvider().Language == ShellLanguage.ChineseTraditional ? chinese : english;
    }

    private enum VersionConfirmationAction
    {
        None,
        Install,
        Switch,
        Delete,
        DeleteLastKnownGood,
    }
}
