using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using System.Text;
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
        ModalInitialFocus.Register(this, () => CloseButton);
    }

    private void ReportModal_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift) ||
            e.Handled || !IsEffectivelyVisible ||
            DataContext is not ReportPresentationViewModel { IsHistoryDeleteConfirmationOpen: false })
        {
            return;
        }

        // Use the visible Report tree, including only the selected tab's header and content.
        Control[] stops = [.. ReportSurface.GetVisualDescendants().OfType<Control>().Where(control =>
            control.Focusable && KeyboardNavigation.GetIsTabStop(control) &&
            control.IsEffectivelyVisible && control.IsEffectivelyEnabled &&
            control is not TabItem { IsSelected: false })];
        if (stops.Length == 0)
        {
            return;
        }

        int current = Array.IndexOf(stops, TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement());
        bool backwards = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        int next = current < 0 ? (backwards ? stops.Length - 1 : 0)
            : (current + (backwards ? -1 : 1) + stops.Length) % stops.Length;
        e.Handled = stops[next].Focus(NavigationMethod.Tab, e.KeyModifiers);
    }

    private void SaveReportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        UiEventAdapter.Run(this, "ReportModal.SaveReport", _ => HandleSaveReportButton_OnClickAsync(sender, e));
    }

    private async Task HandleSaveReportButton_OnClickAsync(object? sender, RoutedEventArgs e)
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

        using IDisposable saveOperation = viewModel.BeginSaveOperation();
        string reportJson = viewModel.LoadedReportJson;
        string suggestedName = viewModel.ReportSaveFileName;
        bool destinationSelected = false;
        _isSavingReport = true;
        try
        {
            string destinationName;
            bool bestEffortProviderWrite;
            using (IStorageFile? file = await FirmwareFilePickerDialogs.PickRunReportSaveFileAsync(storageProvider, suggestedName))
            {
                if (file is null)
                {
                    return;
                }

                destinationSelected = true;
                destinationName = file.Name;
                string? localPath = file.TryGetLocalPath();
                bestEffortProviderWrite = localPath is null;
                if (localPath is not null)
                {
                    await (viewModel.LocalFiles ?? throw new InvalidOperationException("Local report storage is unavailable."))
                        .WriteAsync(localPath, Encoding.UTF8.GetBytes(reportJson), CancellationToken.None);
                }
                else
                {
                    await using Stream stream = await file.OpenWriteAsync();
                    await using var writer = new StreamWriter(stream, leaveOpen: true);
                    await writer.WriteAsync(reportJson);
                }
            }

            await viewModel.NotifyReportSavedWhenAllowedAsync(destinationName, bestEffortProviderWrite);
        }
        catch (OperationCanceledException) when (!destinationSelected)
        {
            // A cancelled platform picker is not a successful save or an error.
        }
        catch (Exception exception)
        {
            // Contain provider and disposal faults at this UI operation boundary.
            await viewModel.NotifyReportSaveFailedWhenAllowedAsync(exception.Message);
        }
        finally
        {
            _isSavingReport = false;
        }
    }
}
