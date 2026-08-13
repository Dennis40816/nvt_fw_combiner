using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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

    private static bool HasStartupReportStage(UiLaunchOptions launchOptions)
    {
        return launchOptions.Issues.Count > 0 ||
            !string.IsNullOrWhiteSpace(launchOptions.ReportPath) ||
            launchOptions.OpenReport;
    }

    internal static async Task ApplyStartupReportAsync(
        MainWindowViewModel viewModel,
        ILocalFileStore reportFiles,
        UiLaunchOptions launchOptions,
        Action<long, long> progress,
        CancellationToken cancellationToken)
    {
        string? terminalDiagnostic = null;
        if (launchOptions.Issues.Count > 0)
        {
            terminalDiagnostic = string.Join(Environment.NewLine, launchOptions.Issues);
            viewModel.Reports.LoadReportError("Startup arguments", terminalDiagnostic);
        }

        if (!string.IsNullOrWhiteSpace(launchOptions.ReportPath))
        {
            ReportPublicationResult result = await viewModel.Reports.LoadReportFileAsync(
                token => reportFiles.ReadTextAsync(
                    launchOptions.ReportPath,
                    MaximumStandaloneReportBytes,
                    token,
                    update => ReportStartupFileProgress(progress, update, token)),
                Path.GetFileName(launchOptions.ReportPath),
                cancellationToken);
            if (result.Outcome != ReportPublicationOutcome.Published)
            {
                RequireStartupPublication(result);
            }
        }

        if (launchOptions.OpenReport && viewModel.Reports.ShowReportCommand.CanExecute(null))
        {
            viewModel.Reports.ShowReportCommand.Execute(null);
        }
        else if (launchOptions.OpenReport)
        {
            terminalDiagnostic =
                "--open-report requires a loaded report. Pass --load-report <path> or --report <path>.";
            viewModel.Reports.LoadReportError("Startup report", terminalDiagnostic);
        }

        if (terminalDiagnostic is not null)
        {
            throw new InvalidOperationException(terminalDiagnostic);
        }
    }

    internal static void RequireStartupPublication(ReportPublicationResult result)
    {
        switch (result.Outcome)
        {
            case ReportPublicationOutcome.Published:
                return;
            case ReportPublicationOutcome.Superseded:
                throw new ShellPreloadSupersededException();
            case ReportPublicationOutcome.Failed:
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Diagnostic)
                    ? "Report publication returned an empty failure diagnostic."
                    : result.Diagnostic);
            case ReportPublicationOutcome.Unknown:
            default:
                throw new InvalidOperationException("Report publication returned an invalid terminal result.");
        }
    }

    private static void ReportStartupFileProgress(
        Action<long, long> progress,
        LocalFileReadProgress update,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            progress(update.BytesRead, update.TotalBytes);
            return;
        }
        Dispatcher.UIThread.InvokeAsync(
            () => progress(update.BytesRead, update.TotalBytes),
            DispatcherPriority.Background,
            cancellationToken).GetAwaiter().GetResult();
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
