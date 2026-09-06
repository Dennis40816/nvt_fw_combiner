using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Hosts separate run-report and refreshable System Information views.</summary>
public sealed partial class MessageCenterModal : UserControl
{
    /// <summary>Routes the import request to the window's sole bounded file loader.</summary>
    public event EventHandler<RoutedEventArgs>? LoadReportRequested;

    private void LoadReportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LoadReportRequested?.Invoke(sender, e);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        bool compact = availableSize.Width < 1200;
        MessageCenterSurface.Margin = compact ? new Thickness(14, 10, 14, 12) : new Thickness(32, 24);
        MessageCenterBody.ColumnDefinitions[0].Width = new GridLength(compact ? 200 : 324);
        MessageCenterContent.Margin = compact ? new Thickness(24, 24, 24, 20) : new Thickness(44, 30, 50, 24);
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Initializes the generated view.</summary>
    public MessageCenterModal()
    {
        InitializeComponent();
        AttachedToVisualTree += MessageCenterModal_OnAttachedToVisualTree;
        PropertyChanged += MessageCenterModal_OnPropertyChanged;
    }

    private void MessageCenterModal_OnAttachedToVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        FocusInitialControl();
    }

    private void MessageCenterModal_OnPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && IsVisible && VisualRoot is not null)
        {
            FocusInitialControl();
        }
    }

    private void FocusInitialControl()
    {
        Dispatcher.UIThread.Post(
            () => _ = CloseButton.Focus(),
            DispatcherPriority.Input);
    }

    private void MessageCenterModal_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MessageCenterViewModel viewModel)
        {
            return;
        }

        viewModel.CloseCommand.Execute(null);
        e.Handled = true;
    }

    private async void ExportDiagnosticsButton_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not MessageCenterViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        IStorageFile? file = await FirmwareFilePickerDialogs.PickDiagnosticsSaveFileAsync(
            storageProvider,
            "nvt-fw-combiner-diagnostics.json");
        string? path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.ExportAsync(path, CancellationToken.None);
        }
    }
}
