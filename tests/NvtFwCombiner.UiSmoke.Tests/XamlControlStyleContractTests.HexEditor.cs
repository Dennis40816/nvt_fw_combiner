using System.Reflection;
using Avalonia;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps each byte value centered on the calculated 16-column viewport geometry.</summary>
    [Fact]
    public void HexEditorByteCellsCenterTheirContentUnderTheHeader()
    {
        string viewport = ReadPresentationFile("Views/HexEditorViewportControl.cs");

        Assert.Contains("private const int BytesPerRow = 16", viewport, StringComparison.Ordinal);
        Assert.Contains("rect.X + ((rect.Width - text.Width) / 2)", viewport, StringComparison.Ordinal);
        Assert.Contains("rect.Y + ((rect.Height - text.Height) / 2)", viewport, StringComparison.Ordinal);
        Assert.Contains("GetCellWidth()", viewport, StringComparison.Ordinal);
    }

    /// <summary>Prevents document scrolling from recreating a control, binding, and template for every visible byte.</summary>
    [Fact]
    public void HexEditorUsesReadMostlyCellsAndOneSharedContextMenu()
    {
        string hexEditor = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string codeBehind = ReadPresentationFile("Views/HexEditorPanel.axaml.cs");
        string viewport = ReadPresentationFile("Views/HexEditorViewportControl.cs");
        string viewportInteraction = ReadPresentationFile("Views/HexEditorViewportControl.Interaction.cs");
        string viewportStructuralBlocks = ReadPresentationFile("Views/HexEditorViewportControl.StructuralBlocks.cs");
        string renderingSupport = ReadPresentationFile("Views/HexEditorViewportControl.RenderingSupport.cs");
        string hexInputBehavior = ReadPresentationFile("Behaviors/HexTextInputBehavior.cs");

        Assert.Contains("<views:HexEditorViewportControl", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding ViewportRows}\"", hexEditor, StringComparison.Ordinal);
        Assert.Equal(2, hexEditor.Split("<ContentControl", StringSplitOptions.None).Length);
        Assert.Contains("Content=\"{Binding ChangedBlockPage}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource HexEditorChangedBlockPagerTemplate}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("public override void Render(DrawingContext context)", viewport, StringComparison.Ordinal);
        Assert.Contains("DrawByte(context", viewport, StringComparison.Ordinal);
        Assert.Contains("internal string? HoveredAddress { get; private set; }", viewport, StringComparison.Ordinal);
        Assert.Contains("internal void UpdateHoveredCell(Point point)", viewportInteraction, StringComparison.Ordinal);
        Assert.Contains("CreateHexTextCache", renderingSupport, StringComparison.Ordinal);
        Assert.Contains("PointerPressed += OnPointerPressed", viewport, StringComparison.Ordinal);
        Assert.Contains("TryHitTestAscii", viewportInteraction, StringComparison.Ordinal);
        Assert.True(
            viewportInteraction.IndexOf("TryHitTestAscii(point", StringComparison.Ordinal) <
            viewportInteraction.IndexOf("TryHitTestStructuralAscii(point", StringComparison.Ordinal));
        Assert.Contains("!TryHitTestAscii(point, out cell, out bounds)", viewportInteraction, StringComparison.Ordinal);
        Assert.Contains("HexViewport.TryGetAsciiCellAt(point", codeBehind, StringComparison.Ordinal);
        Assert.Contains("protected override void OnPointerWheelChanged", viewportInteraction, StringComparison.Ordinal);
        Assert.Contains("ScrollRequested?.Invoke", viewportInteraction, StringComparison.Ordinal);
        Assert.Contains("Background=\"Transparent\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("KeyDown += OnKeyDown", viewport, StringComparison.Ordinal);
        Assert.Contains("GotFocus=\"HexTextInput_OnGotFocus\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("LostFocus=\"HexTextInput_OnLostFocus\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button.ContextMenu>", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAuthoringDisplayVisible", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEditorVisible", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("VirtualizingStackPanel", hexEditor, StringComparison.Ordinal);
        Assert.Contains("new ContextMenu()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_hexByteContextMenu.Open(HexViewport)", codeBehind, StringComparison.Ordinal);
        string[] byteContextBindings =
        [
            "BindContextCommand(_contextInsertBefore, viewModel.Text.HexEditorContextInsertZeroBeforeLabel, viewModel.InsertZeroBeforeCommand, e.Cell);",
            "BindContextCommand(_contextInsertAfter, viewModel.Text.HexEditorContextInsertZeroAfterLabel, viewModel.InsertZeroAfterCommand, e.Cell);",
            "BindContextCommand(_contextInsertManyBefore, viewModel.Text.HexEditorContextInsertBytesBeforeLabel, viewModel.RequestInsertBytesBeforeCommand, e.Cell);",
            "BindContextCommand(_contextInsertManyAfter, viewModel.Text.HexEditorContextInsertBytesAfterLabel, viewModel.RequestInsertBytesAfterCommand, e.Cell);",
            "BindContextCommand(_contextDeleteByte, viewModel.Text.HexEditorContextDeleteByteLabel, viewModel.DeleteByteCommand, e.Cell);",
            "BindContextCommand(_contextSetToZero, viewModel.Text.HexEditorContextSetToZeroLabel, viewModel.SetByteToZeroCommand, e.Cell);",
            "BindContextCommand(_contextSetToFf, viewModel.Text.HexEditorContextSetToFfLabel, viewModel.SetByteToFfCommand, e.Cell);",
        ];
        Assert.All(byteContextBindings, binding => Assert.Contains(binding, codeBehind, StringComparison.Ordinal));
        Assert.Contains("menuItem.Header = header", codeBehind, StringComparison.Ordinal);
        Assert.Contains("menuItem.Command = command", codeBehind, StringComparison.Ordinal);
        Assert.Contains("menuItem.CommandParameter = parameter", codeBehind, StringComparison.Ordinal);
        Assert.Contains("StructuralBlockContextMenuRequested", viewport, StringComparison.Ordinal);
        Assert.Contains("TryHitTestStructuralAscii", viewportStructuralBlocks, StringComparison.Ordinal);
        Assert.Contains("ContainsStructuralPoint", viewportStructuralBlocks, StringComparison.Ordinal);
        Assert.Contains("row.IsOriginalRowVisible ? RowHeight * 2 : RowHeight", viewportStructuralBlocks, StringComparison.Ordinal);
        Assert.Contains("HexViewport.TryGetStructuralBlockAt", codeBehind, StringComparison.Ordinal);
        Assert.True(
            codeBehind.IndexOf("HexViewport.TryGetStructuralBlockAt", StringComparison.Ordinal) <
            codeBehind.IndexOf("HexViewport.TryGetCellAt(point", StringComparison.Ordinal));
        Assert.Contains(
            "BindContextCommand(_structuralGoToStart, viewModel.Text.HexEditorContextGoToBlockStartLabel, viewModel.GoToChangedBlockStartCommand, block);",
            codeBehind,
            StringComparison.Ordinal);
        Assert.Contains(
            "BindContextCommand(_structuralGoToEnd, viewModel.Text.HexEditorContextGoToBlockEndLabel, viewModel.GoToChangedBlockEndCommand, block);",
            codeBehind,
            StringComparison.Ordinal);
        Assert.Contains("NormalizeAddress", hexInputBehavior, StringComparison.Ordinal);
        Assert.Contains("NormalizeByteSequence", hexInputBehavior, StringComparison.Ordinal);
        Assert.DoesNotContain("KeepAsciiHexOnly", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexDocumentScrollBar_OnValueChanged", codeBehind, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HexDocumentSurface\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("HexViewport_OnScrollRequested", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexDocumentSurface_OnPointerWheelChanged", codeBehind, StringComparison.Ordinal);
        Assert.Contains("PointerMoved=\"HexDocumentSurface_OnPointerMoved\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("PointerExited=\"HexDocumentSurface_OnPointerExited\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("HexViewport.UpdateHoveredCell(e.GetPosition(HexViewport))", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexViewport.ClearHoveredCell()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexDocumentSurface_OnDoubleTapped", codeBehind, StringComparison.Ordinal);
        Assert.Contains("QueueDocumentScroll", codeBehind, StringComparison.Ordinal);
        Assert.Contains("QueueViewportLayout", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexEditorSourceDrop_OnDrop", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HexTextInput_OnGotFocus", codeBehind, StringComparison.Ordinal);
        Assert.Contains("CompleteInlineEdit", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SetViewportStartRowCommand.Execute", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isDocumentScrollQueued", codeBehind, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Render", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>Right-click hit testing includes blank pixels inside one wrapped structural outline.</summary>
    [Fact]
    public void HexEditorStructuralOutlineIncludesBlankAndCrossRowAreas()
    {
        MethodInfo hitTest = typeof(HexEditorViewportControl).GetMethod(
            "ContainsStructuralPoint",
            BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("Structural outline hit test was not found.");
        Rect[] segments =
        [
            new Rect(140, 10, 30, 48),
            new Rect(80, 60, 40, 48),
        ];

        bool Contains(Point point)
        {
            return (bool)hitTest.Invoke(null, [segments, point, 80d, 220d])!;
        }

        Assert.True(Contains(new Point(200, 30))); // Blank tail of the first wrapped row.
        Assert.True(Contains(new Point(100, 59))); // Blank gap between wrapped rows.
        Assert.True(Contains(new Point(90, 90))); // Blank head of the final wrapped row.
        Assert.False(Contains(new Point(70, 59)));
        Assert.False(Contains(new Point(221, 30)));
    }
}
