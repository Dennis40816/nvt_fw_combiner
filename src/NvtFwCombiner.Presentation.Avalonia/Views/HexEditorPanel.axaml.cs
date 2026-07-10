using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Experimental profile-bound hexadecimal editor hosted below Replace workflows.</summary>
public sealed partial class HexEditorPanel : UserControl
{
    private readonly ContextMenu _hexByteContextMenu;
    private readonly MenuItem _hexByteContextClearMenuItem;
    private readonly MenuItem _hexByteContextEditMenuItem;
    private readonly MenuItem _hexByteContextRangeEndMenuItem;
    private readonly MenuItem _hexByteContextRangeStartMenuItem;
    private GeneralReplaceHexByteCellViewModel? _hexByteContextCell;

    /// <summary>Initializes the hexadecimal editor panel.</summary>
    public HexEditorPanel()
    {
        InitializeComponent();

        _hexByteContextEditMenuItem = new MenuItem();
        _hexByteContextRangeStartMenuItem = new MenuItem();
        _hexByteContextRangeEndMenuItem = new MenuItem();
        _hexByteContextClearMenuItem = new MenuItem();
        _hexByteContextEditMenuItem.Click += HexByteContextEditMenuItem_OnClick;
        _hexByteContextRangeStartMenuItem.Click += HexByteContextRangeStartMenuItem_OnClick;
        _hexByteContextRangeEndMenuItem.Click += HexByteContextRangeEndMenuItem_OnClick;
        _hexByteContextClearMenuItem.Click += HexByteContextClearMenuItem_OnClick;
        _hexByteContextMenu = new ContextMenu();
        _ = _hexByteContextMenu.Items.Add(_hexByteContextEditMenuItem);
        _ = _hexByteContextMenu.Items.Add(_hexByteContextRangeStartMenuItem);
        _ = _hexByteContextMenu.Items.Add(_hexByteContextRangeEndMenuItem);
        _ = _hexByteContextMenu.Items.Add(new Separator());
        _ = _hexByteContextMenu.Items.Add(_hexByteContextClearMenuItem);
    }

    private async void SaveAsHexEditorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.CanBuildHexEditor ||
            TopLevel.GetTopLevel(this) is not { StorageProvider: { } storageProvider })
        {
            return;
        }

        string? outputPath = await FirmwareFilePickerDialogs.PickEditedFirmwareOutputPathAsync(
            storageProvider,
            viewModel.ReplaceOutputFileName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        await viewModel.BuildHexEditorAsync(outputPath);
    }

    private void HexByte_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is Control { DataContext: GeneralReplaceHexByteCellViewModel cell })
        {
            BeginInlineHexByteEdit(viewModel, cell);
        }
    }

    private void HexByte_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not Control { DataContext: GeneralReplaceHexByteCellViewModel cell } target)
        {
            return;
        }

        viewModel.SelectGeneralReplaceHexByteCommand.Execute(cell);
        if (!e.GetCurrentPoint(target).Properties.IsRightButtonPressed)
        {
            return;
        }

        _hexByteContextCell = cell;
        _hexByteContextEditMenuItem.Header = cell.EditMenuLabel;
        _hexByteContextRangeStartMenuItem.Header = cell.RangeStartMenuLabel;
        _hexByteContextRangeEndMenuItem.Header = cell.RangeEndMenuLabel;
        _hexByteContextClearMenuItem.Header = cell.ClearMenuLabel;
        _hexByteContextMenu.Open(target);
        e.Handled = true;
    }

    private void HexByteEditBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not TextBox { DataContext: GeneralReplaceHexByteCellViewModel cell })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            viewModel.CommitGeneralReplaceHexByteEditCommand.Execute(cell);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.CancelGeneralReplaceHexByteEditCommand.Execute(cell);
            e.Handled = true;
        }
    }

    private void HexByteEditBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            sender is TextBox { DataContext: GeneralReplaceHexByteCellViewModel cell })
        {
            viewModel.CommitGeneralReplaceHexByteEditCommand.Execute(cell);
        }
    }

    private void HexByteContextEditMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextByte(out MainWindowViewModel? viewModel, out GeneralReplaceHexByteCellViewModel? cell) ||
            viewModel is null ||
            cell is null)
        {
            return;
        }

        BeginInlineHexByteEdit(viewModel, cell);
    }

    private void HexByteContextRangeStartMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextByte(out MainWindowViewModel? viewModel, out GeneralReplaceHexByteCellViewModel? cell) ||
            viewModel is null ||
            cell is null)
        {
            return;
        }

        viewModel.SetGeneralReplacePatchStartCommand.Execute(cell);
    }

    private void HexByteContextRangeEndMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextByte(out MainWindowViewModel? viewModel, out GeneralReplaceHexByteCellViewModel? cell) ||
            viewModel is null ||
            cell is null)
        {
            return;
        }

        viewModel.SetGeneralReplacePatchEndCommand.Execute(cell);
    }

    private void HexByteContextClearMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextByte(out MainWindowViewModel? viewModel, out GeneralReplaceHexByteCellViewModel? cell) ||
            viewModel is null ||
            cell is null)
        {
            return;
        }

        viewModel.ClearGeneralReplaceHexByteCommand.Execute(cell);
    }

    private void BeginInlineHexByteEdit(
        MainWindowViewModel viewModel,
        GeneralReplaceHexByteCellViewModel cell)
    {
        viewModel.BeginGeneralReplaceHexByteEditCommand.Execute(cell);
        Dispatcher.UIThread.Post(() =>
        {
            TextBox? editor = this.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(candidate =>
                    candidate.IsVisible &&
                    ReferenceEquals(candidate.DataContext, cell));
            _ = editor?.Focus();
            editor?.SelectAll();
        }, DispatcherPriority.Input);
    }

    private bool TryGetContextByte(
        out MainWindowViewModel? viewModel,
        out GeneralReplaceHexByteCellViewModel? cell)
    {
        viewModel = DataContext as MainWindowViewModel;
        cell = _hexByteContextCell;
        return viewModel is not null && cell is not null;
    }
}
