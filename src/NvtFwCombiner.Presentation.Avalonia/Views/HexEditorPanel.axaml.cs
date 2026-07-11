using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Standalone raw-BIN utility surface with an incrementally rendered hexadecimal grid.</summary>
public sealed partial class HexEditorPanel : UserControl
{
    private readonly MenuItem _contextDeleteByte;
    private readonly MenuItem _contextEdit;
    private readonly MenuItem _contextInsertAfter;
    private readonly MenuItem _contextInsertBefore;
    private readonly MenuItem _contextSetToFf;
    private readonly MenuItem _contextSetToZero;
    private readonly ContextMenu _hexByteContextMenu;
    private readonly DispatcherTimer _progressiveRenderTimer;
    private HexEditorByteCellViewModel? _contextCell;
    private bool _isAttached;
    private HexEditorWorkspaceViewModel? _workspace;

    /// <summary>Initializes the raw-BIN utility panel and its shared low-cost byte context menu.</summary>
    public HexEditorPanel()
    {
        InitializeComponent();

        _contextEdit = new MenuItem();
        _contextInsertBefore = new MenuItem();
        _contextInsertAfter = new MenuItem();
        _contextDeleteByte = new MenuItem();
        _contextSetToZero = new MenuItem();
        _contextSetToFf = new MenuItem();
        _contextEdit.Click += ContextEdit_OnClick;
        _contextInsertBefore.Click += ContextInsertBefore_OnClick;
        _contextInsertAfter.Click += ContextInsertAfter_OnClick;
        _contextDeleteByte.Click += ContextDeleteByte_OnClick;
        _contextSetToZero.Click += ContextSetToZero_OnClick;
        _contextSetToFf.Click += ContextSetToFf_OnClick;
        _hexByteContextMenu = new ContextMenu();
        _ = _hexByteContextMenu.Items.Add(_contextEdit);
        _ = _hexByteContextMenu.Items.Add(new Separator());
        _ = _hexByteContextMenu.Items.Add(_contextInsertBefore);
        _ = _hexByteContextMenu.Items.Add(_contextInsertAfter);
        _ = _hexByteContextMenu.Items.Add(_contextDeleteByte);
        _ = _hexByteContextMenu.Items.Add(new Separator());
        _ = _hexByteContextMenu.Items.Add(_contextSetToZero);
        _ = _hexByteContextMenu.Items.Add(_contextSetToFf);

        _progressiveRenderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(24),
        };
        _progressiveRenderTimer.Tick += ProgressiveRenderTimer_OnTick;
        DataContextChanged += HexEditorPanel_OnDataContextChanged;
        AttachedToVisualTree += HexEditorPanel_OnAttachedToVisualTree;
        DetachedFromVisualTree += HexEditorPanel_OnDetachedFromVisualTree;
    }

    private async void OpenHexEditorSourceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel ||
            TopLevel.GetTopLevel(this) is not { StorageProvider: { } storageProvider })
        {
            return;
        }

        string? sourcePath = await FirmwareFilePickerDialogs.PickFirmwareBinOpenFileAsync(
            storageProvider,
            "Open BIN in Hex Editor");
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            await viewModel.LoadAsync(sourcePath);
        }
    }

    private async void SaveAsHexEditorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel ||
            !viewModel.CanSave ||
            TopLevel.GetTopLevel(this) is not { StorageProvider: { } storageProvider })
        {
            return;
        }

        string? outputPath = await FirmwareFilePickerDialogs.PickEditedFirmwareOutputPathAsync(
            storageProvider,
            viewModel.SuggestedOutputFileName);
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            await viewModel.SaveAsAsync(outputPath);
        }
    }

    private void HexByte_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is HexEditorWorkspaceViewModel viewModel &&
            sender is Control { DataContext: HexEditorByteCellViewModel cell })
        {
            BeginInlineHexByteEdit(viewModel, cell);
        }
    }

    private void HexByte_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel ||
            sender is not Control { DataContext: HexEditorByteCellViewModel cell } target)
        {
            return;
        }

        viewModel.SelectByteCommand.Execute(cell);
        if (!e.GetCurrentPoint(target).Properties.IsRightButtonPressed || cell.IsReference)
        {
            return;
        }

        _contextCell = cell;
        _contextEdit.Header = viewModel.Text.HexEditorEditByteLabel;
        _contextInsertBefore.Header = viewModel.Text.HexEditorContextInsertZeroBeforeLabel;
        _contextInsertAfter.Header = viewModel.Text.HexEditorContextInsertZeroAfterLabel;
        _contextDeleteByte.Header = viewModel.Text.HexEditorContextDeleteByteLabel;
        _contextSetToZero.Header = viewModel.Text.HexEditorContextSetToZeroLabel;
        _contextSetToFf.Header = viewModel.Text.HexEditorContextSetToFfLabel;
        _hexByteContextMenu.Open(target);
        e.Handled = true;
    }

    private void HexByteEditBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel ||
            sender is not TextBox { DataContext: HexEditorByteCellViewModel cell })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            viewModel.CommitByteEditCommand.Execute(cell);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.CancelByteEditCommand.Execute(cell);
            e.Handled = true;
        }
    }

    private void HexByteEditBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HexEditorWorkspaceViewModel viewModel &&
            sender is TextBox { DataContext: HexEditorByteCellViewModel cell })
        {
            viewModel.CommitByteEditCommand.Execute(cell);
        }
    }

    private void HexByteEditBox_OnTextInput(object? sender, TextInputEventArgs e)
    {
        RejectNonHexTextInput(e, allowWhitespace: false);
    }

    private void HexByteEditBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        KeepAsciiHexOnly(sender as TextBox, allowWhitespace: false);
    }

    private void HexRangeValue_OnTextInput(object? sender, TextInputEventArgs e)
    {
        RejectNonHexTextInput(e, allowWhitespace: true);
    }

    private void HexRangeValue_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        KeepAsciiHexOnly(sender as TextBox, allowWhitespace: true);
    }

    private void ContextEdit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetContextByte(out HexEditorWorkspaceViewModel? viewModel, out HexEditorByteCellViewModel? cell))
        {
            BeginInlineHexByteEdit(viewModel!, cell!);
        }
    }

    private void ContextInsertBefore_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetContextByte(out HexEditorWorkspaceViewModel? viewModel, out HexEditorByteCellViewModel? cell))
        {
            viewModel!.InsertZeroBeforeCommand.Execute(cell);
        }
    }

    private void ContextInsertAfter_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetContextByte(out HexEditorWorkspaceViewModel? viewModel, out HexEditorByteCellViewModel? cell))
        {
            viewModel!.InsertZeroAfterCommand.Execute(cell);
        }
    }

    private void ContextDeleteByte_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetContextByte(out HexEditorWorkspaceViewModel? viewModel, out HexEditorByteCellViewModel? cell))
        {
            viewModel!.DeleteByteCommand.Execute(cell);
        }
    }

    private void ContextSetToZero_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetContextByte(out HexEditorWorkspaceViewModel? viewModel, out HexEditorByteCellViewModel? cell))
        {
            viewModel!.SetByteToZeroCommand.Execute(cell);
        }
    }

    private void ContextSetToFf_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryGetContextByte(out HexEditorWorkspaceViewModel? viewModel, out HexEditorByteCellViewModel? cell))
        {
            viewModel!.SetByteToFfCommand.Execute(cell);
        }
    }

    private void BeginInlineHexByteEdit(HexEditorWorkspaceViewModel viewModel, HexEditorByteCellViewModel cell)
    {
        viewModel.BeginByteEditCommand.Execute(cell);
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
        out HexEditorWorkspaceViewModel? viewModel,
        out HexEditorByteCellViewModel? cell)
    {
        viewModel = DataContext as HexEditorWorkspaceViewModel;
        cell = _contextCell;
        return viewModel is not null && cell is not null;
    }

    private void HexEditorPanel_OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_workspace is { })
        {
            _workspace.PropertyChanged -= HexEditorWorkspace_OnPropertyChanged;
        }

        _workspace = DataContext as HexEditorWorkspaceViewModel;
        if (_workspace is { })
        {
            _workspace.PropertyChanged += HexEditorWorkspace_OnPropertyChanged;
        }

        StartProgressiveRenderingIfNeeded();
    }

    private void HexEditorPanel_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _isAttached = true;
        StartProgressiveRenderingIfNeeded();
    }

    private void HexEditorPanel_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        _progressiveRenderTimer.Stop();
    }

    private void HexEditorWorkspace_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HexEditorWorkspaceViewModel.HasMoreRows) or
            nameof(HexEditorWorkspaceViewModel.IsPageActive))
        {
            if (_workspace?.IsPageActive != true)
            {
                _progressiveRenderTimer.Stop();
                return;
            }

            StartProgressiveRenderingIfNeeded();
        }
    }

    private void ProgressiveRenderTimer_OnTick(object? sender, EventArgs e)
    {
        if (!_isAttached || _workspace is null || !_workspace.IsPageActive || !_workspace.HasMoreRows)
        {
            _progressiveRenderTimer.Stop();
            return;
        }

        _workspace.LoadNextPageCommand.Execute(null);
    }

    private void StartProgressiveRenderingIfNeeded()
    {
        if (_isAttached && _workspace is { IsPageActive: true, HasMoreRows: true } && !_progressiveRenderTimer.IsEnabled)
        {
            _progressiveRenderTimer.Start();
        }
    }

    private static void RejectNonHexTextInput(TextInputEventArgs e, bool allowWhitespace)
    {
        if (string.IsNullOrEmpty(e.Text) || e.Text.All(character =>
                character is (>= '0' and <= '9') or
                    (>= 'A' and <= 'F') or
                    (>= 'a' and <= 'f') ||
                (allowWhitespace && char.IsWhiteSpace(character))))
        {
            return;
        }

        e.Handled = true;
    }

    private static void KeepAsciiHexOnly(TextBox? textBox, bool allowWhitespace)
    {
        if (textBox?.Text is not { } text)
        {
            return;
        }

        string filtered = new([
            .. text.Where(character =>
            character is (>= '0' and <= '9') or
                (>= 'A' and <= 'F') or
                (>= 'a' and <= 'f') ||
            (allowWhitespace && char.IsWhiteSpace(character))),
        ]);
        if (string.Equals(text, filtered, StringComparison.Ordinal))
        {
            return;
        }

        int selectionStart = Math.Min(textBox.SelectionStart, filtered.Length);
        textBox.Text = filtered;
        textBox.SelectionStart = selectionStart;
    }
}
