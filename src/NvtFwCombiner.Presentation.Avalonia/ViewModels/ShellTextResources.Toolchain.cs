namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    private string ToolchainText(string english, string chinese)
    {
        return Language == ShellLanguage.ChineseTraditional ? chinese : english;
    }
    public string ToolchainTitle { get; } = "Toolchain";
    public string ToolchainSubtitle => ToolchainText("Choose the VC++ Runtime used by Combiner.", "選擇 Combiner 使用的 VC++ Runtime。");
    public string ToolchainBundledLabel => ToolchainText("Bundled runtime", "內附 Runtime");
    public string ToolchainUserLabel => ToolchainText("User runtime", "使用者 Runtime");
    public string ToolchainDefaultLabel => ToolchainText("Default", "預設");
    public string ToolchainSelectHint => ToolchainText("Select a verified local installation", "選擇已驗證的本機安裝");
    public string ToolchainDetectLabel => ToolchainText("Detect", "偵測");
    public string ToolchainBrowseLabel => ToolchainText("Browse", "瀏覽");
    public string ToolchainRuntimeLabel { get; } = "Runtime";
    public string ToolchainVersionLabel => ToolchainText("Version", "版本");
    public string ToolchainArchitectureLabel => ToolchainText("Architecture", "架構");
    public string ToolchainSourceLabel => ToolchainText("Source", "來源");
    public string ToolchainVerificationLabel => ToolchainText("Verification", "驗證");
    public string ToolchainIncludedLabel => ToolchainText("Included with this application", "隨應用程式提供");
    public string ToolchainVerifiedLabel => ToolchainText("Verified", "已驗證");
    public string ToolchainFailedLabel => ToolchainText("Failed", "失敗");
    public string ToolchainUnknownLabel => ToolchainText("Not verified", "尚未驗證");
    public string ToolchainHint => ToolchainText("Detected versions are never selected automatically.\nChanges apply after saving.", "不會自動選取偵測到的版本。\n儲存後才會套用變更。");
    public string ToolchainInvalidHint => ToolchainText("Selected runtime is unavailable or could not be verified. Select Bundled runtime to recover.", "所選 Runtime 無法使用或未通過驗證。可選取內附 Runtime 以復原。");
    public string ToolchainNoCandidatesLabel => ToolchainText("No local runtime candidates were found.", "找不到本機 Runtime 候選項目。");
    public string ToolchainOperationFailedLabel => ToolchainText("The runtime operation could not be completed. Try again.", "無法完成 Runtime 操作，請重試。");
    public string ToolchainRefreshFailedLabel => ToolchainText("Saved. Runtime readiness refresh failed; refresh diagnostics before building.", "已儲存。Runtime 可用狀態更新失敗；建立前請重新整理診斷。");
    public string ToolchainDiscardDetail => ToolchainText("Discard unsaved Toolchain changes and close Settings?", "捨棄未儲存的 Toolchain 變更並關閉設定？");
}
