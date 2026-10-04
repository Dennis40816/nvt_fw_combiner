using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Confirms history deletion without touching the report or output files.</summary>
public sealed partial class ReportHistoryDeleteConfirmationModal : UserControl
{
    private ReportHistoryEntryViewModel? _returnEntry;
    private bool _returnClearAll;
    private Control? _returnFocus;
    private Control? _report;

    /// <summary>Initializes the accessible confirmation overlay.</summary>
    public ReportHistoryDeleteConfirmationModal()
    {
        InitializeComponent();
        PropertyChanged += Confirmation_OnPropertyChanged;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _report = this.GetVisualAncestors().OfType<Control>()
            .FirstOrDefault(control => control is ReportModal or MessageCenterModal);
        _report?.AddHandler(Button.ClickEvent, CaptureDeletionTrigger, handledEventsToo: true);
        FocusConfirmation();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _report?.RemoveHandler(Button.ClickEvent, CaptureDeletionTrigger);
        _report = null;
        _returnFocus = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void CaptureDeletionTrigger(object? sender, RoutedEventArgs e)
    {
        // Click bubbles before Button executes its command and the report content loses focus.
        if (e.Source is Button button && DataContext is ReportPresentationViewModel viewModel &&
            (ReferenceEquals(button.Command, viewModel.ClearReportHistoryCommand) ||
             ReferenceEquals(button.Command, viewModel.RequestReportHistoryDeletionCommand)))
        {
            _returnFocus = button;
        }
    }

    private void FocusConfirmation()
    {
        if (IsVisible && DataContext is ReportPresentationViewModel { IsHistoryDeleteConfirmationOpen: true } viewModel)
        {
            _returnFocus ??= TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            _returnEntry = viewModel.PendingHistoryDeletion;
            _returnClearAll = viewModel.IsClearAllHistoryDeletionPending;
            Dispatcher.UIThread.Post(() =>
            {
                if (IsEffectivelyVisible)
                {
                    _ = CancelButton.Focus(NavigationMethod.Tab);
                }
            }, DispatcherPriority.Input);
        }
    }

    private void Confirmation_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty || VisualRoot is null)
        {
            return;
        }

        if (IsVisible)
        {
            FocusConfirmation();
        }
        else if (!IsVisible)
        {
            Control? report = _report;
            ReportHistoryEntryViewModel? entry = _returnEntry;
            bool clearAll = _returnClearAll;
            Control? returnFocus = _returnFocus;
            _returnEntry = null;
            _returnClearAll = false;
            _returnFocus = null;
            Dispatcher.UIThread.Post(() =>
            {
                if (report?.IsEffectivelyVisible != true || IsVisible)
                {
                    return;
                }
                if (returnFocus is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } &&
                    ReferenceEquals(TopLevel.GetTopLevel(returnFocus), TopLevel.GetTopLevel(this)) &&
                    returnFocus.Focus(NavigationMethod.Tab))
                {
                    return;
                }
                Button? trash = report.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
                    button.IsEffectivelyVisible && button.IsEnabled && (clearAll
                        ? ReferenceEquals(button.Command, (DataContext as ReportPresentationViewModel)?.ClearReportHistoryCommand)
                        : ReferenceEquals(button.DataContext, entry) && button.Classes.Contains("danger")));
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
        if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift && IsEffectivelyVisible &&
            DataContext is ReportPresentationViewModel { IsHistoryDeleteConfirmationOpen: true })
        {
            bool focusDelete = CancelButton.IsFocused ||
                (!DeleteButton.IsFocused && e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            _ = (focusDelete ? DeleteButton : CancelButton).Focus(NavigationMethod.Tab, e.KeyModifiers);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && DataContext is ReportPresentationViewModel viewModel)
        {
            viewModel.CancelReportHistoryDeletionCommand.Execute(null);
            e.Handled = true;
        }
    }
}
