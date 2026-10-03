using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>History confirmation retains one pending action and bilingual copy.</summary>
public sealed class ReportHistoryConfirmationTests
{
    /// <summary>The existing resource owner supplies precise single-entry and all-entry confirmation text.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoryConfirmationCopyIsLocalized(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        Assert.Equal(chinese ? "刪除這筆報告？" : "Delete this report?", text.DeleteHistoryTitle);
        Assert.Equal(chinese ? "清除全部報告？" : "Clear all reports?", text.ClearHistoryTitle);
        Assert.Equal(chinese ? "只移除這筆歷史紀錄，不會刪除報告檔或輸出檔案。"
            : "This removes only this history entry. Report files and output files will not be deleted.", text.DeleteHistoryDetail);
        Assert.Equal(chinese ? "只移除全部歷史紀錄，不會刪除報告檔或輸出檔案。"
            : "This removes all history entries. Report files and output files will not be deleted.", text.ClearHistoryDetail);
    }

    /// <summary>Clear all cannot replace an already pending single-entry deletion.</summary>
    [Fact]
    public void ClearAllKeepsPendingSingleEntryDeletion()
    {
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(ShellLanguage.English), static () => { });
        reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "first"), "first.json");
        reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "second"), "second.json");
        ReportHistoryEntryViewModel entry = reports.ReportHistoryEntries[0];
        ReportHistoryEntryViewModel retained = reports.ReportHistoryEntries[1];
        reports.RequestReportHistoryDeletionCommand.Execute(entry);
        reports.ClearReportHistoryCommand.Execute(null);
        Assert.Same(entry, reports.PendingHistoryDeletion);
        Assert.Equal("Delete this report?", reports.HistoryDeletionTitle);
        Assert.Equal("Delete", reports.HistoryDeletionConfirmLabel);
        Assert.Equal(2, reports.ReportHistoryCount);
        reports.ConfirmReportHistoryDeletionCommand.Execute(null);
        Assert.Same(retained, Assert.Single(reports.ReportHistoryEntries));
    }

    /// <summary>Closing the Report modal cancels clear all; a later stale confirmation is inert.</summary>
    [Fact]
    public void ClosingReportCancelsClearAll()
    {
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(ShellLanguage.English), static () => { });
        reports.LoadReportJson(ReportJsonSamples.Succeeded(), "report.json");
        IReadOnlyList<ReportHistorySnapshot> expected = reports.ExportReportHistory();
        reports.ClearReportHistoryCommand.Execute(null);
        Assert.True(reports.IsHistoryDeleteConfirmationOpen);
        reports.CloseReportCommand.Execute(null);
        Assert.False(reports.IsHistoryDeleteConfirmationOpen);
        reports.ConfirmReportHistoryDeletionCommand.Execute(null);
        Assert.Equal(expected, reports.ExportReportHistory());
    }

    /// <summary>Clear all cannot open a confirmation without history.</summary>
    [Fact]
    public void EmptyHistoryDoesNotOpenClearConfirmation()
    {
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(ShellLanguage.English), static () => { });
        Assert.False(reports.ClearReportHistoryCommand.CanExecute(null));
        reports.ClearReportHistoryCommand.Execute(null);
        Assert.False(reports.IsHistoryDeleteConfirmationOpen);
        Assert.False(reports.IsReportModalOpen);
    }
}
