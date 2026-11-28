using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Approved notices keep report state, technical context and activity severity.</summary>
public sealed class ApprovedNoticeMessageTests
{
    /// <summary>Error-report creation retains the source name and the approved bilingual notification.</summary>
    [Theory]
    [InlineData(false, "An error report was created; open it for details.")]
    [InlineData(true, "已建立錯誤報告，請開啟查看詳細內容。")]
    public void ErrorReportToastKeepsSource(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(language), static () => { });
        reports.LoadReportError("source.json", "Original load failure");

        Assert.Equal(expected + Environment.NewLine + "source.json", reports.ReportToastText);
        Assert.True(reports.HasReportToast);
        Assert.True(reports.HasLoadedReport);
        Assert.True(reports.LoadedReport.HasPrimaryIssue);
        Assert.Contains("Original load failure", reports.LoadedReport.PrimaryIssue.Detail, StringComparison.Ordinal);
    }

    /// <summary>Save notifications retain destination and exception details without changing the report outcome.</summary>
    [Theory]
    [InlineData(false, true, "The report was saved, but interrupted replacement may leave it incomplete; save a local copy.")]
    [InlineData(true, true, "報告已儲存，但取代過程若中斷可能導致檔案不完整，請另存本機副本。")]
    [InlineData(false, false, "Report save failed and the file may be incomplete; save it again.")]
    [InlineData(true, false, "報告儲存失敗且檔案可能不完整，請重新儲存。")]
    public void SaveToastKeepsReportAndTechnicalDetail(bool chinese, bool saved, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(language), static () => { });
        string json = ReportJsonSamples.Succeeded();
        reports.LoadReportJson(json, "source.json");
        ReportReviewViewModel original = reports.LoadedReport;

        ShowSaveToast(reports, saved);

        Assert.Equal(expected + Environment.NewLine + "destination-or-exception", reports.ReportToastText);
        Assert.Equal(json, reports.LoadedReportJson);
        Assert.Same(original, reports.LoadedReport);
        Assert.Equal(0, reports.LoadedReport.BlockingIssueCount);
        Assert.True(reports.HasReportToast);
    }

    /// <summary>Context-selection notifications retain the discarded file name in the existing toast detail.</summary>
    [Theory]
    [InlineData(false, "The previous BIN selection was not kept for this IC; select a compatible BIN.")]
    [InlineData(true, "切換至此 IC 後未保留先前的 BIN 選擇，請選擇相容的 BIN。")]
    public void SelectionToastKeepsFileName(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        Assert.Equal(expected + Environment.NewLine + "previous.bin",
            ShellTextResources.For(language).FormatFirmwareSelectionNotRetainedToast("previous.bin"));
    }

    /// <summary>Copy failure retains the original report and displays the approved recovery instruction.</summary>
    [Theory]
    [InlineData(false, "Copy failed; try again or use Save report.")]
    [InlineData(true, "複製失敗，請重試或儲存報告。")]
    public void CopyToastKeepsReport(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        ShellTextResources text = ShellTextResources.For(language);
        var reports = new ReportPresentationViewModel(() => text, static () => { });
        reports.LoadReportJson(ReportJsonSamples.Succeeded(), "source.json");
        ReportReviewViewModel original = reports.LoadedReport;

        reports.SetShellToast(text.ReportCopyFailedTitle, text.ReportCopyFailedDetail);

        Assert.Equal(expected, reports.ReportToastText);
        Assert.Same(original, reports.LoadedReport);
        Assert.Equal(0, reports.LoadedReport.BlockingIssueCount);
    }

    /// <summary>Activity titles preserve the severity, importance and context supplied by Application.</summary>
    [Theory]
    [InlineData(false, "activity.composition.preview-failed", SystemActivitySeverity.Warning, "Preview needs attention; check Run reports for details.")]
    [InlineData(true, "activity.composition.preview-failed", SystemActivitySeverity.Warning, "Preview 需要確認，請到「執行報告」查看詳細內容。")]
    [InlineData(false, "activity.composition.preview-failed", SystemActivitySeverity.Error, "Preview needs attention; check Run reports for details.")]
    [InlineData(true, "activity.composition.preview-failed", SystemActivitySeverity.Error, "Preview 需要確認，請到「執行報告」查看詳細內容。")]
    [InlineData(false, "activity.composition.build-failed", SystemActivitySeverity.Error, "Build failed; check Run reports for details.")]
    [InlineData(true, "activity.composition.build-failed", SystemActivitySeverity.Error, "Build 失敗，請到「執行報告」查看詳細內容。")]
    [InlineData(false, "activity.diagnostic.export-failed", SystemActivitySeverity.Error, "System activity export failed; select Export to try another local location.")]
    [InlineData(true, "activity.diagnostic.export-failed", SystemActivitySeverity.Error, "系統活動匯出失敗，請按「匯出」改選其他本機位置。")]
    public void ActivitySentenceKeepsSeverity(bool chinese, string code,
        SystemActivitySeverity severity, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var activity = new SystemActivityEntry(1, DateTimeOffset.UnixEpoch,
            new SystemActivityDraft(code, SystemActivityImportance.Important,
                SystemActivityCategory.Composition, severity, "workflow", "NT51929"));
        ShellTextResources text = ShellTextResources.For(language);

        Assert.Equal(expected, text.GetSystemActivityTitle(activity));
        Assert.Equal(severity, activity.Severity);
        Assert.Equal(SystemActivityImportance.Important, activity.Importance);
        Assert.Equal("workflow · NT51929", text.GetSystemActivityDetail(activity));
        Assert.Equal(severity == SystemActivitySeverity.Error
            ? language == ShellLanguage.ChineseTraditional ? "失敗" : "Failed"
            : language == ShellLanguage.ChineseTraditional ? "注意" : "Attention", text.GetSystemActivityStatus(activity.Severity));
    }

    /// <summary>Export failure uses the same approved sentence for the status and activity surfaces.</summary>
    [Theory]
    [InlineData(false, "System activity export failed; select Export to try another local location.")]
    [InlineData(true, "系統活動匯出失敗，請按「匯出」改選其他本機位置。")]
    public void ExportFailureUsesApprovedSentence(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        Assert.Equal(expected, ShellTextResources.For(language).DiagnosticsExportFailedLabel);
    }

    /// <summary>Unresolved diagnostic activation keeps its existing generic title.</summary>
    [Theory]
    [InlineData(false, "System issue detected")]
    [InlineData(true, "偵測到系統問題")]
    public void DiagnosticActivationRemainsUnchanged(bool chinese, string expected)
    {
        ShellLanguage language = chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English;
        var activity = new SystemActivityEntry(1, DateTimeOffset.UnixEpoch,
            new SystemActivityDraft(SystemActivityCodes.DiagnosticActivated, SystemActivityImportance.Important,
                SystemActivityCategory.Diagnostics, SystemActivitySeverity.Warning, "opaque-diagnostic-code"));
        Assert.Equal(expected, ShellTextResources.For(language).GetSystemActivityTitle(activity));
        Assert.Equal(SystemActivitySeverity.Warning, activity.Severity);
    }

    /// <summary>Exercises the existing save notification entry points with identical technical context.</summary>
    private static void ShowSaveToast(ReportPresentationViewModel reports, bool saved)
    {
        if (saved)
        {
            reports.NotifyReportSaved("destination-or-exception", bestEffortProviderWrite: true);
        }
        else
        {
            reports.NotifyReportSaveFailed("destination-or-exception");
        }
    }
}
