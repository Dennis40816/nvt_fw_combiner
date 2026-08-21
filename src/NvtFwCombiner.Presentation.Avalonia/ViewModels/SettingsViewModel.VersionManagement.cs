using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class SettingsViewModel
{
    private VersionManagementSnapshot? _versionSnapshot;
    private SettingsVersionRowViewModel? _pendingVersionRow;
    private VersionConfirmationAction _pendingConfirmation;

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
    public partial string UpdateSourceHeading { get; private set; } = "Update source";

    [ObservableProperty]
    public partial string UpdateSourcePath { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string UpdateSourceDraft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsUpdateSourceEditing { get; private set; }

    [ObservableProperty]
    public partial bool IsVersionBusy { get; private set; }

    [ObservableProperty]
    public partial bool IsSourceChecking { get; private set; }

    [ObservableProperty]
    public partial string SourceStatusGlyph { get; private set; } = "○";

    [ObservableProperty]
    public partial string SourceStatusText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string EditSourceLabel { get; private set; } = "Edit";

    [ObservableProperty]
    public partial string BrowseSourceLabel { get; private set; } = "Browse";

    [ObservableProperty]
    public partial string CheckNowLabel { get; private set; } = "Check now";

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
        IsVersionBusy = true;
        try
        {
            VersionManagementSnapshot initialized = await _versionManagement.InitializeAsync(CancellationToken.None);
            ApplyVersionSnapshot(initialized);
            if (initialized.State?.UpdateSource is not null)
            {
                IsSourceChecking = true;
                ApplyVersionSnapshot(await _versionManagement.CheckAsync(isAutomatic, CancellationToken.None));
            }
        }
        finally
        {
            IsSourceChecking = false;
            IsVersionBusy = false;
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
        CurrentVersionLabel = snapshot.State?.ActiveVersion?.ToString() ?? _appVersion;
        CurrentStatusLabel = snapshot.Inventory.Find(snapshot.State?.ActiveVersion ?? default)?.Integrity ==
            ManagedVersionIntegrity.Healthy
                ? Localize("Active · Verified", "使用中 · 已驗證")
                : snapshot.StateIssue == VersionManagerStateLoadIssue.None
                    ? Localize("Current · Unmanaged", "目前版本 · 非受管安裝")
                    : Localize("Recovery required", "需要復原");
        SourceStatusGlyph = snapshot.SourceStatus switch
        {
            VersionSourceStatus.Checking => "◌",
            VersionSourceStatus.Connected => "✓",
            VersionSourceStatus.Offline => "!",
            VersionSourceStatus.PermissionDenied => "⊘",
            VersionSourceStatus.Invalid => "×",
            VersionSourceStatus.NotConfigured => "○",
            _ => "○",
        };
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
        string recoverySummary = snapshot.Inventory.UnadmittedCount > 0
            ? Localize(
                $" · {snapshot.Inventory.UnadmittedCount} need recovery",
                $" · {snapshot.Inventory.UnadmittedCount} 個需要復原")
            : string.Empty;
        InventorySummary = Localize(
            $"{snapshot.Inventory.HealthyCount} healthy · {snapshot.Inventory.DamagedCount} damaged",
            $"{snapshot.Inventory.HealthyCount} 個正常 · {snapshot.Inventory.DamagedCount} 個已損壞") +
            recoverySummary;
        HasRetentionReview = snapshot.State?.RetentionReviewDue == true;
        RetentionReviewMessage = HasRetentionReview
            ? Localize(
                $"More than {VersionManagementPolicy.DefaultHealthyVersionReminderThreshold} healthy versions are installed. Delete any non-active version below, or keep all.",
                $"已安裝超過 {VersionManagementPolicy.DefaultHealthyVersionReminderThreshold} 個正常版本。可在下方逐一刪除非使用中版本，或全部保留。")
            : string.Empty;
        ProjectVersionRows(snapshot);
        HasVerifiedUpdate = snapshot.VerifiedCandidate is not null;
        VerifiedUpdateMessage = snapshot.VerifiedCandidate is { } candidate
            ? Localize(
                $"Version {candidate.Version} is verified and available.",
                $"版本 {candidate.Version} 已驗證並可安裝。")
            : string.Empty;
        if (!IsVersionConfirmationOpen &&
            snapshot.ShouldPromptForUpdate &&
            VersionRows.FirstOrDefault(row => row.Version == snapshot.VerifiedCandidate?.Version) is { } row)
        {
            BeginConfirmation(row, VersionConfirmationAction.Install);
        }
    }

    internal void SetUpdateSourceDraft(string path)
    {
        if (IsUpdateSourceEditing && !string.IsNullOrWhiteSpace(path))
        {
            UpdateSourceDraft = path;
        }
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
        ConfirmLabel = Localize("Confirm", "確認");
        CancelLabel = Localize("Cancel", "取消");
        AvailableVersionsHeading = Localize("Available versions", "可用版本");
        VersionColumnLabel = Localize("Version", "版本");
        StatusColumnLabel = Localize("Status", "狀態");
        PublishedColumnLabel = Localize("Published", "發布日期");
        ActionColumnLabel = Localize("Action", "動作");
        KeepAllVersionsLabel = Localize("Keep all", "全部保留");
        if (_versionSnapshot is not null)
        {
            ApplyVersionSnapshot(_versionSnapshot);
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
        if (_versionManagement is null || string.IsNullOrWhiteSpace(UpdateSourceDraft))
        {
            return;
        }
        IsVersionBusy = true;
        IsSourceChecking = true;
        try
        {
            IsUpdateSourceEditing = false;
            ApplyVersionSnapshot(await _versionManagement.CommitUpdateSourceAsync(
                UpdateSourceDraft,
                CancellationToken.None));
        }
        finally
        {
            IsSourceChecking = false;
            IsVersionBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckNowAsync()
    {
        if (_versionManagement is null)
        {
            return;
        }
        IsVersionBusy = true;
        IsSourceChecking = true;
        try
        {
            ApplyVersionSnapshot(await _versionManagement.CheckAsync(
                isAutomatic: false,
                CancellationToken.None));
        }
        finally
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
        if (_versionManagement is null)
        {
            return;
        }
        IsVersionBusy = true;
        try
        {
            ApplyVersionSnapshot(await _versionManagement.AcknowledgeRetentionReviewAsync(
                CancellationToken.None));
            VersionOperationStatus = Localize(
                "All installed versions were kept.",
                "已保留所有安裝版本。");
        }
        finally
        {
            IsVersionBusy = false;
        }
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
        if (_versionManagement is null || _pendingVersionRow is not { } row)
        {
            return;
        }
        VersionConfirmationAction action = _pendingConfirmation;
        CancelVersionConfirmation();
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
                ApplyVersionSnapshot(installed.Snapshot);
                if (!installed.Install.IsSuccess)
                {
                    VersionOperationStatus = installed.Install.Issue == ManagedVersionInstallIssue.StateUnavailable
                        ? Localize(
                            "Version state is unavailable. Restart Settings to reconcile the installation.",
                            "版本狀態目前無法使用。請重新開啟設定以收斂安裝狀態。")
                        : Localize("Installation failed verification.", "安裝驗證失敗。");
                    return;
                }
            }

            try
            {
                _ = await _versionManagement.PrepareActivationAsync(row.Version, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                VersionOperationStatus = Localize(
                    "Activation could not be prepared because version state is unavailable or changed.",
                    "版本狀態目前無法使用或已變更，因此無法準備啟用。");
                return;
            }
            VersionOperationStatus = Localize("Restarting through the launcher…", "正在透過啟動器重新啟動…");
            ActivationRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsVersionBusy = false;
        }
    }

    internal async Task<bool> HandleLauncherHandoffFailureAsync()
    {
        bool activationCleared = true;
        if (_versionManagement is not null)
        {
            try
            {
                VersionManagementSnapshot recovered = await _versionManagement.CancelPendingActivationAsync(
                    CancellationToken.None);
                ApplyVersionSnapshot(recovered);
            }
            catch (InvalidOperationException)
            {
                activationCleared = false;
            }
        }
        VersionOperationStatus = activationCleared
            ? Localize(
                "The stable launcher could not be started. The app remains open; verify the managed folder and try again.",
                "無法啟動穩定啟動器。應用程式仍保持開啟；請檢查受管資料夾後重試。")
            : Localize(
                "The stable launcher could not be started, and pending activation could not be cleared. The app remains open; restore version-state access and close again to retry the launcher.",
                "無法啟動穩定啟動器，且無法清除待處理的版本啟用。應用程式仍保持開啟；請恢復版本狀態存取後再次關閉，以重試啟動器。");
        return activationCleared;
    }

    private void BeginConfirmation(
        SettingsVersionRowViewModel row,
        VersionConfirmationAction action)
    {
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
                ? Localize("Active · Verified", "使用中 · 已驗證")
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
