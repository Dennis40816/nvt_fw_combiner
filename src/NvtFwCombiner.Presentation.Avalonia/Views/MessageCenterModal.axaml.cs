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
        MessageCenterNavigationRail.Padding = compact ? new Thickness(8, 30, 8, 30) : new Thickness(18, 30, 36, 30);
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

    private void ExportDiagnosticsButton_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        UiEventAdapter.Run(this, "MessageCenterModal.ExportDiagnostics", _ => HandleExportDiagnosticsButton_OnClickAsync(sender, e));
    }

    private async Task HandleExportDiagnosticsButton_OnClickAsync(
        object? sender,
        RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        await ExportWithPickerAsync(async () =>
        {
            using IStorageFile? file = await FirmwareFilePickerDialogs.PickDiagnosticsSaveFileAsync(
                storageProvider,
                "nvt-fw-combiner-diagnostics.json");
            return file is null ? null : file.TryGetLocalPath() ??
                throw new IOException("Diagnostics export requires a local destination.");
        });
    }

    internal async Task ExportWithPickerAsync(Func<Task<string?>> pickPathAsync)
    {
        ArgumentNullException.ThrowIfNull(pickPathAsync);
        if (DataContext is not MessageCenterViewModel viewModel || !viewModel.IsOpen)
        {
            return;
        }

        long contextGeneration = viewModel.ExportContextGeneration;
        string? path;
        try
        {
            path = await pickPathAsync();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (ReferenceEquals(DataContext, viewModel) && viewModel.IsExportContextCurrent(contextGeneration))
            {
                viewModel.ReportExportFailure();
            }
            return;
        }
        if (!string.IsNullOrWhiteSpace(path) &&
            ReferenceEquals(DataContext, viewModel) &&
            viewModel.IsExportContextCurrent(contextGeneration))
        {
            await viewModel.ExportAsync(path, CancellationToken.None);
        }
    }
}
