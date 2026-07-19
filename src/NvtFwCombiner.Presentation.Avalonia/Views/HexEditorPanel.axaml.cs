using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Standalone raw-BIN utility surface with a bounded hexadecimal document viewport.</summary>
public sealed partial class HexEditorPanel : UserControl
{
    private readonly MenuItem _contextDeleteByte;
    private readonly MenuItem _contextEdit;
    private readonly MenuItem _contextInsertAfter;
    private readonly MenuItem _contextInsertBefore;
    private readonly MenuItem _contextInsertManyAfter;
    private readonly MenuItem _contextInsertManyBefore;
    private readonly MenuItem _contextSetToFf;
    private readonly MenuItem _contextSetToZero;
    private readonly ContextMenu _hexByteContextMenu;
    private readonly MenuItem _structuralGoToEnd;
    private readonly MenuItem _structuralGoToStart;
    private readonly ContextMenu _structuralBlockContextMenu;
    private HexEditorByteCellViewModel? _contextCell;
    private HexEditorByteCellViewModel? _inlineEditCell;
    private bool _isCompletingInlineEdit;
    private bool _isDocumentScrollQueued;
    private TopLevel? _layoutTopLevel;
    private int _pendingDocumentScrollRow;

    /// <summary>Initializes the raw-BIN utility panel and its shared low-cost byte context menu.</summary>
    public HexEditorPanel()
    {
        InitializeComponent();

        _contextEdit = new MenuItem();
        _contextInsertBefore = new MenuItem();
        _contextInsertAfter = new MenuItem();
        _contextInsertManyBefore = new MenuItem();
        _contextInsertManyAfter = new MenuItem();
        _contextDeleteByte = new MenuItem();
        _contextSetToZero = new MenuItem();
        _contextSetToFf = new MenuItem();
        _contextEdit.Click += ContextEdit_OnClick;
        _hexByteContextMenu = new ContextMenu();
        _ = _hexByteContextMenu.Items.Add(_contextEdit);
        _ = _hexByteContextMenu.Items.Add(new Separator());
        _ = _hexByteContextMenu.Items.Add(_contextInsertBefore);
        _ = _hexByteContextMenu.Items.Add(_contextInsertAfter);
        _ = _hexByteContextMenu.Items.Add(_contextInsertManyBefore);
        _ = _hexByteContextMenu.Items.Add(_contextInsertManyAfter);
        _ = _hexByteContextMenu.Items.Add(_contextDeleteByte);
        _ = _hexByteContextMenu.Items.Add(new Separator());
        _ = _hexByteContextMenu.Items.Add(_contextSetToZero);
        _ = _hexByteContextMenu.Items.Add(_contextSetToFf);
        _structuralGoToStart = new MenuItem();
        _structuralGoToEnd = new MenuItem();
        _structuralBlockContextMenu = new ContextMenu();
        _ = _structuralBlockContextMenu.Items.Add(_structuralGoToStart);
        _ = _structuralBlockContextMenu.Items.Add(_structuralGoToEnd);
        HexViewport.EditRequested += HexViewport_OnEditRequested;
        HexViewport.ContextMenuRequested += HexViewport_OnContextRequested;
        HexViewport.StructuralBlockContextMenuRequested += HexViewport_OnStructuralBlockContextRequested;
        HexViewport.ScrollRequested += HexViewport_OnScrollRequested;
        AddHandler(KeyDownEvent, HexEditorPanel_OnKeyDown, RoutingStrategies.Tunnel);
        AttachedToVisualTree += HexEditorPanel_OnAttachedToVisualTree;
        DetachedFromVisualTree += HexEditorPanel_OnDetachedFromVisualTree;
        SizeChanged += HexEditorPanel_OnSizeChanged;
    }

    private void HexEditorPanel_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _layoutTopLevel = TopLevel.GetTopLevel(this);
        if (_layoutTopLevel is { } layoutTopLevel)
        {
            layoutTopLevel.SizeChanged += LayoutTopLevel_OnSizeChanged;
        }

