using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Main desktop window for the firmware combiner UI.</summary>
public sealed partial class MainWindow : Window
{
    private const string DropZoneDragActiveClass = "dragActive";
    private const double ReportToastFadeStep = 0.12;

    private readonly DispatcherTimer _reportToastHoldTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _reportToastFadeTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };

    /// <summary>Initializes the main window controls.</summary>
    public MainWindow()
        : this(UiLaunchOptions.Empty)
    {
    }

    /// <summary>Initializes the main window controls with command-line startup state.</summary>
    public MainWindow(UiLaunchOptions launchOptions)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);

        InitializeComponent();
        _reportToastHoldTimer.Tick += ReportToastHoldTimer_OnTick;
        _reportToastFadeTimer.Tick += ReportToastFadeTimer_OnTick;
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        ReportHistoryFileStore.LoadInto(viewModel);
        ShellPreferenceFileStore.LoadInto(viewModel);
        DataContext = viewModel;
        ApplyThemePreference(viewModel.SelectedTheme);

        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += ViewModel_OnPropertyChanged;
        }

        ApplyLaunchOptions(viewModel, launchOptions);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= ViewModel_OnPropertyChanged;
        }

        _reportToastHoldTimer.Stop();
        _reportToastFadeTimer.Stop();
        _reportToastHoldTimer.Tick -= ReportToastHoldTimer_OnTick;
        _reportToastFadeTimer.Tick -= ReportToastFadeTimer_OnTick;
        base.OnClosed(e);
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WindowState = WindowState.Maximized;
    }

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
        viewModel.LoadReportJson(json, files[0].Name);
    }

    private static void ApplyLaunchOptions(MainWindowViewModel viewModel, UiLaunchOptions launchOptions)
    {
        ApplyLaunchPage(viewModel, launchOptions.Page);
        if (launchOptions.Issues.Count > 0)
        {
            viewModel.LoadReportError("Startup arguments", string.Join(Environment.NewLine, launchOptions.Issues));
        }

        if (!string.IsNullOrWhiteSpace(launchOptions.ReportPath))
        {
            LoadStartupReport(viewModel, launchOptions.ReportPath);
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
            default:
                break;
        }
    }

    private static void LoadStartupReport(MainWindowViewModel viewModel, string reportPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(reportPath);
            string json = File.ReadAllText(fullPath);
            viewModel.LoadReportJson(json, Path.GetFileName(fullPath));
        }
        catch (ArgumentException exception)
        {
            viewModel.LoadReportError(reportPath, exception.Message);
        }
        catch (IOException exception)
        {
            viewModel.LoadReportError(reportPath, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            viewModel.LoadReportError(reportPath, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            viewModel.LoadReportError(reportPath, exception.Message);
        }
    }

    private async void SaveReportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            string.IsNullOrWhiteSpace(viewModel.LoadedReportJson))
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save run report JSON",
            SuggestedFileName = viewModel.ReportSaveFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("Run report JSON")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"],
                },
                FilePickerFileTypes.All,
            ],
        });

        if (file is null)
        {
            return;
        }

        await using Stream stream = await file.OpenWriteAsync();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(viewModel.LoadedReportJson);
        viewModel.NotifyReportSaved(file.Name);
    }

    private async void BuildMergeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.CanBuildMerge)
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save merged firmware BIN",
            SuggestedFileName = viewModel.MergeOutputFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? outputPath = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        await viewModel.BuildMergeAsync(outputPath);
    }

    private async void BuildReplaceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.CanBuildReplace)
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save replaced firmware BIN",
            SuggestedFileName = viewModel.ReplaceOutputFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? outputPath = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        await viewModel.BuildReplaceAsync(outputPath);
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTheme))
        {
            ApplyThemePreference(viewModel.SelectedTheme);
        }

        if (IsShellPreferenceProperty(e.PropertyName))
        {
            ShellPreferenceFileStore.Save(viewModel);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.ReportHistoryCount))
        {
            ReportHistoryFileStore.Save(viewModel);
        }

        if (e.PropertyName != nameof(MainWindowViewModel.HasReportToast))
        {
            return;
        }

        if (viewModel.HasReportToast)
        {
            _reportToastFadeTimer.Stop();
            _reportToastHoldTimer.Stop();
            viewModel.SetReportToastOpacity(1);
            _reportToastHoldTimer.Start();
        }
        else
        {
            _reportToastHoldTimer.Stop();
            _reportToastFadeTimer.Stop();
        }
    }

    private static bool IsShellPreferenceProperty(string? propertyName)
    {
        return propertyName is
            nameof(MainWindowViewModel.SelectedTheme) or
            nameof(MainWindowViewModel.SelectedStrictness) or
            nameof(MainWindowViewModel.SelectedLanguage);
    }

    private void ApplyThemePreference(string selectedTheme)
    {
        RequestedThemeVariant = selectedTheme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" or "High contrast" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
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

    private void DropZone_OnDragEnter(object? sender, DragEventArgs e)
    {
        bool canDrop = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        SetDropZoneDragActive(sender, canDrop);
    }

    private void SlotDragOver_OnDragOver(object? sender, DragEventArgs e)
    {
        bool canDrop = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        SetDropZoneDragActive(sender, canDrop);
    }

    private void DropZone_OnDragLeave(object? sender, DragEventArgs e)
    {
        SetDropZoneDragActive(sender, isActive: false);
    }

    private void SlotDrop_OnDrop(object? sender, DragEventArgs e)
    {
        SetDropZoneDragActive(sender, isActive: false);

        if (sender is not Control { Tag: string slotId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetSlotFile(slotId, path);
        }
    }

    private void GeneralMappingDrop_OnDrop(object? sender, DragEventArgs e)
    {
        SetDropZoneDragActive(sender, isActive: false);

        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetGeneralReplaceMappingFile(mappingId, path);
        }
    }

    private void GeneralMergeMappingDrop_OnDrop(object? sender, DragEventArgs e)
    {
        SetDropZoneDragActive(sender, isActive: false);

        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = viewModel.SetGeneralMergeMappingFile(mappingId, path);
        }
    }

    private static void SetDropZoneDragActive(object? sender, bool isActive)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (isActive)
        {
            if (!control.Classes.Contains(DropZoneDragActiveClass))
            {
                control.Classes.Add(DropZoneDragActiveClass);
            }
        }
        else
        {
            _ = control.Classes.Remove(DropZoneDragActiveClass);
        }
    }

    private async void BrowseSlotButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string slotId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select BIN file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetSlotFile(slotId, path);
        }
    }

    private async void BrowseGeneralMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select replacement BIN",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetGeneralReplaceMappingFile(mappingId, path);
        }
    }

    private async void BrowseGeneralMergeMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string mappingId } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select source BIN",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Firmware BIN")
                {
                    Patterns = ["*.bin"],
                    MimeTypes = ["application/octet-stream"],
                },
                FilePickerFileTypes.All,
            ],
        });

        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = viewModel.SetGeneralMergeMappingFile(mappingId, path);
        }
    }

    private void RemoveGeneralMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: GeneralReplaceMappingViewModel mapping } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.RemoveGeneralReplaceMappingRow(mapping);
    }

    private void RemoveGeneralMergeMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: GeneralMergeMappingViewModel mapping } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.RemoveGeneralMergeMappingRow(mapping);
    }
}
