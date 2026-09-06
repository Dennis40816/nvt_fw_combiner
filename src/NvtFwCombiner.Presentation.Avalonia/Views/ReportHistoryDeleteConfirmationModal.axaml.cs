using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirms one history deletion without touching the report or output files.</summary>
public sealed partial class ReportHistoryDeleteConfirmationModal : UserControl
{
    private ReportHistoryEntryViewModel? _returnEntry;

    /// <summary>Initializes the accessible confirmation overlay.</summary>
    public ReportHistoryDeleteConfirmationModal()
    {
        InitializeComponent();
        PropertyChanged += Confirmation_OnPropertyChanged;
    }

    private void Confirmation_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty || VisualRoot is null)
        {
            return;
        }

        if (IsVisible && DataContext is ReportPresentationViewModel viewModel)
        {
            _returnEntry = viewModel.PendingHistoryDeletion;
            Dispatcher.UIThread.Post(() =>
            {
                if (IsEffectivelyVisible)
                {
                    _ = CancelButton.Focus(NavigationMethod.Tab);
                }
            }, DispatcherPriority.Input);
        }
        else if (!IsVisible)
        {
            Control? report = this.GetVisualAncestors().OfType<Control>()
                .FirstOrDefault(control => control is ReportModal or MessageCenterModal);
            ReportHistoryEntryViewModel? entry = _returnEntry;
            _returnEntry = null;
            Dispatcher.UIThread.Post(() =>
            {
                if (report?.IsEffectivelyVisible != true || IsVisible)
                {
                    return;
                }
                Button? trash = report.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
                    ReferenceEquals(button.DataContext, entry) && button.Classes.Contains("danger"));
                trash ??= report.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
                    button.IsEffectivelyVisible && button.IsEnabled && button.Classes.Contains("reportListRow"));
                trash ??= report.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
                    button.IsEffectivelyVisible && button.IsEnabled && button.Name == "LoadRunReportButton");
                _ = (trash as Control ?? report).Focus(NavigationMethod.Tab);
            }, DispatcherPriority.Input);
        }
    }

    private void Confirmation_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is ReportPresentationViewModel viewModel)
        {
            viewModel.CancelReportHistoryDeletionCommand.Execute(null);
            e.Handled = true;
        }
    }
}
