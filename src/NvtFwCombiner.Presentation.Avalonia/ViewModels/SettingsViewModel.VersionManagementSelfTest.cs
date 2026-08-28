using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class SettingsViewModel
{
    [RelayCommand]
    private async Task RunVersionSelfTestAsync(CancellationToken cancellationToken)
    {
        if (_versionManagement is null)
        {
            return;
        }

        IsVersionBusy = true;
        IsVersionSelfTestRunning = true;
        IsSourceChecking = true;
        VersionOperationStatus = Localize(
            "Running the update environment self-test…",
            "正在執行更新環境自我測試…");
        try
        {
            VersionEnvironmentSelfTestResult result = await Task.Run(
                () => _versionManagement.RunEnvironmentSelfTestAsync(cancellationToken).AsTask(), cancellationToken);
            VersionOperationStatus = FormatEnvironmentSelfTestResult(result);
        }
        finally
        {
            IsSourceChecking = false;
            IsVersionSelfTestRunning = false;
            IsVersionBusy = false;
        }
    }

    private string FormatEnvironmentSelfTestResult(VersionEnvironmentSelfTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        string replicas = FormatRegistryReplicaHealth(result);
        if (result.RegistryIssue != UpdateSourceRegistryLoadIssue.None)
        {
            string failure = Localize(
                $"Self-test failed: {FormatRegistrySelfTestIssue(result.RegistryIssue)}.",
                $"自我測試失敗：{FormatRegistrySelfTestIssue(result.RegistryIssue)}。");
            return string.IsNullOrEmpty(replicas) ? failure : $"{failure} {replicas}";
        }
        if (result.AuthorityIssue != UpdateSourceRegistryIssue.None)
        {
            string failure = Localize(
                $"Self-test failed: {FormatRegistryAuthorityIssue(result.AuthorityIssue)}.",
                $"自我測試失敗：{FormatRegistryAuthorityIssue(result.AuthorityIssue)}。");
            return string.IsNullOrEmpty(replicas) ? failure : $"{failure} {replicas}";
        }

        int verified = result.Attempts.Count(static attempt => attempt.IsVerified);
        string summary = !result.IsSuccess
            ? Localize(
                $"Self-test failed: {verified}/{result.Attempts.Count} automatic sources verified.",
                $"自我測試失敗：{verified}/{result.Attempts.Count} 個自動來源驗證成功。")
            : verified != result.Attempts.Count
            ? Localize(
                $"Self-test needs attention: {verified}/{result.Attempts.Count} automatic sources verified.",
                $"自我測試需要注意：{verified}/{result.Attempts.Count} 個自動來源驗證成功。")
            : Localize(
                $"Self-test passed: {verified}/{result.Attempts.Count} automatic sources verified.",
                $"自我測試通過：{verified}/{result.Attempts.Count} 個自動來源驗證成功。");
        string details = string.Join(
            " · ",
            result.Attempts.Select(FormatEnvironmentSelfTestAttempt));
        string suffix = string.Join(" · ", new[] { replicas, details }.Where(static value => value.Length > 0));
        return string.IsNullOrEmpty(suffix) ? summary : $"{summary} {suffix}";
    }

    private string FormatRegistryReplicaHealth(VersionEnvironmentSelfTestResult result)
    {
        long? selectedRevision = result.Replicas.SingleOrDefault(static replica => replica.IsSelected)
            ?.RegistryRevision;
        return string.Join(
            " · ",
            result.Replicas.Select(replica =>
            {
                string role = replica.Position == 1
                    ? Localize("Primary", "主要")
                    : Localize($"Backup {replica.Position - 1}", $"備援 {replica.Position - 1}");
                return replica.Issue != UpdateSourceRegistryLoadIssue.None
                    ? $"{role}: {FormatRegistrySelfTestIssue(replica.Issue)}"
                    : result.AcceptedRegistryRevision is { } accepted &&
                      replica.RegistryRevision < accepted
                    ? Localize(
                        $"{role}: stale revision {replica.RegistryRevision} (accepted {accepted})",
                        $"{role}：版本 {replica.RegistryRevision} 已過期（已接受 {accepted}）")
                    : replica.IsSelected
                    ? Localize(
                        $"{role}: revision {replica.RegistryRevision} selected",
                        $"{role}：已選用版本 {replica.RegistryRevision}")
                    : replica.RegistryRevision == selectedRevision
                    ? Localize(
                        $"{role}: revision {replica.RegistryRevision} synchronized",
                        $"{role}：版本 {replica.RegistryRevision} 已同步")
                    : Localize(
                        $"{role}: stale revision {replica.RegistryRevision}",
                        $"{role}：版本 {replica.RegistryRevision} 已過期");
            }));
    }

    private string FormatEnvironmentSelfTestAttempt(VersionEnvironmentSelfTestAttempt attempt)
    {
        string role = attempt.Status == UpdateSourceRegistryEntryStatus.Latest
            ? Localize("Latest", "最新")
            : Localize("Available", "可用");
        return attempt.IsVerified
            ? Localize(
                $"{role}: version {attempt.NewestVersion} verified",
                $"{role}：版本 {attempt.NewestVersion} 驗證成功")
            : attempt.CatalogIssue != UpdateCatalogLoadIssue.None
            ? Localize(
                $"{role}: {FormatCatalogSelfTestIssue(attempt.CatalogIssue)}",
                $"{role}：{FormatCatalogSelfTestIssue(attempt.CatalogIssue)}")
            : Localize(
                $"{role}: {FormatPackageSelfTestIssue(attempt.PackageIssue!.Value)}",
                $"{role}：{FormatPackageSelfTestIssue(attempt.PackageIssue!.Value)}");
    }

    private string FormatRegistrySelfTestIssue(UpdateSourceRegistryLoadIssue issue)
    {
        return issue switch
        {
            UpdateSourceRegistryLoadIssue.None => Localize("the fixed Registry is valid", "固定 Registry 有效"),
            UpdateSourceRegistryLoadIssue.NotConfigured => Localize(
                "the fixed Registry location is not configured",
                "尚未設定固定 Registry 位置"),
            UpdateSourceRegistryLoadIssue.RegistryMissing => Localize(
                "the fixed Registry file is missing",
                "找不到固定 Registry 檔案"),
            UpdateSourceRegistryLoadIssue.RegistryUnavailable => Localize(
                "the fixed Registry is unavailable",
                "固定 Registry 無法使用"),
            UpdateSourceRegistryLoadIssue.PermissionDenied => Localize(
                "permission to read the fixed Registry was denied",
                "沒有權限讀取固定 Registry"),
            UpdateSourceRegistryLoadIssue.AuthenticationRequired => Localize(
                "Registry JSON was not retrieved; sign in to the configured HTTPS source, confirm access, then run the self-test again",
                "未取得 Registry JSON；請先登入設定的 HTTPS 來源並確認可存取，再重新執行自我測試"),
            UpdateSourceRegistryLoadIssue.RegistryTimedOut => Localize(
                "the fixed Registry request timed out",
                "固定 Registry 請求逾時"),
            UpdateSourceRegistryLoadIssue.UnsafeLocator => Localize(
                "the fixed Registry location is unsafe",
                "固定 Registry 位置不安全"),
            UpdateSourceRegistryLoadIssue.RegistryTooLarge => Localize(
                "the fixed Registry exceeds its size limit",
                "固定 Registry 超過大小限制"),
            UpdateSourceRegistryLoadIssue.InvalidManifest => Localize(
                "the fixed Registry document is invalid",
                "固定 Registry 文件無效"),
            UpdateSourceRegistryLoadIssue.UnstableRead => Localize(
                "the fixed Registry changed while it was being read",
                "讀取固定 Registry 時檔案發生變更"),
            UpdateSourceRegistryLoadIssue.ReplicaConflict => Localize(
                "Registry replicas conflict at the same publication authority",
                "Registry 副本的發布權限或相同版本內容互相衝突"),
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
        };
    }

    private string FormatRegistryAuthorityIssue(UpdateSourceRegistryIssue issue)
    {
        return issue switch
        {
            UpdateSourceRegistryIssue.None => Localize(
                "no Registry authority issue",
                "Registry 發布權限沒有問題"),
            UpdateSourceRegistryIssue.NotConfigured => Localize(
                "Registry authority is not configured",
                "尚未設定 Registry 發布權限"),
            UpdateSourceRegistryIssue.Unavailable => Localize(
                "Registry authority is unavailable",
                "Registry 發布權限無法使用"),
            UpdateSourceRegistryIssue.PermissionDenied => Localize(
                "Registry authority access was denied",
                "沒有權限讀取 Registry 發布權限"),
            UpdateSourceRegistryIssue.AuthenticationRequired => Localize(
                "Registry authority requires authentication",
                "Registry 發布權限需要登入驗證"),
            UpdateSourceRegistryIssue.TimedOut => Localize(
                "Registry authority timed out",
                "Registry 發布權限讀取逾時"),
            UpdateSourceRegistryIssue.Invalid => Localize(
                "Registry authority is invalid",
                "Registry 發布權限無效"),
            UpdateSourceRegistryIssue.RevisionRollback => Localize(
                "the selected Registry revision is older than the last accepted revision",
                "選取的 Registry 版本低於上次已接受版本"),
            UpdateSourceRegistryIssue.RevisionConflict => Localize(
                "the selected Registry reuses an accepted revision with different bytes",
                "選取的 Registry 以不同內容重複使用已接受版本"),
            UpdateSourceRegistryIssue.StateUnavailable => Localize(
                "the durable version state is unavailable",
                "無法讀取持久版本狀態"),
            UpdateSourceRegistryIssue.CandidatesExhausted => Localize(
                "all Registry candidates were rejected",
                "所有 Registry 候選來源均遭拒絕"),
            UpdateSourceRegistryIssue.RegistryChanged => Localize(
                "the Registry changed before source selection completed",
                "來源選取完成前 Registry 已變更"),
            UpdateSourceRegistryIssue.Superseded => Localize(
                "a newer Registry check superseded this result",
                "較新的 Registry 檢查已取代本次結果"),
            UpdateSourceRegistryIssue.CurrentSourceDeprecated => Localize(
                "the retained source is deprecated",
                "目前保留的來源已被標示為停用"),
            _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, null),
        };
    }

    private string FormatCatalogSelfTestIssue(UpdateCatalogLoadIssue issue)
    {
        return issue switch
        {
            UpdateCatalogLoadIssue.None => Localize("catalog verified", "Catalog 驗證成功"),
            UpdateCatalogLoadIssue.SourceMissing => Localize("source folder is missing", "找不到來源資料夾"),
            UpdateCatalogLoadIssue.SourceUnavailable => Localize("source folder is unavailable", "來源資料夾無法使用"),
            UpdateCatalogLoadIssue.PermissionDenied => Localize("source access was denied", "沒有權限讀取來源"),
            UpdateCatalogLoadIssue.UnsafeSource => Localize("source path is unsafe", "來源路徑不安全"),
            UpdateCatalogLoadIssue.CatalogTooLarge => Localize("catalog exceeds its size limit", "Catalog 超過大小限制"),
            UpdateCatalogLoadIssue.InvalidManifest => Localize("catalog is invalid", "Catalog 無效"),
            UpdateCatalogLoadIssue.UnstableRead => Localize("catalog changed while it was being read", "讀取 Catalog 時檔案發生變更"),
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
        };
    }

    private string FormatPackageSelfTestIssue(ManagedVersionInstallIssue issue)
    {
        return issue switch
        {
            ManagedVersionInstallIssue.None => Localize("package verified", "套件驗證成功"),
            ManagedVersionInstallIssue.PackageUnavailable => Localize("a package is missing or unreadable", "套件遺失或無法讀取"),
            ManagedVersionInstallIssue.PackageMismatch => Localize("a package hash does not match the catalog", "套件雜湊與 Catalog 不符"),
            ManagedVersionInstallIssue.UnsafeArchive => Localize("a package archive is unsafe", "套件壓縮檔不安全"),
            ManagedVersionInstallIssue.InvalidPayload => Localize("a package payload is invalid", "套件內容無效"),
            ManagedVersionInstallIssue.IdentityConflict => Localize("a package identity conflicts with the catalog", "套件識別與 Catalog 衝突"),
            ManagedVersionInstallIssue.PromotionFailed => Localize("package verification could not complete", "套件驗證無法完成"),
            ManagedVersionInstallIssue.StateUnavailable => Localize("managed version state is unavailable", "受管版本狀態無法使用"),
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
        };
    }

}
