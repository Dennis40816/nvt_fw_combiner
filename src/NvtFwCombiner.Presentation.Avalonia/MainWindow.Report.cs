using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private const long MaximumStandaloneReportBytes = 10L * 1024 * 1024;
    private const double ReportToastFadeStep = 0.12;

    private async void LoadReportJsonButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load run report JSON",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Run report JSON")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"],
                },
            ],
        });

        if (files.Count == 0 || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        IStorageFile file = files[0];
        _ = await viewModel.Reports.LoadReportFileAsync(
            token => _hostServices.LocalFiles.ReadTextAsync(
                _ => new ValueTask<Stream>(file.OpenReadAsync()),
                MaximumStandaloneReportBytes,
                token),
            file.Name,
            _startupLoadCancellation.Token);
    }

    private static async Task ApplyDeferredLaunchOptionsAsync(
        MainWindowViewModel viewModel,
        ILocalFileStore reportFiles,
        UiLaunchOptions launchOptions,
        CancellationToken cancellationToken)
    {
        bool historyPublished = await viewModel.Reports.LoadReportHistoryAsync(
            token => ReportHistoryFileStore.LoadAsync(
                reportFiles,
                ReportHistoryFileStore.DefaultHistoryPath,
                token),
            cancellationToken);
        if (!historyPublished)
        {
            return;
        }

        if (launchOptions.Issues.Count > 0)
        {
            viewModel.Reports.LoadReportError("Startup arguments", string.Join(Environment.NewLine, launchOptions.Issues));
        }

        if (!string.IsNullOrWhiteSpace(launchOptions.ReportPath))
        {
            bool reportPublished = await viewModel.Reports.LoadReportFileAsync(
                token => reportFiles.ReadTextAsync(
                    launchOptions.ReportPath,
                    MaximumStandaloneReportBytes,
                    token),
                Path.GetFileName(launchOptions.ReportPath),
                cancellationToken);
            if (!reportPublished)
            {
                return;
            }
        }

        if (!launchOptions.OpenReport)
        {
            return;
        }

        if (viewModel.Reports.ShowReportCommand.CanExecute(null))
        {
            viewModel.Reports.ShowReportCommand.Execute(null);
        }
        else
        {
            viewModel.Reports.LoadReportError(
                "Startup report",
                "--open-report requires a loaded report. Pass --load-report <path> or --report <path>.");
        }
    }

    private static void ApplyLaunchPage(MainWindowViewModel viewModel, ShellPage? page)
    {
        switch (page)
        {
            case ShellPage.Home:
                viewModel.ShowHomeCommand.Execute(null);
                break;
            case ShellPage.Settings:
                viewModel.ShowSettingsCommand.Execute(null);
                break;
            case ShellPage.Merge:
                viewModel.ShowMergeCommand.Execute(null);
                break;
            case ShellPage.Replace:
                viewModel.ShowReplaceCommand.Execute(null);
                break;
            case ShellPage.HexEditor:
                viewModel.ShowHexEditorCommand.Execute(null);
                break;
            default:
                break;
        }
    }

    private void ReportToastHoldTimer_OnTick(object? sender, EventArgs e)
    {
        _reportToastHoldTimer.Stop();
        _reportToastFadeTimer.Start();
    }

    private void ReportToastFadeTimer_OnTick(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.Reports.HasReportToast)
        {
            double nextOpacity = viewModel.Reports.ReportToastOpacity - ReportToastFadeStep;
            if (nextOpacity <= 0)
            {
                _reportToastFadeTimer.Stop();
                if (viewModel.Reports.DismissReportToastCommand.CanExecute(null))
                {
                    viewModel.Reports.DismissReportToastCommand.Execute(null);
                }

                return;
            }

            viewModel.Reports.SetReportToastOpacity(nextOpacity);
        }
        else
        {
            _reportToastFadeTimer.Stop();
        }
    }
}
