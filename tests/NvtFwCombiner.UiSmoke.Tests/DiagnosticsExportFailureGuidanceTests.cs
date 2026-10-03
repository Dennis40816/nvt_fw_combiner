using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Diagnostics export failures provide safe next steps in both shell languages.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class DiagnosticsExportFailureGuidanceTests
{
    /// <summary>Localized export failure guidance stays fully visible at the minimum window size.</summary>
    [AvaloniaTheory]
    [InlineData(false, "Diagnostics could not be exported. Try again or choose another location.", false)]
    [InlineData(true, "無法匯出診斷。請重試或改選儲存位置。", false)]
    [InlineData(false, "Diagnostics could not be exported. Try again or choose another location.", true)]
    [InlineData(true, "無法匯出診斷。請重試或改選儲存位置。", true)]
    public async Task ExportFailureStatusProvidesVisibleGuidanceAtMinimumWindowSize(
        bool traditionalChinese,
        string expectedStatus,
        bool revisedWording)
    {
        ShellTextResources text = ShellTextResources.For(
            traditionalChinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.3-test");
        var reports = new ReportPresentationViewModel(() => text, static () => { });
        var exporter = new FailingDiagnosticsExporter();
        var viewModel = new MessageCenterViewModel(
            () => text,
            services.SystemInformation,
            services.ExternalEnvironmentLoader,
            exporter,
            reports,
            static _ => { });
        viewModel.OpenCommand.Execute(null);

        await viewModel.ExportAsync("diagnostics.json", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, viewModel.ExportStatus);
        Assert.True(viewModel.HasExportFailure);
        SystemActivityEntry failure = Assert.Single(
            services.SystemInformation.Activity,
            static entry => entry.Code == SystemActivityCodes.DiagnosticsExportFailed);
        Assert.Equal(SystemActivityImportance.Important, failure.Importance);
        Assert.Equal(SystemActivityCategory.Diagnostics, failure.Category);
        Assert.Equal(SystemActivitySeverity.Error, failure.Severity);
        if (revisedWording)
        {
            expectedStatus = traditionalChinese
                ? "診斷無法寫入選定的位置。請重試或改選另一個儲存位置。"
                : "Diagnostics could not be written to the selected location. Try again or choose another location.";
            typeof(MessageCenterViewModel).GetProperty(nameof(MessageCenterViewModel.ExportStatus))!
                .SetValue(viewModel, expectedStatus);
            Assert.True(viewModel.HasExportFailure);
        }

        var modal = new MessageCenterModal { DataContext = viewModel };
        var window = new Window
        {
            Width = 980,
            Height = 640,
            RequestedThemeVariant = ThemeVariant.Light,
            Content = modal,
        };
        foreach (string file in new[]
        {
            "MainWindowStyles.axaml",
            "MainWindowButtonStyles.axaml",
            "MainWindowVisualStyles.axaml",
        })
        {
            var source = new Uri($"avares://NvtFwCombiner.Presentation.Avalonia/Styles/{file}");
            window.Styles.Add(new StyleInclude(source) { Source = source });
        }
        try
        {
            window.Show();
            window.Measure(new Size(980, 640));
            window.Arrange(new Rect(0, 0, 980, 640));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.Equal(new Size(980, 640), window.ClientSize);

            Grid activity = Assert.IsType<Grid>(modal.FindControl<Control>("SystemActivityRoot"));
            Assert.True(activity.IsVisible);
            TextBlock status = Assert.Single(activity.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == expectedStatus);
            Assert.Contains("exportFailureStatus", status.Classes);
            TextBlock notice = Assert.Single(activity.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == text.SessionActivityNotice);
            Button refresh = Assert.Single(activity.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, viewModel.RefreshCommand));
            Assert.InRange(status.TextLayout.WidthIncludingTrailingWhitespace, 1, status.Bounds.Width + 0.5);
            Assert.InRange(status.TextLayout.Height, 1, status.Bounds.Height + 0.5);
            Assert.All(status.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));

            var statusBounds = new Rect(
                Assert.IsType<Point>(status.TranslatePoint(default, activity)), status.Bounds.Size);
            foreach (Control neighbor in new Control[] { notice, refresh })
            {
                var neighborBounds = new Rect(
                    Assert.IsType<Point>(neighbor.TranslatePoint(default, activity)), neighbor.Bounds.Size);
                Assert.False(statusBounds.Intersects(neighborBounds),
                    $"{expectedStatus}: status {statusBounds} intersects {neighbor.GetType().Name} {neighborBounds}.");
            }
            var noticeTextBounds = new Rect(
                Assert.IsType<Point>(notice.TranslatePoint(default, activity)),
                new Size(notice.TextLayout.WidthIncludingTrailingWhitespace, notice.TextLayout.Height));
            Assert.False(statusBounds.Intersects(noticeTextBounds),
                $"{expectedStatus}: status {statusBounds} intersects notice text {noticeTextBounds}.");
            foreach (Control container in status.GetVisualAncestors().OfType<Control>())
            {
                var bounds = new Rect(
                    Assert.IsType<Point>(status.TranslatePoint(default, container)), status.Bounds.Size);
                Assert.True(bounds.Left >= -0.5 && bounds.Top >= -0.5 &&
                    bounds.Right <= container.Bounds.Width + 0.5 && bounds.Bottom <= container.Bounds.Height + 0.5,
                    $"{expectedStatus}: status {bounds} is outside {container.GetType().Name} {container.Bounds.Size}.");
            }

            // Keep the existing short success message on one line in the same footer.
            exporter.ShouldFail = false;
            await viewModel.ExportAsync("diagnostics.json", TestContext.Current.CancellationToken);
            Assert.False(viewModel.HasExportFailure);
            Assert.Equal(text.DiagnosticsExportedLabel, status.Text);
            Assert.DoesNotContain("exportFailureStatus", status.Classes);
            window.Measure(new Size(980, 640));
            window.Arrange(new Rect(0, 0, 980, 640));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            _ = Assert.Single(status.TextLayout.TextLines);
            viewModel.ReportExportFailure();
            viewModel.ApplyLanguageChanged();
            Assert.False(viewModel.HasExportFailure);
            Assert.Equal(string.Empty, viewModel.ExportStatus);
            viewModel.ReportExportFailure();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            Assert.False(viewModel.HasExportFailure);
            Assert.Equal(string.Empty, viewModel.ExportStatus);
            viewModel.ReportExportFailure();
            viewModel.CloseCommand.Execute(null);
            viewModel.OpenCommand.Execute(null);
            Assert.False(viewModel.HasExportFailure);
            Assert.Equal(string.Empty, viewModel.ExportStatus);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class FailingDiagnosticsExporter : ISystemDiagnosticsExporter
    {
        public bool ShouldFail { get; set; } = true;

        public ValueTask ExportAsync(
            SystemDiagnosticsBundle bundle,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ShouldFail ? ValueTask.FromException(new IOException("private exporter detail")) : ValueTask.CompletedTask;
        }
    }
}
