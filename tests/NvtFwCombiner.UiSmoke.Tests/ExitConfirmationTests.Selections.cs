using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ExitConfirmationTests
{
    /// <summary>Exit observes selected files across hidden composition pages and clean or dirty utilities.</summary>
    [AvaloniaTheory]
    [InlineData("merge")]
    [InlineData("ab")]
    [InlineData("reference")]
    [InlineData("mapping")]
    [InlineData("hex")]
    [InlineData("hex-dirty")]
    [InlineData("report-pending")]
    public async Task EverySelectedFileCategoryBlocksCloseUntilConfirmed(string category)
    {
        using var workspace = TempWorkspace.Create("v114-exit-selections");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        await AwaitHistoryReadyAsync(window);
        var reportRead = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ReportPublicationResult>? pendingReport = null;
        string path = workspace.Write("selected.bin", [0x12, 0x34]);
        try
        {
            switch (category)
            {
                case "merge":
                    shell.Merge.MergeDpSlot.FilePath = path;
                    break;
                case "ab":
                    shell.ShowMergeCommand.Execute(null);
                    shell.WorkflowSession.SelectedIc = "NT51929";
                    shell.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
                    shell.Merge.MergeSlots[0].FilePath = path;
                    shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
                    break;
                case "reference":
                    shell.Replace.ReplaceBaseSlot.FilePath = path;
                    break;
                case "mapping":
                    shell.ShowMergeCommand.Execute(null);
                    shell.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
                    Assert.Single(shell.Merge.GeneralMergeMappings).FilePath = path;
                    break;
                case "hex":
                case "hex-dirty":
                    shell.ShowHexEditorCommand.Execute(null);
                    await shell.HexEditorWorkspace.LoadAsync(path, TestContext.Current.CancellationToken);
                    if (category == "hex-dirty")
                    {
                        shell.HexEditorWorkspace.SetByteToFfCommand.Execute(0L);
                        Assert.True(shell.HexEditorWorkspace.HasUnsavedChanges);
                    }
                    shell.ShowHomeCommand.Execute(null);
                    Assert.True(shell.Navigation.IsNavigationClearConfirmationOpen);
                    Assert.Equal(shell.Text.LeaveEditorDetail, shell.Navigation.ConfirmationDetail);
                    shell.Navigation.CancelNavigationClearCommand.Execute(null);
                    Assert.True(shell.IsHexEditorVisible);
                    shell.ShowHomeCommand.Execute(null);
                    shell.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
                    Assert.True(shell.IsHomeVisible);
                    Assert.True(shell.HexEditorWorkspace.HasDocument);
                    Assert.Equal(category == "hex-dirty", shell.HexEditorWorkspace.HasUnsavedChanges);
                    break;
                case "report-pending":
                    pendingReport = shell.Reports.LoadReportFileAsync(
                        _ => new ValueTask<string>(reportRead.Task), "pending.json", TestContext.Current.CancellationToken);
                    Assert.False(pendingReport.IsCompleted);
                    Assert.False(shell.Reports.HasLoadedReport);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }
            Assert.True(shell.HasSelectedFiles);
            ShellPage page = shell.SelectedPage;
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(shell.Navigation.IsExitConfirmationOpen);
            Assert.True(window.IsEnabled);
            shell.Navigation.CancelNavigationClearCommand.Execute(null);
            Assert.Equal(page, shell.SelectedPage);
            Assert.True(shell.HasSelectedFiles);
            if (pendingReport is not null)
            {
                _ = reportRead.TrySetResult("{}");
                Assert.Equal(ReportPublicationOutcome.Published, (await pendingReport).Outcome);
            }
        }
        finally
        {
            _ = reportRead.TrySetResult("{}");
            if (pendingReport is not null)
            {
                _ = await pendingReport;
            }
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>Ordinary startup and automatic history restoration do not invent user file selections.</summary>
    [AvaloniaFact]
    public async Task EmptyAndRestoredHistoryWindowsCloseWithoutPrompt()
    {
        using var workspace = TempWorkspace.Create("v114-exit-restored");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        for (int generation = 0; generation < 2; generation++)
        {
            using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
            var shell = (MainWindowViewModel)window.DataContext!;
            window.Show();
            await AwaitHistoryReadyAsync(window);
            Assert.False(shell.HasSelectedFiles);
            Assert.Null(shell.LoadedHexEditorWorkspace);
            if (generation == 1)
            {
                Assert.NotEmpty(shell.Reports.ReportHistoryEntries);
            }
            TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Closed += (_, _) => closed.TrySetResult();
            window.Close();
            Assert.False(shell.Navigation.IsNavigationClearConfirmationOpen);
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            if (generation == 0)
            {
                var seed = new ReportPresentationViewModel(() => ShellTextResources.For(ShellLanguage.English), static () => { });
                seed.LoadReportJson("{}", "previous-session.json");
                await ReportHistoryFileStore.SaveAsync(services.LocalFiles, ReportHistoryFileStore.DefaultHistoryPath,
                    seed.ExportReportHistory(), TestContext.Current.CancellationToken);
            }
        }
    }
}
