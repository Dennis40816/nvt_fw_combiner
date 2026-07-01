using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Main desktop window for the firmware combiner UI.</summary>
public sealed partial class MainWindow : Window
{
    private const double ReportToastFadeStep = 0.12;

    private readonly DispatcherTimer _reportToastHoldTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _reportToastFadeTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };

    /// <summary>Initializes the main window controls.</summary>
    public MainWindow()
    {
        InitializeComponent();
        _reportToastHoldTimer.Tick += ReportToastHoldTimer_OnTick;
        _reportToastFadeTimer.Tick += ReportToastFadeTimer_OnTick;
        DataContext = ShellViewModelFactory.Create();
        if (DataContext is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += ViewModel_OnPropertyChanged;
        }
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
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.CanBuildStandardMerge)
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save merged firmware BIN",
            SuggestedFileName = viewModel.StandardMergeOutputFileName,
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

        await viewModel.BuildStandardMergeAsync(outputPath);
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.HasReportToast) ||
            DataContext is not MainWindowViewModel viewModel)
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

    private void SlotDragOver_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void SlotDrop_OnDrop(object? sender, DragEventArgs e)
    {
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

    private void RemoveGeneralMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: GeneralReplaceMappingViewModel mapping } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.RemoveGeneralReplaceMappingRow(mapping);
    }
}
