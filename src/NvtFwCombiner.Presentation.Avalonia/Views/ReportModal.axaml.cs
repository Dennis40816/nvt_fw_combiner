using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Overlay that displays run reports and saves the current report JSON.</summary>
public sealed partial class ReportModal : UserControl
{
    private bool _isSavingReport;
    /// <summary>Initializes the report modal.</summary>
    public ReportModal()
    {
        InitializeComponent();
    }

    private async void SaveReportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            await SaveReportAsync(topLevel.StorageProvider);
        }
    }

    internal async Task SaveReportAsync(IStorageProvider storageProvider)
    {
        if (_isSavingReport || DataContext is not ReportPresentationViewModel viewModel ||
            string.IsNullOrWhiteSpace(viewModel.LoadedReportJson))
        {
            return;
        }

        string reportJson = viewModel.LoadedReportJson;
        string suggestedName = viewModel.ReportSaveFileName;
        bool destinationSelected = false;
        _isSavingReport = true;
        try
        {
            string destinationName;
            using (IStorageFile? file = await FirmwareFilePickerDialogs.PickRunReportSaveFileAsync(storageProvider, suggestedName))
            {
                if (file is null)
                {
                    return;
                }

                destinationSelected = true;
                destinationName = file.Name;
                await using Stream stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync(reportJson);
            }

            viewModel.NotifyReportSaved(destinationName);
        }
        catch (OperationCanceledException) when (!destinationSelected)
        {
            // A cancelled platform picker is not a successful save or an error.
        }
        catch (Exception exception)
        {
            // Contain provider and disposal faults at this UI operation boundary.
            viewModel.NotifyReportSaveFailed(exception.Message);
        }
        finally
        {
            _isSavingReport = false;
        }
    }
}
