using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Editable General Merge mapping row with source BIN browse and drop selection.</summary>
public sealed partial class GeneralMergeMappingRow : UserControl
{
    /// <summary>Defines the localized browse button label.</summary>
    public static readonly StyledProperty<string> BrowseLabelProperty =
        AvaloniaProperty.Register<GeneralMergeMappingRow, string>(nameof(BrowseLabel), "Browse");

    /// <summary>Defines the localized source BIN selection tooltip.</summary>
    public static readonly StyledProperty<string> SelectSourceBinTooltipProperty =
        AvaloniaProperty.Register<GeneralMergeMappingRow, string>(nameof(SelectSourceBinTooltip), string.Empty);

    /// <summary>Defines the localized row removal tooltip.</summary>
    public static readonly StyledProperty<string> RemoveMappingTooltipProperty =
        AvaloniaProperty.Register<GeneralMergeMappingRow, string>(nameof(RemoveMappingTooltip), string.Empty);

    /// <summary>Gets or sets the localized browse button label.</summary>
    public string BrowseLabel
    {
        get => GetValue(BrowseLabelProperty);
        set => SetValue(BrowseLabelProperty, value);
    }

    /// <summary>Gets or sets the localized source BIN selection tooltip.</summary>
    public string SelectSourceBinTooltip
    {
        get => GetValue(SelectSourceBinTooltipProperty);
        set => SetValue(SelectSourceBinTooltipProperty, value);
    }

    /// <summary>Gets or sets the localized row removal tooltip.</summary>
    public string RemoveMappingTooltip
    {
        get => GetValue(RemoveMappingTooltipProperty);
        set => SetValue(RemoveMappingTooltipProperty, value);
    }

    /// <summary>Initializes the General Merge mapping row.</summary>
    public GeneralMergeMappingRow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ShellViewModel =>
        WorkbenchShellViewModelLocator.Find(this);

    private void DropZone_OnDragEnter(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, DropZoneDragState.ApplyFileDropEffect(e));
    }

    private void DropZone_OnDragOver(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, DropZoneDragState.ApplyFileDropEffect(e));
    }

    private void DropZone_OnDragLeave(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);
    }

    private void MappingDrop_OnDrop(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);

        if (DataContext is not GeneralMergeMappingViewModel mapping ||
            ShellViewModel is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = DropZoneDragState.GetFirstLocalFilePath(e);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = viewModel.SetGeneralMergeMappingFile(mapping.MappingId, path);
        }
    }

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GeneralMergeMappingViewModel mapping ||
            ShellViewModel is not MainWindowViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        string title = string.IsNullOrWhiteSpace(SelectSourceBinTooltip)
            ? "Select source BIN"
            : SelectSourceBinTooltip;
        string? path = await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
            topLevel.StorageProvider,
            title);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = viewModel.SetGeneralMergeMappingFile(mapping.MappingId, path);
        }
    }

    private void RemoveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GeneralMergeMappingViewModel mapping &&
            ShellViewModel is MainWindowViewModel viewModel)
        {
            viewModel.RemoveGeneralMergeMappingRow(mapping);
        }
    }
}
