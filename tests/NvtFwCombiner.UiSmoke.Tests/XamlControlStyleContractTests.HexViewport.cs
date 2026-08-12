using Avalonia;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Hex feedback follows the shell motion preference and resolves its font from the shared theme token.</summary>
    [Fact]
    public void HexViewportUsesShellReducedMotionAndThemeFont()
    {
        string editor = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string inspector = ReadPresentationFile("Views/BinInspectorPanel.axaml");
        string report = ReadPresentationFile("Resources/MainWindowReportAuditTemplates.axaml");
        string viewport = ReadPresentationFile("Views/HexViewportControl.cs");
        string viewportTheme = ReadPresentationFile("Views/HexViewportControl.Theme.cs");
        string historyFeedback = ReadPresentationFile("Views/HexViewportControl.HistoryFeedback.cs");
        const string reducedMotionBinding =
            "IsReducedMotionEnabled=\"{ReflectionBinding $parent[Window].DataContext.IsReducedMotionEnabled}\"";

        Assert.Contains(reducedMotionBinding, editor, StringComparison.Ordinal);
        Assert.Contains(reducedMotionBinding, inspector, StringComparison.Ordinal);
        Assert.Contains(reducedMotionBinding, report, StringComparison.Ordinal);
        Assert.Contains("IsReducedMotionEnabledProperty", viewport, StringComparison.Ordinal);
        Assert.Contains("NfcTechnicalFontFamily", viewportTheme, StringComparison.Ordinal);
        Assert.Contains("ShouldAnimateHistoryFeedback", historyFeedback, StringComparison.Ordinal);
        Assert.DoesNotContain("Cascadia Mono, Consolas", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("Cascadia Mono, Consolas", viewportTheme, StringComparison.Ordinal);
        Assert.DoesNotContain("Cascadia Mono, Consolas", historyFeedback, StringComparison.Ordinal);
    }

    /// <summary>Reduced motion keeps the Undo/Redo cue visible without running its decorative timer.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public void HexHistoryFeedbackAnimationRespectsReducedMotion(
        bool isReducedMotionEnabled,
        bool hasHistoryFeedback,
        bool expected)
    {
        Assert.Equal(
            expected,
            HexViewportControl.ShouldAnimateHistoryFeedback(isReducedMotionEnabled, hasHistoryFeedback));
    }

    /// <summary>Ensures Hex Editor uses the shared safe-save and immutable-reference interaction contracts.</summary>
    [Fact]
    public void HexEditorUsesConfirmedSaveAndReadOnlyReferenceRows()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string hexEditor = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string viewport = ReadPresentationFile("Views/HexViewportControl.cs");
        string viewportTheme = ReadPresentationFile("Views/HexViewportControl.Theme.cs");
        string historyFeedback = ReadPresentationFile("Views/HexViewportControl.HistoryFeedback.cs");
        string renderingSupport = ReadPresentationFile("Views/HexViewportControl.RenderingSupport.cs");
        string sharedStyles = ReadPresentationFile("Styles/MainWindowStyles.axaml");

        Assert.Contains("Gesture=\"Ctrl+S\"", shell, StringComparison.Ordinal);
        Assert.Contains("RequestHexEditorSaveCommand", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HexInlineEditor\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("<views:HexViewportControl", hexEditor, StringComparison.Ordinal);
        Assert.Contains("IsComparisonRowVisible", viewport, StringComparison.Ordinal);
        Assert.Contains("DrawReferenceRow", viewport, StringComparison.Ordinal);
        Assert.Contains("ReferenceChangedBrush", viewport, StringComparison.Ordinal);
        Assert.Contains("ActualThemeVariant", viewportTheme, StringComparison.Ordinal);
        Assert.Contains("ActualThemeVariantChanged", viewportTheme, StringComparison.Ordinal);
        Assert.Contains("InvalidateVisual();", viewportTheme, StringComparison.Ordinal);
        Assert.Contains("TryFindResource", viewportTheme, StringComparison.Ordinal);
        Assert.Contains("EnsureThemePalette();", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("Brush.Parse", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("Brush.Parse", viewportTheme, StringComparison.Ordinal);
        Assert.DoesNotMatch("#[0-9A-Fa-f]{6,8}", viewport);
        Assert.DoesNotMatch("#[0-9A-Fa-f]{6,8}", viewportTheme);
        Assert.Contains("DrawAsciiStructuralBlocks", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("IBrush StructuralBrush =", viewport, StringComparison.Ordinal);
        Assert.Contains("$\"0x{address:X6}  {ComparisonRowLabel}\"", renderingSupport, StringComparison.Ordinal);
        Assert.Contains("DecorationVersion", historyFeedback, StringComparison.Ordinal);
        Assert.Contains("DispatcherTimer", historyFeedback, StringComparison.Ordinal);
        Assert.Contains("DrawHistoryFeedback", historyFeedback, StringComparison.Ordinal);
        Assert.Contains("DrawAsciiSearchRanges", viewport, StringComparison.Ordinal);
        Assert.Equal(2, viewport.Split("DrawHoverOutline(context, rect", StringSplitOptions.None).Length - 1);
        Assert.Contains("TextChanged=\"HexByteEditBox_OnTextChanged\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("behaviors:HexTextInputBehavior.Mode=\"Byte\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("behaviors:HexTextInputBehavior.Mode=\"ByteSequence\"", hexEditor, StringComparison.Ordinal);
        Assert.Equal(3, hexEditor.Split("behaviors:HexTextInputBehavior.Mode=\"Address\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("behaviors:HexTextInputBehavior.Mode", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("input:InputMethod.IsInputMethodEnabled=\"False\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("HexEditorSourceDrop_OnDrop", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Height=\"{Binding HexViewportHeight}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding EditorStatus}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"hexInspectorHint\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes.error=\"{Binding HasEditFeedback}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding EditNotice}\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"hexInspectorFeedback\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding EditFeedback}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.hexInspectorHint\"", ReadPresentationFile("Styles/MainWindowControlStyles.axaml"), StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.hexInspectorHint.error\"", ReadPresentationFile("Styles/MainWindowControlStyles.axaml"), StringComparison.Ordinal);
    }

    /// <summary>Uses one hover overlay policy for both Hex and ASCII across every visible byte state.</summary>
    [Fact]
    public void HexEditorHoverOutlineDoesNotDisappearBehindByteState()
    {
        foreach (HexViewportCellVisualState state in Enum.GetValues<HexViewportCellVisualState>())
        {
            Assert.True(HexViewportControl.ShouldDrawHoverOutline(isReference: false, isHovered: true, state));
            Assert.False(HexViewportControl.ShouldDrawHoverOutline(isReference: false, isHovered: false, state));
            Assert.False(HexViewportControl.ShouldDrawHoverOutline(isReference: true, isHovered: true, state));
        }
    }

    /// <summary>Preserves the legacy blank for a comparison byte beyond the original document tail.</summary>
    [Fact]
    public void HexViewportKeepsMissingComparisonAsciiBlank()
    {
        Assert.Equal(' ', HexViewportControl.ResolveAsciiCharacter(null, isReference: true));
        Assert.Equal('.', HexViewportControl.ResolveAsciiCharacter(null, isReference: false));
        Assert.Equal('.', HexViewportControl.ResolveAsciiCharacter(0x00, isReference: true));
        Assert.Equal('A', HexViewportControl.ResolveAsciiCharacter(0x41, isReference: true));
    }

    /// <summary>Every pixel inside a byte cell, including glyph-free corners, resolves to that byte.</summary>
    [Theory]
    [InlineData(100.01, 20.01, 0)]
    [InlineData(129.99, 44.99, 0)]
    [InlineData(130.00, 44.99, 1)]
    [InlineData(159.99, 20.01, 1)]
    [InlineData(160.00, 30.00, -1)]
    [InlineData(120.00, 45.00, -1)]
    public void HexEditorHoverUsesTheCompleteHalfOpenByteCell(
        double x,
        double y,
        int expectedIndex)
    {
        int index = HexViewportControl.ResolveCellIndex(
            new Point(x, y),
            cellStart: 100,
            cellWidth: 30,
            cellCount: 2,
            rowTop: 20,
            rowHeight: 25);

        Assert.Equal(expectedIndex, index);
    }

    /// <summary>The arranged viewport uses full cell rectangles for hover and keeps click selection unchanged.</summary>
    [Fact]
    public async Task HexEditorArrangedViewportTracksHoverAtCellCornersAndExit()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-hex-hover-cell");
        string sourcePath = workspace.Write("hover.bin", [.. Enumerable.Range(0, 32).Select(index => (byte)index)]);
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        await shell.HexEditorWorkspace.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        var viewport = new HexViewportControl
        {
            Snapshot = shell.HexEditorWorkspace.ViewportSnapshot,
        };
        viewport.Measure(new Size(1080, 50));
        viewport.Arrange(new Rect(0, 0, 1080, 50));

        Assert.True(viewport.TryGetCellAt(new Point(117, 1), out HexViewportCell resolvedCell, out Rect bounds));
        Point[] glyphFreeCorners =
        [
            new(bounds.Left + 0.01, bounds.Top + 0.01),
            new(bounds.Right - 0.01, bounds.Bottom - 0.01),
        ];

        foreach (Point point in glyphFreeCorners)
        {
            viewport.UpdateHoveredCell(point);
            Assert.Equal(resolvedCell.Address, viewport.HoveredAddress);
            Assert.True(HexViewportControl.ShouldDrawHoverOutline(
                isReference: false,
                isHovered: true,
                HexViewportCellVisualState.Normal));
        }

        viewport.ClearHoveredCell();
        Assert.Null(viewport.HoveredAddress);

        shell.HexEditorWorkspace.SelectByte(resolvedCell.Address);
        Assert.Equal(resolvedCell.Address, shell.HexEditorWorkspace.ViewportSnapshot.SelectedAddress);
    }

    /// <summary>Ensures the Hex Editor inspector remains compact and the source and data surfaces share one workbench grid.</summary>
    [Fact]
    public void HexEditorInspectorUsesCompactTopAlignedLayout()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string hexEditor = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string codeBehind = ReadPresentationFile("Views/HexEditorPanel.axaml.cs");

        Assert.Contains("<ScrollViewer Grid.Row=\"3\">", shell, StringComparison.Ordinal);
        Assert.True(
            shell.IndexOf("ContentTemplate=\"{StaticResource HexEditorPageTemplate}\"", StringComparison.Ordinal) <
            shell.IndexOf("</ScrollViewer>", StringComparison.Ordinal));
        Assert.Contains("MaxWidth=\"1720\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,336\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"1\" Classes=\"compactSurface\" VerticalAlignment=\"Stretch\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,*\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("<ScrollBar", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,20\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"{Binding DocumentScrollMaximum}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ViewportSize=\"{Binding VisibleRowCount}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ValueChanged=\"HexDocumentScrollBar_OnValueChanged\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.HexEditorDocumentScrollBarAutomationName}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AccessibilityView=\"Content\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"iconButton hexGoToAddress\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding Text.HexEditorGoToAddressLabel}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding FindAsciiCommand}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding SelectedByteAccessibleLabel}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"GoToAddressTextBox\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"GoToAddressTextBox_OnKeyDown\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AsciiSearchTextBox\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"AsciiSearchTextBox_OnKeyDown\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AddHandler(KeyDownEvent, HexEditorPanel_OnKeyDown, RoutingStrategies.Tunnel)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"Auto,*,196\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ToggleSwitch", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"hexOriginalRowsToggle\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("SelectNextChangedBlockCommand", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ChangedBlockCount", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding ChangedBlockNavigationAccessibleLabel}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding ChangedBlockPage.PageStatus}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ApplyRangeEditCommand}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"hexWriteModeToggle\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding CurrentWriteModeLabel}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"iconButton hexInspectorAction\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Classes=\"primary hexApplyChange\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding ChangedBlockPage}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{StaticResource HexEditorChangedBlockPagerTemplate}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding ChangedBlockPage.HasMultiplePages}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ChangedBlockPage.Items}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"Auto,*,Auto\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"48\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding ReasonTooltip}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding ReasonTooltip}\"", hexEditor, StringComparison.Ordinal);
        Assert.Contains("TextAlignment=\"Left\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"hexEditMode\"", hexEditor, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved region", hexEditor, StringComparison.OrdinalIgnoreCase);
    }
}
