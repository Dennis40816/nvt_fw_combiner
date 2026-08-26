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
        IsSourceChecking = true;
        VersionOperationStatus = Localize(
            "Running the update environment self-test…",
            "正在執行更新環境自我測試…");
        try
        {
            VersionEnvironmentSelfTestResult result =
                await _versionManagement.RunEnvironmentSelfTestAsync(cancellationToken);
            VersionOperationStatus = FormatEnvironmentSelfTestResult(result);
        }
        finally
        {
            IsSourceChecking = false;
            IsVersionBusy = false;
        }
    }

    private string FormatEnvironmentSelfTestResult(VersionEnvironmentSelfTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.RegistryIssue != UpdateSourceRegistryLoadIssue.None)
        {
            return Localize(
                $"Self-test failed: {FormatRegistrySelfTestIssue(result.RegistryIssue)}.",
                $"自我測試失敗：{FormatRegistrySelfTestIssue(result.RegistryIssue)}。");
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
        return string.IsNullOrEmpty(details) ? summary : $"{summary} {details}";
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
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
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
