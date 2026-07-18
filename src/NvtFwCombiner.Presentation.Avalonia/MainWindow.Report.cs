using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
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

        await using Stream stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);
        string json = await reader.ReadToEndAsync();
        await viewModel.LoadReportJsonAsync(json, files[0].Name);
    }

    private static void ApplyInitialLaunchOptions(MainWindowViewModel viewModel, UiLaunchOptions launchOptions)
    {
        ApplyLaunchPage(viewModel, launchOptions.Page);
    }

    private static async Task ApplyDeferredLaunchOptionsAsync(
        MainWindowViewModel viewModel,
        UiLaunchOptions launchOptions,
        CancellationToken cancellationToken)
    {
        bool historyPublished = await viewModel.LoadReportHistoryAsync(
            ReportHistoryFileStore.LoadAsync,
            cancellationToken);
        if (!historyPublished)
        {
            return;
        }

        if (launchOptions.Issues.Count > 0)
        {
            viewModel.LoadReportError("Startup arguments", string.Join(Environment.NewLine, launchOptions.Issues));
        }

        if (!string.IsNullOrWhiteSpace(launchOptions.ReportPath))
        {
            bool reportPublished = await LoadStartupReportAsync(
                viewModel,
                launchOptions.ReportPath,
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

        if (!viewModel.ShowReportCommand.CanExecute(null))
        {
            viewModel.LoadReportError(
                "Startup report",
                "--open-report requires a loaded report. Pass --load-report <path> or --report <path>.");
        }

        if (viewModel.ShowReportCommand.CanExecute(null))
        {
            viewModel.ShowReportCommand.Execute(null);
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

    private static Task<bool> LoadStartupReportAsync(
        MainWindowViewModel viewModel,
        string reportPath,
        CancellationToken cancellationToken)
    {
        return viewModel.LoadReportJsonAsync(
            token => Task.Run(
                () => File.ReadAllText(Path.GetFullPath(reportPath)),
                token),
            Path.GetFileName(reportPath),
            cancellationToken);
    }

    private void ReportToastHoldTimer_OnTick(object? sender, EventArgs e)
    {
        _reportToastHoldTimer.Stop();
        _reportToastFadeTimer.Start();
    }

    private void ReportToastFadeTimer_OnTick(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.HasReportToast)
        {
            double nextOpacity = viewModel.ReportToastOpacity - ReportToastFadeStep;
            if (nextOpacity <= 0)
            {
                _reportToastFadeTimer.Stop();
                if (viewModel.DismissReportToastCommand.CanExecute(null))
                {
                    viewModel.DismissReportToastCommand.Execute(null);
                }

                return;
            }

            viewModel.SetReportToastOpacity(nextOpacity);
        }
        else
        {
            _reportToastFadeTimer.Stop();
        }
    }
}
