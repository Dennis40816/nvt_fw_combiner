using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;
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
    private long? _contextAddress;
    private long? _inlineEditAddress;
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
        HexViewport.InteractionRequested += HexViewport_OnInteractionRequested;
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

        if (_inlineEditAddress is not null)
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

        SingleLocalFileDropSelection selection = DropZoneDragState.GetSingleLocalFile(e);
        e.Handled = true;
        if (!selection.IsAccepted)
        {
            if (ShellViewModelLocator.Find(this) is MainWindowViewModel shell)
            {
                shell.Reports.SetShellToast(
                    shell.Text.FileDropRejectedTitle,
                    shell.Text.FileDropRejectedDetail);
            }

            return;
        }

        await viewModel.LoadAsync(selection.Path!);
    }

    private void HexViewport_OnInteractionRequested(object? sender, HexViewportInteractionEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel)
        {
            return;
        }

        HexViewportInteractionIntent intent = e.Intent;
        if (intent.Trigger == HexViewportInteractionTrigger.Select && intent.Address is long selectAddress)
        {
            viewModel.SelectByte(selectAddress);
        }
        else if (intent.Trigger == HexViewportInteractionTrigger.Activate && intent.Address is long activateAddress)
        {
            BeginInlineHexByteEdit(viewModel, activateAddress, intent.Bounds);
        }
        else if (intent.Trigger == HexViewportInteractionTrigger.Context && intent.Address is long contextAddress)
        {
            OpenByteContextMenu(viewModel, contextAddress);
        }
        else if (intent.Trigger == HexViewportInteractionTrigger.StructuralContext)
        {
            OpenStructuralContextMenu(viewModel, intent.StructuralBlockIndex);
        }
        else if (intent.Trigger == HexViewportInteractionTrigger.MoveSelection)
        {
            viewModel.MoveSelection(intent.Delta);
        }
        else if (intent.Trigger == HexViewportInteractionTrigger.Scroll)
        {
            if (_inlineEditAddress is not null)
            {
                CompleteInlineEdit(viewModel, commit: true);
            }

            RequestDocumentScroll(viewModel, intent.Delta);
        }
    }

    private void OpenByteContextMenu(HexEditorWorkspaceViewModel viewModel, long address)
    {
        _contextAddress = address;
        _contextEdit.Header = viewModel.Text.HexEditorEditByteLabel;
        BindContextCommand(_contextInsertBefore, viewModel.Text.HexEditorContextInsertZeroBeforeLabel, viewModel.InsertZeroBeforeCommand, address);
        BindContextCommand(_contextInsertAfter, viewModel.Text.HexEditorContextInsertZeroAfterLabel, viewModel.InsertZeroAfterCommand, address);
        BindContextCommand(_contextInsertManyBefore, viewModel.Text.HexEditorContextInsertBytesBeforeLabel, viewModel.RequestInsertBytesBeforeCommand, address);
        BindContextCommand(_contextInsertManyAfter, viewModel.Text.HexEditorContextInsertBytesAfterLabel, viewModel.RequestInsertBytesAfterCommand, address);
        BindContextCommand(_contextDeleteByte, viewModel.Text.HexEditorContextDeleteByteLabel, viewModel.DeleteByteCommand, address);
        BindContextCommand(_contextSetToZero, viewModel.Text.HexEditorContextSetToZeroLabel, viewModel.SetByteToZeroCommand, address);
        BindContextCommand(_contextSetToFf, viewModel.Text.HexEditorContextSetToFfLabel, viewModel.SetByteToFfCommand, address);
        _hexByteContextMenu.Open(HexViewport);
    }

    private void OpenStructuralContextMenu(HexEditorWorkspaceViewModel viewModel, int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= viewModel.ChangedBlockCount)
        {
            return;
        }

        HexEditorChangedBlockViewModel? block = viewModel.GetChangedBlock(blockIndex);
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
            _inlineEditAddress is null)
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
            _inlineEditAddress is not null)
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
        _ = sender;
        _ = e;
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
            _contextAddress is long address &&
            HexViewport.TryGetCellBounds(address, out Rect bounds))
        {
            BeginInlineHexByteEdit(viewModel, address, bounds);
        }
    }

    private void BeginInlineHexByteEdit(
        HexEditorWorkspaceViewModel viewModel,
        long address,
        Rect bounds)
    {
        if (_inlineEditAddress is not null)
        {
            CompleteInlineEdit(viewModel, commit: true);
        }

        viewModel.BeginByteEditCommand.Execute(address);
        if (!viewModel.IsInlineEditActive)
        {
            return;
        }

        _inlineEditAddress = address;
        HexInlineEditor.Text = viewModel.GetCurrentHex(address);
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
        if (_isCompletingInlineEdit || _inlineEditAddress is not long address)
        {
            return;
        }

        _isCompletingInlineEdit = true;
        try
        {
            if (commit)
            {
                viewModel.CommitByteEditCommand.Execute(new HexEditorByteEditRequest(
                    address,
                    HexInlineEditor.Text ?? string.Empty));
                if (viewModel.IsInlineEditActive)
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
                viewModel.CancelByteEditCommand.Execute(address);
            }

            HexInlineEditor.IsVisible = false;
            _inlineEditAddress = null;
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

        if (_inlineEditAddress is not null)
        {
            CompleteInlineEdit(viewModel, commit: true);
        }

        _pendingDocumentScrollRow = checked((int)Math.Floor(e.NewValue));
        QueueDocumentScroll(viewModel);
    }

    private void HexDocumentSurface_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not HexEditorWorkspaceViewModel viewModel || e.Delta.Y == 0)
        {
            return;
        }

        if (_inlineEditAddress is not null)
        {
            CompleteInlineEdit(viewModel, commit: true);
        }

        const int rowsPerWheelStep = 3;
        RequestDocumentScroll(viewModel, e.Delta.Y < 0 ? rowsPerWheelStep : -rowsPerWheelStep);
        e.Handled = true;
    }

    private void HexDocumentSurface_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        HexViewport.UpdateHoveredCell(e.GetPosition(HexViewport));
    }

    private void HexDocumentSurface_OnPointerExited(object? sender, PointerEventArgs e)
    {
        HexViewport.ClearHoveredCell();
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
                out HexViewportCell structuralCell,
                out _))
        {
            _ = HexViewport.Focus();
            OpenStructuralContextMenu(viewModel, structuralCell.StructuralBlockIndex);
            e.Handled = true;
            return;
        }

        if (!HexViewport.TryGetCellAt(point, out HexViewportCell cell, out _) &&
            !HexViewport.TryGetAsciiCellAt(point, out cell, out _))
        {
            return;
        }

        _ = HexViewport.Focus();
        viewModel.SelectByte(cell.Address);
        if (isRightButton)
        {
            OpenByteContextMenu(viewModel, cell.Address);
        }

        e.Handled = true;
    }

    private void HexDocumentSurface_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is HexEditorWorkspaceViewModel viewModel &&
            HexViewport.TryGetCellAt(e.GetPosition(HexViewport), out HexViewportCell cell, out Rect bounds))
        {
            BeginInlineHexByteEdit(viewModel, cell.Address, bounds);
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
