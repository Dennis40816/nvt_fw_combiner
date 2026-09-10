using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.ComponentModel;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>The shared bounded report list, backed by the existing history owner.</summary>
public sealed partial class RunReportsTable : UserControl
{
    private ReportPresentationViewModel? _reports;
    private Control? _returnControl;
    private MessageCenterModal? _focusHost;

    /// <summary>Initializes the history table.</summary>
    public RunReportsTable()
    {
        InitializeComponent();
    }

    private void Table_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button button && _reports?.IsReportModalOpen == false &&
            (button.Classes.Contains("reportListRow") || button.Name == "LoadRunReportButton"))
        {
            _returnControl = button;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double viewport = (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 900) - 340;
        ReportListScroll.MaxHeight = Math.Clamp(viewport, 100, 480);
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _reports = DataContext as ReportPresentationViewModel;
        _reports?.PropertyChanged += Reports_OnPropertyChanged;
        _focusHost = this.GetVisualAncestors().OfType<MessageCenterModal>().FirstOrDefault();
        _focusHost?.GotFocus += Table_OnGotFocus;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _reports?.PropertyChanged -= Reports_OnPropertyChanged;
        _focusHost?.GotFocus -= Table_OnGotFocus;
        _focusHost = null;
        _reports = null;
        _returnControl = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void Reports_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ReportPresentationViewModel.IsReportModalOpen) || !IsEffectivelyVisible)
        {
            return;
        }
        TopLevel? window = TopLevel.GetTopLevel(this);
        if (_reports?.IsReportModalOpen == true)
        {
            // The report host may already have taken focus during an earlier
            // property subscriber. Keep the invoking row captured before opening.
            Dispatcher.UIThread.Post(() =>
            {
                if (_reports?.IsReportModalOpen == true)
                {
                    _ = window?.GetVisualDescendants().OfType<ReportModal>().FirstOrDefault()?.Focus(NavigationMethod.Tab);
                }
            }, DispatcherPriority.Input);
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (IsEffectivelyVisible && _reports?.IsReportModalOpen == false)
                {
                    Control? target = _returnControl is { IsEffectivelyVisible: true, IsEnabled: true } ? _returnControl :
                        this.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Classes.Contains("reportListRow"));
                    target ??= window?.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "LoadRunReportButton");
                    _ = target?.Focus(NavigationMethod.Tab);
                }
            }, DispatcherPriority.Input);
        }
    }
}
