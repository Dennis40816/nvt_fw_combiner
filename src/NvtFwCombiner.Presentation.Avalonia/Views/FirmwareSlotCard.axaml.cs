using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Reusable firmware input slot card with browse and drag/drop file selection.</summary>
public sealed partial class FirmwareSlotCard : UserControl
{
    /// <summary>Formats the shared visible, assistive, and picker-title Browse phrase.</summary>
    public const string BrowseActionFormat = "{0} — {1}";
    private static readonly CompositeFormat BrowseActionCompositeFormat = CompositeFormat.Parse(BrowseActionFormat);

    internal Func<global::Avalonia.Platform.Storage.IStorageProvider, string, Task<string?>> PickFirmwareFileAsync { get; init; } =
        FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync;

    /// <summary>Defines the localized browse button label.</summary>
    public static readonly StyledProperty<string> BrowseLabelProperty = AvaloniaProperty.Register<FirmwareSlotCard, string>(nameof(BrowseLabel), string.Empty);

    /// <summary>Gets or sets the localized browse button label.</summary>
    public string BrowseLabel
    {
        get => GetValue(BrowseLabelProperty);
        set => SetValue(BrowseLabelProperty, value);
    }

    /// <summary>Initializes the firmware slot card.</summary>
    public FirmwareSlotCard()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ShellViewModel =>
        ShellViewModelLocator.Find(this);

    internal static string FormatBrowseActionLabel(string browseLabel, string slotTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(browseLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotTitle);
        return string.Format(
            CultureInfo.CurrentCulture,
            BrowseActionCompositeFormat,
            browseLabel,
            slotTitle);
    }

    private void SlotDragOver_OnDragOver(object? sender, DragEventArgs e)
    {
        if (sender is Control { DataContext: FirmwareSlotViewModel { CanSelectFile: false } })
        {
            DropZoneDragState.SetActive(sender, isActive: false);
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        DropZoneDragState.SetActive(sender, DropZoneDragState.ApplyFileDropEffect(e));
    }

    private void DropZone_OnDragLeave(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);
    }

    private async void SlotDrop_OnDrop(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);

        if (sender is not Control
            {
                Tag: string slotId,
                DataContext: FirmwareSlotViewModel { CanSelectFile: true },
            } ||
            ShellViewModel is not MainWindowViewModel viewModel)
        {
            return;
        }

        SingleLocalFileDropSelection selection = DropZoneDragState.GetSingleLocalFile(e);
        e.Handled = true;
        if (!selection.IsAccepted)
        {
            viewModel.Reports.SetShellToast(
                viewModel.Text.FileDropRejectedTitle,
                viewModel.Text.FileDropRejectedDetail);
            return;
        }

        await viewModel.WorkflowSession.SetSlotFileAsync(slotId, selection.Path!);
    }

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control
            {
                Tag: string slotId,
                DataContext: FirmwareSlotViewModel { CanSelectFile: true } slot,
            } ||
            ShellViewModel is not MainWindowViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        string? path = await PickFirmwareFileAsync(
            topLevel.StorageProvider,
            FormatBrowseActionLabel(BrowseLabel, slot.Title));
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.WorkflowSession.SetSlotFileAsync(slotId, path);
        }
    }
}
