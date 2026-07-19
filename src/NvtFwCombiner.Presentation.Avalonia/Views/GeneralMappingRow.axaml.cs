using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Shared General mapping row with local BIN browse and drop selection.</summary>
public sealed partial class GeneralMappingRow : UserControl
{
    /// <summary>Defines the localized browse button label.</summary>
    public static readonly StyledProperty<string> BrowseLabelProperty =
        AvaloniaProperty.Register<GeneralMappingRow, string>(nameof(BrowseLabel), "Browse");

    /// <summary>Defines the localized BIN selection tooltip.</summary>
    public static readonly StyledProperty<string> SelectBinTooltipProperty =
        AvaloniaProperty.Register<GeneralMappingRow, string>(nameof(SelectBinTooltip), string.Empty);

    /// <summary>Defines the localized row removal tooltip.</summary>
    public static readonly StyledProperty<string> RemoveMappingTooltipProperty =
        AvaloniaProperty.Register<GeneralMappingRow, string>(nameof(RemoveMappingTooltip), string.Empty);

    /// <summary>Gets or sets the localized browse button label.</summary>
    public string BrowseLabel
    {
        get => GetValue(BrowseLabelProperty);
        set => SetValue(BrowseLabelProperty, value);
    }

    /// <summary>Gets or sets the localized BIN selection tooltip.</summary>
    public string SelectBinTooltip
    {
        get => GetValue(SelectBinTooltipProperty);
        set => SetValue(SelectBinTooltipProperty, value);
    }

    /// <summary>Gets or sets the localized row removal tooltip.</summary>
    public string RemoveMappingTooltip
    {
        get => GetValue(RemoveMappingTooltipProperty);
        set => SetValue(RemoveMappingTooltipProperty, value);
    }

    /// <summary>Initializes the shared General mapping row.</summary>
    public GeneralMappingRow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ShellViewModel =>
        WorkbenchShellViewModelLocator.Find(this);

    private void DropZone_OnDragOver(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, DropZoneDragState.ApplyFileDropEffect(e));
    }

    private void DropZone_OnDragLeave(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);
    }

    private async void MappingDrop_OnDrop(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);

        if (DataContext is not GeneralMappingRowViewModel mapping ||
            ShellViewModel is not MainWindowViewModel viewModel)
        {
            return;
        }

        string? path = DropZoneDragState.GetFirstLocalFilePath(e);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.SetSlotFileAsync(mapping.MappingId, path);
        }
    }

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GeneralMappingRowViewModel mapping ||
            ShellViewModel is not MainWindowViewModel viewModel ||
            TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        string title = string.IsNullOrWhiteSpace(SelectBinTooltip)
            ? mapping is GeneralMergeMappingViewModel ? "Select source BIN" : "Select replacement BIN"
            : SelectBinTooltip;
        string? path = await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
            topLevel.StorageProvider,
            title);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.SetSlotFileAsync(mapping.MappingId, path);
        }
    }

    private void RemoveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GeneralMappingRowViewModel mapping &&
            ShellViewModel is MainWindowViewModel viewModel)
        {
            viewModel.RemoveGeneralMappingRow(mapping);
        }
    }
}