        QueueViewportLayout();
    }

    private void HexEditorPanel_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_layoutTopLevel is { } layoutTopLevel)
        {
            layoutTopLevel.SizeChanged -= LayoutTopLevel_OnSizeChanged;
            _layoutTopLevel = null;
        }
    }

    private void HexEditorPanel_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        QueueViewportLayout();
    }

    private void LayoutTopLevel_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        QueueViewportLayout();
    }

    private void QueueViewportLayout()
    {
        Dispatcher.UIThread.Post(UpdateViewportLayout, DispatcherPriority.Render);
    }

    private void UpdateViewportLayout()
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel ||
            _layoutTopLevel is null ||
            HexDocumentSurface.TranslatePoint(default, _layoutTopLevel) is not Point origin)
        {
            return;
        }

        const double bottomMargin = 18;
        viewModel.SetViewportHeight(_layoutTopLevel.ClientSize.Height - origin.Y - bottomMargin);
    }

    private void HexEditorPanel_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control ||
            DataContext is not HexEditorWorkspaceViewModel { HasDocument: true } viewModel)
        {
            return;
        }

        TextBox? target = null;
        if (e.Key == Key.F)
        {
            target = AsciiSearchTextBox;
        }
        else if (e.Key == Key.G)
        {
            target = GoToAddressTextBox;
        }

        if (target is null)
        {
            return;
        }

        if (_inlineEditCell is not null)
        {
            CompleteInlineEdit(viewModel, commit: true);
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                _ = target.Focus();
                target.SelectAll();
            },
            DispatcherPriority.Input);
        e.Handled = true;
    }

    private void GoToAddressTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is HexEditorWorkspaceViewModel viewModel)
        {
            viewModel.GoToCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void AsciiSearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is HexEditorWorkspaceViewModel viewModel)
        {
            viewModel.FindAsciiCommand.Execute(null);
            e.Handled = true;
        }
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

    private void HexEditorSourceDrop_OnDragOver(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, DropZoneDragState.ApplyFileDropEffect(e));
    }

    private void HexEditorSourceDrop_OnDragLeave(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);
    }

    private async void HexEditorSourceDrop_OnDrop(object? sender, DragEventArgs e)
    {
        DropZoneDragState.SetActive(sender, isActive: false);
        if (DataContext is not HexEditorWorkspaceViewModel viewModel)
        {
            return;
        }

        string? path = DropZoneDragState.GetFirstLocalFilePath(e);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.LoadAsync(path);
        }
    }

    private void HexViewport_OnEditRequested(object? sender, HexEditorViewportCellEventArgs e)
    {
        if (DataContext is HexEditorWorkspaceViewModel viewModel)
        {
            BeginInlineHexByteEdit(viewModel, e.Cell, e.Bounds);
        }
    }

    private void HexViewport_OnContextRequested(object? sender, HexEditorViewportCellEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel)
        {
            return;
        }

        _contextCell = e.Cell;
        _contextEdit.Header = viewModel.Text.HexEditorEditByteLabel;
        BindContextCommand(_contextInsertBefore, viewModel.Text.HexEditorContextInsertZeroBeforeLabel, viewModel.InsertZeroBeforeCommand, e.Cell);
        BindContextCommand(_contextInsertAfter, viewModel.Text.HexEditorContextInsertZeroAfterLabel, viewModel.InsertZeroAfterCommand, e.Cell);
        BindContextCommand(_contextInsertManyBefore, viewModel.Text.HexEditorContextInsertBytesBeforeLabel, viewModel.RequestInsertBytesBeforeCommand, e.Cell);
        BindContextCommand(_contextInsertManyAfter, viewModel.Text.HexEditorContextInsertBytesAfterLabel, viewModel.RequestInsertBytesAfterCommand, e.Cell);
        BindContextCommand(_contextDeleteByte, viewModel.Text.HexEditorContextDeleteByteLabel, viewModel.DeleteByteCommand, e.Cell);
        BindContextCommand(_contextSetToZero, viewModel.Text.HexEditorContextSetToZeroLabel, viewModel.SetByteToZeroCommand, e.Cell);
        BindContextCommand(_contextSetToFf, viewModel.Text.HexEditorContextSetToFfLabel, viewModel.SetByteToFfCommand, e.Cell);
        _hexByteContextMenu.Open(HexViewport);
    }

    private void HexViewport_OnStructuralBlockContextRequested(object? sender, HexEditorViewportCellEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel ||
            e.Cell.StructuralBlockIndex < 0 ||
            e.Cell.StructuralBlockIndex >= viewModel.ChangedBlockCount)
        {
            return;
        }

        HexEditorChangedBlockViewModel? block = viewModel.GetChangedBlock(e.Cell.StructuralBlockIndex);
        if (block is null)
        {
            return;
        }

        BindContextCommand(_structuralGoToStart, viewModel.Text.HexEditorContextGoToBlockStartLabel, viewModel.GoToChangedBlockStartCommand, block);
        BindContextCommand(_structuralGoToEnd, viewModel.Text.HexEditorContextGoToBlockEndLabel, viewModel.GoToChangedBlockEndCommand, block);
        _structuralBlockContextMenu.Open(HexViewport);
    }

    private void HexByteEditBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel ||
            sender is not TextBox ||
            _inlineEditCell is null)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CompleteInlineEdit(viewModel, commit: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CompleteInlineEdit(viewModel, commit: false);
            e.Handled = true;
        }
    }

    private void HexByteEditBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_isCompletingInlineEdit &&
            DataContext is HexEditorWorkspaceViewModel viewModel &&
            _inlineEditCell is not null)
        {
            CompleteInlineEdit(viewModel, commit: true);
        }
    }

    private void HexTextInput_OnGotFocus(object? sender, FocusChangedEventArgs _)
    {
        if (DataContext is HexEditorWorkspaceViewModel viewModel)
        {
            viewModel.SetTextEntryFocused(true);
        }
    }

    private void HexTextInput_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HexEditorWorkspaceViewModel viewModel)
        {
            viewModel.SetTextEntryFocused(false);
        }
    }

    private void HexByteEditBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_inlineEditCell is not null && sender is TextBox textBox)
        {
            _inlineEditCell.EditValue = textBox.Text ?? string.Empty;
        }
    }

    private void AsciiSearch_OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || e.Text.All(character => character is >= (char)0x20 and <= (char)0x7E))
        {
            return;
        }

        e.Handled = true;
    }

    private static void AsciiSearch_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { Text: { } text } textBox)
        {
            return;
        }

        string filtered = new([.. text.Where(character => character is >= (char)0x20 and <= (char)0x7E)]);
        if (string.Equals(text, filtered, StringComparison.Ordinal))
        {
            return;
        }

        int selectionStart = Math.Min(textBox.SelectionStart, filtered.Length);
        textBox.Text = filtered;
        textBox.SelectionStart = selectionStart;
    }

    private void ContextEdit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HexEditorWorkspaceViewModel viewModel &&
            _contextCell is { } cell &&
            HexViewport.TryGetCellBounds(cell, out Rect bounds))
        {
            BeginInlineHexByteEdit(viewModel, cell, bounds);
        }
    }

    private void BeginInlineHexByteEdit(
        HexEditorWorkspaceViewModel viewModel,
        HexEditorByteCellViewModel cell,
        Rect bounds)
    {
        if (_inlineEditCell is not null)
        {
            CompleteInlineEdit(viewModel, commit: true);
        }

        viewModel.BeginByteEditCommand.Execute(cell);
        _inlineEditCell = cell;
        HexInlineEditor.Text = cell.EditValue;
        HexInlineEditor.Width = Math.Max(24, bounds.Width - 2);
        HexInlineEditor.Height = bounds.Height;
        HexInlineEditor.Margin = new Thickness(bounds.X + 1, bounds.Y, 0, 0);
        HexInlineEditor.IsVisible = true;
        Dispatcher.UIThread.Post(() =>
        {
            _ = HexInlineEditor.Focus();
            HexInlineEditor.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void CompleteInlineEdit(HexEditorWorkspaceViewModel viewModel, bool commit)
    {
        if (_isCompletingInlineEdit || _inlineEditCell is not { } cell)
        {
            return;
        }

        _isCompletingInlineEdit = true;
        try
        {
            cell.EditValue = HexInlineEditor.Text ?? string.Empty;
            if (commit)
            {
                viewModel.CommitByteEditCommand.Execute(cell);
                if (cell.IsEditing)
                {
                    viewModel.SetTextEntryFocused(true);
                    Dispatcher.UIThread.Post(() =>
                    {
                        _ = HexInlineEditor.Focus();
                        HexInlineEditor.SelectAll();
                    }, DispatcherPriority.Input);
                    return;
                }
            }
            else
            {
                viewModel.CancelByteEditCommand.Execute(cell);
            }

            HexInlineEditor.IsVisible = false;
            _inlineEditCell = null;
            viewModel.SetTextEntryFocused(false);
            _ = HexViewport.Focus();
        }
        finally
        {
            _isCompletingInlineEdit = false;
        }
    }

    private static void BindContextCommand(MenuItem menuItem, string header, ICommand command, object parameter)
    {
        menuItem.Header = header;
        menuItem.Command = command;
        menuItem.CommandParameter = parameter;
    }

    private void HexDocumentScrollBar_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel)
        {
            return;
        }

        if (_inlineEditCell is not null)
        {
            CompleteInlineEdit(viewModel, commit: true);
        }

        _pendingDocumentScrollRow = checked((int)Math.Floor(e.NewValue));
        QueueDocumentScroll(viewModel);
    }

    private void HexViewport_OnScrollRequested(object? sender, HexEditorViewportScrollEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel)
        {
            return;
        }

        if (_inlineEditCell is not null)
        {
            CompleteInlineEdit(viewModel, commit: true);
        }

        RequestDocumentScroll(viewModel, e.RowDelta);
    }

    private void HexDocumentSurface_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel || e.Delta.Y == 0)
        {
            return;
        }

        if (_inlineEditCell is not null)
        {
            CompleteInlineEdit(viewModel, commit: true);
        }

        const int rowsPerWheelStep = 3;
        RequestDocumentScroll(viewModel, e.Delta.Y < 0 ? rowsPerWheelStep : -rowsPerWheelStep);
        e.Handled = true;
    }

    private void HexDocumentSurface_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel)
        {
            return;
        }

        Point point = e.GetPosition(HexViewport);
        bool isRightButton = e.GetCurrentPoint(HexDocumentSurface).Properties.IsRightButtonPressed;
        if (isRightButton &&
            HexViewport.TryGetStructuralBlockAt(
                point,
                out HexEditorByteCellViewModel? structuralCell,
                out Rect structuralBounds))
        {
            _ = HexViewport.Focus();
            HexViewport_OnStructuralBlockContextRequested(
                this,
                new HexEditorViewportCellEventArgs(structuralCell!, structuralBounds));
            e.Handled = true;
            return;
        }

        if (!HexViewport.TryGetCellAt(point, out HexEditorByteCellViewModel? cell, out Rect bounds) &&
            !HexViewport.TryGetAsciiCellAt(point, out cell, out bounds))
        {
            return;
        }

        _ = HexViewport.Focus();
        viewModel.SelectByteCommand.Execute(cell);
        HexViewport.InvalidateVisual();
        if (isRightButton)
        {
            HexViewport_OnContextRequested(this, new HexEditorViewportCellEventArgs(cell!, bounds));
        }

        e.Handled = true;
    }

    private void HexDocumentSurface_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is HexEditorWorkspaceViewModel viewModel &&
            HexViewport.TryGetCellAt(e.GetPosition(HexViewport), out HexEditorByteCellViewModel? cell, out Rect bounds))
        {
            BeginInlineHexByteEdit(viewModel, cell!, bounds);
            e.Handled = true;
        }
    }

    private void RequestDocumentScroll(HexEditorWorkspaceViewModel viewModel, int rowDelta)
    {
        int currentTarget = _isDocumentScrollQueued
            ? _pendingDocumentScrollRow
            : viewModel.ViewportStartRow;
        _pendingDocumentScrollRow = Math.Clamp(
            currentTarget + rowDelta,
            0,
            viewModel.DocumentScrollMaximum);
        QueueDocumentScroll(viewModel);
    }

    private void QueueDocumentScroll(HexEditorWorkspaceViewModel viewModel)
    {
        if (_isDocumentScrollQueued)
        {
            return;
        }

        _isDocumentScrollQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _isDocumentScrollQueued = false;
                viewModel.SetViewportStartRowCommand.Execute(_pendingDocumentScrollRow);
            },
            DispatcherPriority.Render);
    }

}
