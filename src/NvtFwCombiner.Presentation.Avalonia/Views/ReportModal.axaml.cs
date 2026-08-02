using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Overlay that displays run reports and saves the current report JSON.</summary>
public sealed partial class ReportModal : UserControl
{
    /// <summary>Initializes the report modal.</summary>
    public ReportModal()
    {
        InitializeComponent();
    }

    private async void SaveReportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReportPresentationViewModel viewModel ||
            string.IsNullOrWhiteSpace(viewModel.LoadedReportJson))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        IStorageFile? file = await FirmwareFilePickerDialogs.PickRunReportSaveFileAsync(
            topLevel.StorageProvider,
            viewModel.ReportSaveFileName);
        if (file is null)
        {
            return;
        }

        await using Stream stream = await file.OpenWriteAsync();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(viewModel.LoadedReportJson);
        viewModel.NotifyReportSaved(file.Name);
    }
}
