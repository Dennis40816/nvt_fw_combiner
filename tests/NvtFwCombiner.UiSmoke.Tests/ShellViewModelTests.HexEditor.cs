using System.Diagnostics;
using System.Globalization;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>UI-state coverage for the standalone profile-independent raw BIN utility.</summary>
public sealed partial class ShellViewModelTests
{
    /// <summary>Rejects a source that would exceed the bounded in-memory document contract.</summary>
    [Fact]
    public async Task HexEditorRejectsDocumentsBeyondTheMemoryLimit()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-limit");
        string sourcePath = workspace.Write(
            "oversized.bin",
            new byte[RawBinaryEditorSession.MaximumDocumentLength + 1]);
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();

        await shell.HexEditorWorkspace.LoadAsync(sourcePath, TestContext.Current.CancellationToken);

        Assert.False(shell.HexEditorWorkspace.HasDocument);
        Assert.Contains(
            RawBinaryEditorSession.MaximumDocumentLength.ToString(CultureInfo.InvariantCulture),
            shell.HexEditorWorkspace.EditorStatus,
            StringComparison.Ordinal);
    }

    /// <summary>Locks raw Hex Editor state to an isolated fixed-window document without IC or Replace state.</summary>
    [Fact]
    public async Task HexEditorLoadsOneMemoryDocumentAndFocusesMatchingRowAndColumn()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-load");
        byte[] source = CreateHexPattern(16 * 4_097);
        string sourcePath = workspace.Write("source.bin", source);
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();

        shell.ShowHexEditorCommand.Execute(null);
        await shell.HexEditorWorkspace.LoadAsync(sourcePath, TestContext.Current.CancellationToken);

        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;
        Assert.True(shell.IsHexEditorVisible);
        Assert.False(shell.IsReplaceVisible);
        Assert.True(editor.HasDocument);
        Assert.Equal(4_097, editor.TotalRowCount);
        Assert.Equal(12, editor.ViewportSnapshot.Rows.Count);
        Assert.Equal(4_085, editor.DocumentScrollMaximum);
        Assert.Equal("source.bin", editor.SourceName);

        HexViewportRow row = editor.ViewportSnapshot.Rows[0];
        editor.SelectByte(row.Cells[5].Address);

        Assert.Equal(row.Cells[5].Address, editor.ViewportSnapshot.SelectedAddress);
        Assert.True(editor.ColumnHeaders[5].IsSelected);
        Assert.False(editor.ColumnHeaders[4].IsSelected);
        Assert.Equal(FormatTestAddress(row.Cells[5].Address), editor.RangeStartAddress);
        Assert.Equal(FormatTestAddress(row.Cells[5].Address), editor.RangeEndAddress);

        editor.MoveSelection(17);
        Assert.Equal(editor.ViewportSnapshot.Rows[1].Cells[6].Address, editor.ViewportSnapshot.SelectedAddress);
        Assert.True(editor.ColumnHeaders[6].IsSelected);
        Assert.Equal(FormatTestAddress(editor.ViewportSnapshot.Rows[1].Cells[6].Address), editor.SelectedByteAddress);
    }

    /// <summary>Uses stable full-document scroll geometry while replacing only one bounded raw-BIN row window.</summary>
    [Fact]
    public async Task HexEditorUsesStableViewportAndExportsOnlyANewBin()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-progressive");
        byte[] source = CreateHexPattern(16 * 40);
        string sourcePath = workspace.Write("source.bin", source);
        string outputPath = workspace.PathFor("edited.bin");
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        Assert.Equal(40, editor.TotalRowCount);
        Assert.Equal(12, editor.ViewportSnapshot.Rows.Count);
        Assert.Equal(28, editor.DocumentScrollMaximum);

        editor.SetViewportStartRowCommand.Execute(editor.TotalRowCount);
        Assert.Equal(28, editor.ViewportStartRow);
        Assert.Equal(0x1C0, editor.ViewportSnapshot.Rows[0].Address);
        editor.SetViewportStartRowCommand.Execute(0);

        HexViewportRow firstRow = editor.ViewportSnapshot.Rows[0];
        long targetAddress = firstRow.Cells[1].Address;
        editor.InsertZeroAfterCommand.Execute(targetAddress);
        Assert.Equal("0x000002", editor.SelectedByteAddress);
        editor.SetByteToFfCommand.Execute(editor.ViewportSnapshot.Rows[0].Cells[0].Address);
        firstRow = editor.ViewportSnapshot.Rows[0];

        Assert.True(editor.CanSave);
        Assert.Equal(41, editor.TotalRowCount);
        Assert.Equal(29, editor.DocumentScrollMaximum);
        Assert.Equal(source[0], firstRow.Cells[0].ComparisonValue);
        Assert.Equal((byte)0xFF, firstRow.Cells[0].PrimaryValue);
        Assert.Equal((byte)0x00, firstRow.Cells[2].PrimaryValue);
        Assert.False(firstRow.Cells[3].IsDataChanged);
        Assert.True(firstRow.Cells[3].IsStructuralChanged);
        Assert.True(firstRow.Cells[3].IsChanged);
        Assert.True(firstRow.Cells[2].IsChanged);

        editor.RequestSaveCommand.Execute(null);
        Assert.True(editor.IsSaveConfirmationOpen);
        editor.CancelSaveCommand.Execute(null);
        await editor.SaveAsAsync(outputPath, TestContext.Current.CancellationToken);

        Assert.False(editor.IsSaveConfirmationOpen);
        Assert.Equal(source, File.ReadAllBytes(sourcePath));
        Assert.Equal((byte)0xFF, File.ReadAllBytes(outputPath)[0]);
        Assert.Equal(source.Length + 1, new FileInfo(outputPath).Length);
    }

    /// <summary>Keeps an invalid inline byte active so the user can correct or cancel it.</summary>
    [Fact]
    public async Task HexEditorInvalidInlineByteRemainsEditable()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-inline-invalid");
        string sourcePath = workspace.Write("source.bin", [0x10]);
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        long address = editor.ViewportSnapshot.Rows[0].Cells[0].Address;
        editor.BeginByteEditCommand.Execute(address);
        editor.CommitByteEditCommand.Execute(new HexEditorByteEditRequest(address, "F"));

        Assert.True(editor.IsInlineEditActive);
        Assert.False(editor.CanSave);
    }

    /// <summary>Keeps earlier rows visible when Go to moves the bounded window to a later address.</summary>
    [Fact]
    public async Task HexEditorGoToPreservesFourRowsOfEarlierContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-go-to");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(16 * 64));
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.ViewportAddress = "0x000200";
        editor.GoToCommand.Execute(null);

        Assert.Equal(28, editor.ViewportStartRow);
        Assert.Equal(0x1C0, editor.ViewportSnapshot.Rows[0].Address);
        Assert.Equal(0x200, editor.ViewportSnapshot.Rows[4].Address);
        Assert.Equal(0x200, editor.ViewportSnapshot.SelectedAddress);
    }

    /// <summary>Finds the next ASCII occurrence in the existing memory document without reopening the BIN.</summary>
    [Fact]
    public async Task HexEditorFindAsciiSelectsAndFramesTheMatchedBytes()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-ascii-search");
        string sourcePath = workspace.Write("source.bin", "prefix NVT middle NVT"u8.ToArray());
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.AsciiSearchText = "NVT";
        await editor.FindAsciiCommand.ExecuteAsync(null);

        Assert.Equal("0x000007", editor.SelectedByteAddress);
        Assert.Equal("0x000007", editor.RangeStartAddress);
        Assert.Equal("0x000009", editor.RangeEndAddress);
        Assert.Equal("1/2", editor.AsciiSearchResultLabel);
        Assert.All(editor.ViewportSnapshot.Rows[0].Cells.Skip(7).Take(3), cell => Assert.True(cell.IsSearchMatch));
        Assert.Contains("0x000007", editor.EditorStatus, StringComparison.Ordinal);

        await editor.FindAsciiCommand.ExecuteAsync(null);
        Assert.Equal("0x000012", editor.SelectedByteAddress);
        Assert.Equal("2/2", editor.AsciiSearchResultLabel);
    }

    /// <summary>Labels a structural-edit reference row as mapped source data instead of a false same-address comparison.</summary>
    [Fact]
    public async Task HexEditorReferenceRowsExposeOriginalMappingAfterInsertion()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-original-map");
        string sourcePath = workspace.Write("source.bin", "ABCT"u8.ToArray());
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.InsertZeroAfterCommand.Execute(editor.ViewportSnapshot.Rows[0].Cells[1].Address);
        editor.IsOriginalRowsVisible = true;

        HexViewportRow changedRow = editor.ViewportSnapshot.Rows[0];
        Assert.True(editor.ViewportSnapshot.ShowComparisonRows && changedRow.HasComparison);
        Assert.Equal((byte?)0x43, changedRow.Cells[2].ComparisonValue);
        Assert.False(changedRow.Cells[2].IsDataChanged);
        Assert.True(changedRow.Cells[2].IsStructuralChanged);
        Assert.True(changedRow.Cells[2].IsChanged);
        Assert.Equal((byte)0x54, changedRow.Cells[4].PrimaryValue);
        Assert.Null(changedRow.Cells[4].ComparisonValue);
        Assert.Equal("ABCT ", CreateComparisonAscii(changedRow));
    }

    /// <summary>Groups adjacent memory changes and lets the inspector frame each editable block without reloading the BIN.</summary>
    [Fact]
    public async Task HexEditorTracksAndCyclesContiguousChangedBlocks()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-changed-blocks");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(16 * 8));
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.RangeStartAddress = "0x000002";
        editor.RangeEndAddress = "0x000004";
        editor.RangeValue = "AA BB CC";
        editor.ApplyOverwriteRangeCommand.Execute(null);

        Assert.Equal(1, editor.ChangedBlockCount);
        Assert.True(editor.HasChangedBlocks);
        Assert.False(editor.HasNoChangedBlocks);
        HexEditorChangedBlockViewModel block = Assert.IsType<HexEditorChangedBlockViewModel>(
            Assert.Single(editor.ChangedBlockPage.Items));
        Assert.Equal("0x000002 - 0x000004", block.RangeLabel);
        Assert.Contains("3 values changed", block.ReasonTooltip, StringComparison.Ordinal);
        Assert.Contains("0x000002: 02 -> AA", block.ReasonTooltip, StringComparison.Ordinal);
        editor.SelectNextChangedBlockCommand.Execute(null);
        Assert.Equal("0x000002", editor.SelectedByteAddress);
        Assert.Equal("0x000004", editor.RangeEndAddress);

        editor.RangeStartAddress = "0x000006";
        editor.RangeEndAddress = "0x000007";
        editor.RangeValue = "EE";
        editor.IsFillModeSelected = true;
        editor.ApplyRangeEditCommand.Execute(null);
        Assert.True(editor.IsFillModeSelected);
        Assert.Equal(editor.Text.HexEditorFillModeLabel, editor.CurrentWriteModeLabel);
        Assert.Equal(editor.Text.HexEditorFillModeTooltip, editor.CurrentWriteModeTooltip);
        Assert.Equal((byte)0xEE, editor.ViewportSnapshot.Rows[0].Cells[6].PrimaryValue);
        Assert.Equal((byte)0xEE, editor.ViewportSnapshot.Rows[0].Cells[7].PrimaryValue);
    }

    /// <summary>Keeps a fragmented edit navigator bounded instead of projecting every changed block onto the dispatcher.</summary>
    [Fact]
    public async Task HexEditorBoundsFragmentedChangedBlockProjection()
    {
        const int documentLength = 20_000;
        const int expectedChangedBlocks = documentLength / 2;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-fragmented-blocks");
        string sourcePath = workspace.Write("source.bin", new byte[documentLength]);
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.RangeStartAddress = "0x000000";
        editor.RangeEndAddress = "0x004E1F";
        editor.RangeValue = string.Join(
            ' ',
            Enumerable.Range(0, documentLength).Select(static index => index % 2 == 0 ? "FF" : "00"));
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        editor.ApplyOverwriteRangeCommand.Execute(null);
        stopwatch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(expectedChangedBlocks, editor.ChangedBlockCount);
        Assert.Equal(expectedChangedBlocks, editor.ChangedBlockPage.TotalCount);
        Assert.Equal("Jump to the next edited block; 10000 edited blocks", editor.ChangedBlockNavigationAccessibleLabel);
        Assert.Equal(64, editor.ChangedBlockPage.VisibleCount);
        Assert.True(editor.ChangedBlockPage.HasMultiplePages);
        Assert.Equal("Showing 1-64 of 10000", editor.ChangedBlockPage.PageStatus);
        Assert.Equal(0, Assert.IsType<HexEditorChangedBlockViewModel>(editor.ChangedBlockPage.Items[0]).Index);
        HexEditorChangedBlockViewModel lastInitialBlock = Assert.IsType<HexEditorChangedBlockViewModel>(
            editor.ChangedBlockPage.Items[^1]);
        Assert.Equal(63, lastInitialBlock.Index);

        editor.SelectChangedBlockCommand.Execute(lastInitialBlock);
        editor.SelectNextChangedBlockCommand.Execute(null);

        Assert.Equal("0x000080", editor.SelectedByteAddress);
        Assert.Equal(64, editor.ChangedBlockPage.VisibleCount);
        Assert.Equal(64, Assert.IsType<HexEditorChangedBlockViewModel>(editor.ChangedBlockPage.Items[0]).Index);
        Assert.Equal(127, Assert.IsType<HexEditorChangedBlockViewModel>(editor.ChangedBlockPage.Items[^1]).Index);

        editor.ChangedBlockPage.ShowItemAt(expectedChangedBlocks - 1);

        Assert.Equal(16, editor.ChangedBlockPage.VisibleCount);
        Assert.Equal(expectedChangedBlocks - 1, Assert.IsType<HexEditorChangedBlockViewModel>(editor.ChangedBlockPage.Items[^1]).Index);
        Assert.Equal("Showing 9985-10000 of 10000", editor.ChangedBlockPage.PageStatus);

        HexEditorChangedBlockViewModel finalBlock = Assert.IsType<HexEditorChangedBlockViewModel>(
            editor.ChangedBlockPage.Items[^1]);
        editor.SelectChangedBlockCommand.Execute(finalBlock);
        editor.SelectNextChangedBlockCommand.Execute(null);

        Assert.Equal("0x000000", editor.SelectedByteAddress);
        Assert.Equal(0, editor.ChangedBlockPage.PageIndex);
        Assert.Equal(0, Assert.IsType<HexEditorChangedBlockViewModel>(editor.ChangedBlockPage.Items[0]).Index);

        editor.ApplyTextResources(ShellTextResources.For(ShellLanguage.ChineseTraditional));

        Assert.Equal("跳至下一個修改區塊；共 10000 個修改區塊", editor.ChangedBlockNavigationAccessibleLabel);
        Assert.Equal("顯示第 1-64 筆，共 10000 筆", editor.ChangedBlockPage.PageStatus);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"fragmentedChangedBlocks={expectedChangedBlocks}; visible={editor.ChangedBlockPage.VisibleCount}; " +
            $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}; allocated={allocated}");
    }

    /// <summary>Keeps short overwrite local to Start while showing range validation in the Edit Region only.</summary>
    [Fact]
    public async Task HexEditorAllowsShortOverwriteAndKeepsEndOverflowAsEditFeedback()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-overwrite-boundary");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x20, 0x30]);
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.RangeStartAddress = "0x000000";
        editor.RangeEndAddress = "0x000002";
        editor.RangeValue = "A5";
        editor.ApplyRangeEditCommand.Execute(null);

        Assert.Equal(editor.Text.HexEditorOverwriteGuidance, editor.EditGuidance);
        Assert.Equal(editor.Text.HexEditorOverwriteGuidance, editor.EditNotice);
        Assert.False(editor.HasEditFeedback);
        Assert.Equal((byte)0xA5, editor.ViewportSnapshot.Rows[0].Cells[0].PrimaryValue);
        Assert.Equal((byte)0x20, editor.ViewportSnapshot.Rows[0].Cells[1].PrimaryValue);
        Assert.Equal((byte)0x30, editor.ViewportSnapshot.Rows[0].Cells[2].PrimaryValue);
        Assert.Contains("In memory", editor.EditorStatus, StringComparison.Ordinal);

        editor.RangeValue = "AA BB CC DD";
        editor.ApplyRangeEditCommand.Execute(null);

        Assert.True(editor.HasEditFeedback);
        Assert.Equal(editor.Text.HexEditorInputExceedsRangeDetail, editor.EditFeedback);
        Assert.Equal(editor.Text.HexEditorInputExceedsRangeDetail, editor.EditNotice);
        Assert.Equal((byte)0xA5, editor.ViewportSnapshot.Rows[0].Cells[0].PrimaryValue);
        Assert.Equal((byte)0x20, editor.ViewportSnapshot.Rows[0].Cells[1].PrimaryValue);
        Assert.Equal((byte)0x30, editor.ViewportSnapshot.Rows[0].Cells[2].PrimaryValue);

        editor.IsFillModeSelected = true;
        Assert.Equal(editor.Text.HexEditorFillGuidance, editor.EditGuidance);
        Assert.Equal(editor.Text.HexEditorFillGuidance, editor.EditNotice);
        Assert.False(editor.HasEditFeedback);
    }

    /// <summary>Projects one structural tail block without falsely classifying equal shifted values as data edits.</summary>
    [Fact]
    public async Task HexEditorSeparatesStructuralTailBlocksFromDataDiffs()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-structural-block");
        string sourcePath = workspace.Write("source.bin", [.. Enumerable.Repeat((byte)0xAA, 64)]);
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.DeleteByteCommand.Execute(editor.ViewportSnapshot.Rows[0].Cells[1].Address);

        HexEditorChangedBlockViewModel block = Assert.IsType<HexEditorChangedBlockViewModel>(
            Assert.Single(editor.ChangedBlockPage.Items));
        Assert.Equal("0x000001", block.StartAddress);
        Assert.Equal("0x00003E", block.EndAddress);
        Assert.Contains("Deleted 1 byte(s) at 0x000001", block.ReasonTooltip, StringComparison.Ordinal);
        Assert.False(block.HasDataChanges);
        Assert.True(block.HasStructuralChanges);
        Assert.False(editor.ViewportSnapshot.Rows[0].Cells[1].IsDataChanged);
        Assert.True(editor.ViewportSnapshot.Rows[0].Cells[1].IsStructuralChanged);
    }

    /// <summary>Clears a selection that falls beyond the shortened document after deleting its last byte.</summary>
    [Fact]
    public async Task HexEditorDeletingSelectedLastBytePublishesValidSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-delete-last");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x20]);
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.SelectByte(1);
        editor.DeleteByteCommand.Execute(1);

        Assert.Equal(1, editor.ViewportSnapshot.DocumentLength);
        Assert.Null(editor.ViewportSnapshot.SelectedAddress);
        Assert.Null(editor.SelectedByteAddress);
        Assert.Equal((byte)0x10, Assert.Single(editor.ViewportSnapshot.Rows[0].Cells).PrimaryValue);
    }

    /// <summary>Connects multi-byte insertion, concise structural endpoints, and human-readable block causes.</summary>
    [Fact]
    public async Task HexEditorExplainsBatchInsertAndShowsOnlyStructuralBlockEndpoints()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-batch-insert");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(64));
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.RequestInsertBytesBeforeCommand.Execute(editor.ViewportSnapshot.Rows[0].Cells[1].Address);

        Assert.True(editor.IsInsertBytesPromptOpen);
        Assert.Equal("0x000001", editor.InsertTargetAddress);
        Assert.Equal(editor.Text.HexEditorInsertBytesBeforeTitle, editor.InsertBytesPromptTitle);
        editor.InsertByteCount = 3;
        editor.ConfirmInsertBytesCommand.Execute(null);

        Assert.False(editor.IsInsertBytesPromptOpen);
        Assert.Equal("0x43 bytes", editor.WorkingLengthLabel);
        HexEditorChangedBlockViewModel block = Assert.IsType<HexEditorChangedBlockViewModel>(
            Assert.Single(editor.ChangedBlockPage.Items));
        Assert.Contains("Inserted 3 byte(s) at 0x000001", block.ReasonTooltip, StringComparison.Ordinal);
        IReadOnlyList<HexViewportCell> cells = [.. editor.ViewportSnapshot.Rows.SelectMany(row => row.Cells)];
        HexViewportCell start = Assert.Single(cells, cell => cell.IsStructuralBoundaryStart);
        HexViewportCell end = Assert.Single(cells, cell => cell.IsStructuralBoundaryEnd);
        Assert.Equal(0, start.StructuralBlockIndex);
        Assert.Equal(0, end.StructuralBlockIndex);
        Assert.All(cells.Where(cell => cell.IsStructuralChanged), cell => Assert.Equal(0, cell.StructuralBlockIndex));
        Assert.False(cells[4].IsDataChanged);
        Assert.True(cells[4].IsStructuralChanged);
        Assert.False(cells[4].IsStructuralBoundary);

        editor.IsOriginalRowsVisible = true;
        Assert.Equal(5, editor.ViewportSnapshot.Rows.Count(row => row.HasComparison));

        editor.GoToChangedBlockEndCommand.Execute(block);
        Assert.Equal("0x000042", editor.SelectedByteAddress);
        editor.GoToChangedBlockStartCommand.Execute(block);
        Assert.Equal("0x000001", editor.SelectedByteAddress);
    }

    /// <summary>Publishes only the visible bytes changed by Undo or Redo for bounded visual feedback.</summary>
    [Fact]
    public async Task HexEditorPublishesVisibleUndoRedoFeedback()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-history-feedback");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(64));
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.SetByteToFfCommand.Execute(editor.ViewportSnapshot.Rows[0].Cells[2].Address);
        int initialVersion = editor.HistoryFeedbackVersion;

        editor.UndoCommand.Execute(null);

        Assert.Equal(initialVersion + 1, editor.HistoryFeedbackVersion);
        Assert.Equal([2L], editor.HistoryFeedbackAddresses);

        editor.RedoCommand.Execute(null);

        Assert.Equal(initialVersion + 2, editor.HistoryFeedbackVersion);
        Assert.Equal([2L], editor.HistoryFeedbackAddresses);
    }

    /// <summary>Expands the bounded renderer by logical rows while preserving a stable full-document scrollbar.</summary>
    [Fact]
    public async Task HexEditorViewportHeightExpandsOnlyTheBoundedPage()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-viewport-height");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(16 * 80));
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.SetViewportHeight(500);

        Assert.Equal(500, editor.HexViewportHeight);
        Assert.Equal(20, editor.VisibleRowCount);
        Assert.Equal(60, editor.DocumentScrollMaximum);
        Assert.Equal(20, editor.ViewportSnapshot.Rows.Count);

        editor.IsOriginalRowsVisible = true;
        Assert.Equal(20, editor.VisibleRowCount);
        Assert.Equal(60, editor.DocumentScrollMaximum);
        Assert.Equal(20, editor.ViewportSnapshot.Rows.Count);

        editor.RangeStartAddress = "0x000000";
        editor.RangeEndAddress = "0x000000";
        editor.RangeValue = "FF";
        editor.ApplyOverwriteRangeCommand.Execute(null);

        Assert.Equal(19, editor.VisibleRowCount);
        Assert.Equal(60, editor.DocumentScrollMaximum);
        Assert.Equal(19, editor.ViewportSnapshot.Rows.Count);
        Assert.Equal(20, editor.ViewportSnapshot.Rows.Sum(row => row.HasComparison ? 2 : 1));
    }

    /// <summary>Scopes save, undo, and redo shortcuts to the active raw utility page.</summary>
    [Fact]
    public async Task HexEditorStopsBackgroundWorkAndSaveShortcutOutsideItsPage()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-page-scope");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(16 * 40));
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();

        shell.ShowHexEditorCommand.Execute(null);
        await shell.HexEditorWorkspace.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        shell.HexEditorWorkspace.SetByteToFfCommand.Execute(shell.HexEditorWorkspace.ViewportSnapshot.Rows[0].Cells[0].Address);
        Assert.True(shell.RequestHexEditorSaveCommand.CanExecute(null));
        Assert.True(shell.RequestHexEditorUndoCommand.CanExecute(null));

        long address = shell.HexEditorWorkspace.ViewportSnapshot.Rows[0].Cells[1].Address;
        shell.HexEditorWorkspace.BeginByteEditCommand.Execute(address);
        Assert.True(shell.HexEditorWorkspace.IsInlineEditActive);
        Assert.False(shell.RequestHexEditorSaveCommand.CanExecute(null));
        Assert.False(shell.RequestHexEditorUndoCommand.CanExecute(null));
        Assert.False(shell.RequestHexEditorRedoCommand.CanExecute(null));
        shell.HexEditorWorkspace.CancelByteEditCommand.Execute(address);
        Assert.False(shell.HexEditorWorkspace.IsInlineEditActive);
        Assert.True(shell.RequestHexEditorUndoCommand.CanExecute(null));

        shell.HexEditorWorkspace.SetTextEntryFocused(true);
        Assert.False(shell.RequestHexEditorSaveCommand.CanExecute(null));
        Assert.False(shell.RequestHexEditorUndoCommand.CanExecute(null));
        Assert.False(shell.RequestHexEditorRedoCommand.CanExecute(null));
        shell.HexEditorWorkspace.SetTextEntryFocused(false);
        Assert.True(shell.RequestHexEditorUndoCommand.CanExecute(null));

        shell.RequestHexEditorUndoCommand.Execute(null);
        Assert.False(shell.HexEditorWorkspace.HasUnsavedChanges);
        Assert.True(shell.RequestHexEditorRedoCommand.CanExecute(null));

        shell.RequestHexEditorRedoCommand.Execute(null);
        Assert.True(shell.HexEditorWorkspace.HasUnsavedChanges);

        shell.ShowHomeCommand.Execute(null);

        Assert.False(shell.RequestHexEditorSaveCommand.CanExecute(null));
        Assert.False(shell.RequestHexEditorUndoCommand.CanExecute(null));
        Assert.False(shell.RequestHexEditorRedoCommand.CanExecute(null));
        shell.RequestHexEditorSaveCommand.Execute(null);
        Assert.False(shell.HexEditorWorkspace.IsSaveConfirmationOpen);
    }

    private static byte[] CreateHexPattern(int length)
    {
        return [.. Enumerable.Range(0, length).Select(index => (byte)(index % 251))];
    }

    private static string FormatTestAddress(long address)
    {
        return FormattableString.Invariant($"0x{address:X6}");
    }

    private static string CreateComparisonAscii(HexViewportRow row)
    {
        return new string([
            .. row.Cells.Select(cell => cell.ComparisonValue is >= 0x20 and <= 0x7E
                ? (char)cell.ComparisonValue.Value
                : ' '),
        ]);
    }
}
