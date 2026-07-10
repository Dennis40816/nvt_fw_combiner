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
    /// <summary>Initializes the hexadecimal editor panel.</summary>
    public HexEditorPanel()
    {
        InitializeComponent();
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
        if (!TryGetContextByte(sender, out MainWindowViewModel? viewModel, out GeneralReplaceHexByteCellViewModel? cell) ||
            viewModel is null ||
            cell is null)
        {
            return;
        }

        BeginInlineHexByteEdit(viewModel, cell);
    }

    private void HexByteContextRangeStartMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextByte(sender, out MainWindowViewModel? viewModel, out GeneralReplaceHexByteCellViewModel? cell) ||
            viewModel is null ||
            cell is null)
        {
            return;
        }

        viewModel.SetGeneralReplacePatchStartCommand.Execute(cell);
    }

    private void HexByteContextRangeEndMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextByte(sender, out MainWindowViewModel? viewModel, out GeneralReplaceHexByteCellViewModel? cell) ||
            viewModel is null ||
            cell is null)
        {
            return;
        }

        viewModel.SetGeneralReplacePatchEndCommand.Execute(cell);
    }

    private void HexByteContextClearMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryGetContextByte(sender, out MainWindowViewModel? viewModel, out GeneralReplaceHexByteCellViewModel? cell) ||
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
        object? sender,
        out MainWindowViewModel? viewModel,
        out GeneralReplaceHexByteCellViewModel? cell)
    {
        viewModel = DataContext as MainWindowViewModel;
        cell = (sender as Control)?.DataContext as GeneralReplaceHexByteCellViewModel;
        return viewModel is not null && cell is not null;
    }
}
