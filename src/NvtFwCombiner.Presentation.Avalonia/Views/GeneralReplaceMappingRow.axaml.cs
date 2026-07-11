using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Editable General Replace mapping row with replacement BIN browse and drop selection.</summary>
public sealed partial class GeneralReplaceMappingRow : UserControl
{
    /// <summary>Defines the localized browse button label.</summary>
    public static readonly StyledProperty<string> BrowseLabelProperty =
        AvaloniaProperty.Register<GeneralReplaceMappingRow, string>(nameof(BrowseLabel), "Browse");

    /// <summary>Defines the localized replacement BIN selection tooltip.</summary>
    public static readonly StyledProperty<string> SelectReplacementBinTooltipProperty =
        AvaloniaProperty.Register<GeneralReplaceMappingRow, string>(nameof(SelectReplacementBinTooltip), string.Empty);

    /// <summary>Defines the localized row removal tooltip.</summary>
    public static readonly StyledProperty<string> RemoveRangeTooltipProperty =
        AvaloniaProperty.Register<GeneralReplaceMappingRow, string>(nameof(RemoveRangeTooltip), string.Empty);

    /// <summary>Gets or sets the localized browse button label.</summary>
    public string BrowseLabel
    {
        get => GetValue(BrowseLabelProperty);
        set => SetValue(BrowseLabelProperty, value);
    }

    /// <summary>Gets or sets the localized replacement BIN selection tooltip.</summary>
    public string SelectReplacementBinTooltip
    {
        get => GetValue(SelectReplacementBinTooltipProperty);
        set => SetValue(SelectReplacementBinTooltipProperty, value);
    }

    /// <summary>Gets or sets the localized row removal tooltip.</summary>
    public string RemoveRangeTooltip
    {
        get => GetValue(RemoveRangeTooltipProperty);
        set => SetValue(RemoveRangeTooltipProperty, value);
    }

    /// <summary>Initializes the General Replace mapping row.</summary>
    public GeneralReplaceMappingRow()
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

        if (DataContext is not GeneralReplaceMappingViewModel mapping ||
            ShellViewModel is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = DropZoneDragState.GetFirstLocalFilePath(e);
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetGeneralReplaceMappingFile(mapping.MappingId, path);
        }
    }

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GeneralReplaceMappingViewModel mapping ||
            ShellViewModel is not MainWindowViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        string title = string.IsNullOrWhiteSpace(SelectReplacementBinTooltip)
            ? "Select replacement BIN"
            : SelectReplacementBinTooltip;
        string? path = await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
            topLevel.StorageProvider,
            title);
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetGeneralReplaceMappingFile(mapping.MappingId, path);
        }
    }

    private void RemoveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GeneralReplaceMappingViewModel mapping &&
            ShellViewModel is MainWindowViewModel viewModel)
        {
            viewModel.RemoveGeneralReplaceMappingRow(mapping);
        }
    }
}
