using System.Globalization;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>UI-state coverage for the standalone profile-independent raw BIN utility.</summary>
public sealed partial class ShellViewModelTests
{
    /// <summary>Locks raw Hex Editor state to an isolated fixed-window document without IC or Replace state.</summary>
    [Fact]
    public async Task HexEditorLoadsOneMemoryDocumentAndFocusesMatchingRowAndColumn()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-load");
        byte[] source = CreateHexPattern(16 * 4_097);
        string sourcePath = workspace.Write("source.bin", source);
        MainWindowViewModel shell = ShellViewModelFactory.Create();

        shell.ShowHexEditorCommand.Execute(null);
        await shell.HexEditorWorkspace.LoadAsync(sourcePath, TestContext.Current.CancellationToken);

        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;
        Assert.True(shell.IsHexEditorVisible);
        Assert.False(shell.IsReplaceVisible);
        Assert.True(editor.HasDocument);
        Assert.Equal(4_097, editor.TotalRowCount);
        Assert.Equal(12, editor.ViewportRows.Count);
        Assert.Equal(4_085, editor.DocumentScrollMaximum);
        Assert.Equal("source.bin", editor.SourceName);

        HexEditorViewportRowViewModel row = editor.ViewportRows[0];
        editor.SelectByteCommand.Execute(row.Bytes[5]);

        Assert.True(row.IsSelected);
        Assert.True(row.Bytes[5].IsSelected);
        Assert.True(editor.ColumnHeaders[5].IsSelected);
        Assert.False(editor.ColumnHeaders[4].IsSelected);
        Assert.Equal(row.Bytes[5].Address, editor.RangeStartAddress);
        Assert.Equal(row.Bytes[5].Address, editor.RangeEndAddress);

        editor.MoveSelectionCommand.Execute(17);
        Assert.True(editor.ViewportRows[1].Bytes[6].IsSelected);
        Assert.True(editor.ColumnHeaders[6].IsSelected);
        Assert.Equal(editor.ViewportRows[1].Bytes[6].Address, editor.SelectedByteAddress);
    }

    /// <summary>Uses stable full-document scroll geometry while replacing only one bounded raw-BIN row window.</summary>
    [Fact]
    public async Task HexEditorUsesStableViewportAndExportsOnlyANewBin()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-progressive");
        byte[] source = CreateHexPattern(16 * 40);
        string sourcePath = workspace.Write("source.bin", source);
        string outputPath = workspace.PathFor("edited.bin");
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        Assert.Equal(40, editor.TotalRowCount);
        Assert.Equal(12, editor.ViewportRows.Count);
        Assert.Equal(28, editor.DocumentScrollMaximum);

        editor.SetViewportStartRowCommand.Execute(editor.TotalRowCount);
        Assert.Equal(28, editor.ViewportStartRow);
        Assert.Equal("0x0001C0", editor.ViewportRows[0].Address);
        editor.SetViewportStartRowCommand.Execute(0);

        HexEditorViewportRowViewModel firstRow = editor.ViewportRows[0];
        HexEditorByteCellViewModel target = firstRow.Bytes[1];
        editor.InsertZeroAfterCommand.Execute(target);
        editor.SetByteToFfCommand.Execute(editor.ViewportRows[0].Bytes[0]);
        firstRow = editor.ViewportRows[0];

        Assert.True(editor.CanSave);
        Assert.Equal(41, editor.TotalRowCount);
        Assert.Equal(29, editor.DocumentScrollMaximum);
        Assert.Equal(source[0].ToString("X2", CultureInfo.InvariantCulture), firstRow.Bytes[0].OriginalHex);
        Assert.Equal("FF", firstRow.Bytes[0].ValueHex);
        Assert.Equal("00", firstRow.Bytes[2].ValueHex);
        Assert.False(firstRow.Bytes[3].IsDataChanged);
        Assert.True(firstRow.Bytes[3].IsStructuralChanged);
        Assert.True(firstRow.Bytes[3].IsChanged);
        Assert.True(firstRow.Bytes[2].IsChanged);

        editor.RequestSaveCommand.Execute(null);
        Assert.True(editor.IsSaveConfirmationOpen);
        editor.CancelSaveCommand.Execute(null);
        await editor.SaveAsAsync(outputPath, TestContext.Current.CancellationToken);

        Assert.False(editor.IsSaveConfirmationOpen);
        Assert.Equal(source, File.ReadAllBytes(sourcePath));
        Assert.Equal((byte)0xFF, File.ReadAllBytes(outputPath)[0]);
        Assert.Equal(source.Length + 1, new FileInfo(outputPath).Length);
    }

    /// <summary>Keeps earlier rows visible when Go to moves the bounded window to a later address.</summary>
    [Fact]
    public async Task HexEditorGoToPreservesFourRowsOfEarlierContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-go-to");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(16 * 64));
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.ViewportAddress = "0x000200";
        editor.GoToCommand.Execute(null);

        Assert.Equal(28, editor.ViewportStartRow);
        Assert.Equal("0x0001C0", editor.ViewportRows[0].Address);
        Assert.Equal("0x000200", editor.ViewportRows[4].Address);
        Assert.True(editor.ViewportRows[4].IsSelected);
    }

    /// <summary>Finds the next ASCII occurrence in the existing memory document without reopening the BIN.</summary>
    [Fact]
    public async Task HexEditorFindAsciiSelectsAndFramesTheMatchedBytes()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-ascii-search");
        string sourcePath = workspace.Write("source.bin", "prefix NVT middle NVT"u8.ToArray());
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.AsciiSearchText = "NVT";
        editor.FindAsciiCommand.Execute(null);

        Assert.Equal("0x000007", editor.SelectedByteAddress);
        Assert.Equal("0x000007", editor.RangeStartAddress);
        Assert.Equal("0x000009", editor.RangeEndAddress);
        Assert.Equal("1/2", editor.AsciiSearchResultLabel);
        Assert.All(editor.ViewportRows[0].Bytes.Skip(7).Take(3), cell => Assert.True(cell.IsAsciiSearchMatch));
        Assert.Contains("0x000007", editor.EditorStatus, StringComparison.Ordinal);

        editor.FindAsciiCommand.Execute(null);
        Assert.Equal("0x000012", editor.SelectedByteAddress);
        Assert.Equal("2/2", editor.AsciiSearchResultLabel);
    }

    /// <summary>Labels a structural-edit reference row as mapped source data instead of a false same-address comparison.</summary>
    [Fact]
    public async Task HexEditorReferenceRowsExposeOriginalMappingAfterInsertion()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-original-map");
        string sourcePath = workspace.Write("source.bin", "ABCT"u8.ToArray());
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.InsertZeroAfterCommand.Execute(editor.ViewportRows[0].Bytes[1]);
        editor.IsOriginalRowsVisible = true;

        HexEditorViewportRowViewModel changedRow = editor.ViewportRows[0];
        Assert.True(changedRow.IsOriginalRowVisible);
        Assert.True(changedRow.OriginalBytes[2].IsReference);
        Assert.Equal("43", changedRow.OriginalBytes[2].OriginalHex);
        Assert.False(changedRow.OriginalBytes[2].IsDataChanged);
        Assert.True(changedRow.OriginalBytes[2].IsStructuralChanged);
        Assert.True(changedRow.OriginalBytes[2].IsChanged);
        Assert.Equal("54", changedRow.Bytes[4].ValueHex);
        Assert.Equal("--", changedRow.OriginalBytes[4].OriginalHex);
        Assert.False(changedRow.OriginalBytes[4].HasOriginalValue);
        Assert.Equal("ABCT ", changedRow.OriginalAscii);
    }

    /// <summary>Groups adjacent memory changes and lets the inspector frame each editable block without reloading the BIN.</summary>
    [Fact]
    public async Task HexEditorTracksAndCyclesContiguousChangedBlocks()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-changed-blocks");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(16 * 8));
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.RangeStartAddress = "0x000002";
        editor.RangeEndAddress = "0x000004";
        editor.RangeValue = "AA BB CC";
        editor.ApplyOverwriteRangeCommand.Execute(null);

        Assert.Equal(1, editor.ChangedBlockCount);
        Assert.True(editor.HasChangedBlocks);
        Assert.False(editor.HasNoChangedBlocks);
        HexEditorChangedBlockViewModel block = Assert.Single(editor.ChangedBlocks);
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
        Assert.Equal("EE", editor.ViewportRows[0].Bytes[6].ValueHex);
        Assert.Equal("EE", editor.ViewportRows[0].Bytes[7].ValueHex);
    }

    /// <summary>Keeps short overwrite local to Start while showing range validation in the Edit Region only.</summary>
    [Fact]
    public async Task HexEditorAllowsShortOverwriteAndKeepsEndOverflowAsEditFeedback()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-overwrite-boundary");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x20, 0x30]);
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.RangeStartAddress = "0x000000";
        editor.RangeEndAddress = "0x000002";
        editor.RangeValue = "A5";
        editor.ApplyRangeEditCommand.Execute(null);

        Assert.Equal(editor.Text.HexEditorOverwriteGuidance, editor.EditGuidance);
        Assert.Equal(editor.Text.HexEditorOverwriteGuidance, editor.EditNotice);
        Assert.False(editor.HasEditFeedback);
        Assert.Equal("A5", editor.ViewportRows[0].Bytes[0].ValueHex);
        Assert.Equal("20", editor.ViewportRows[0].Bytes[1].ValueHex);
        Assert.Equal("30", editor.ViewportRows[0].Bytes[2].ValueHex);
        Assert.Contains("In memory", editor.EditorStatus, StringComparison.Ordinal);

        editor.RangeValue = "AA BB CC DD";
        editor.ApplyRangeEditCommand.Execute(null);

        Assert.True(editor.HasEditFeedback);
        Assert.Equal(editor.Text.HexEditorInputExceedsRangeDetail, editor.EditFeedback);
        Assert.Equal(editor.Text.HexEditorInputExceedsRangeDetail, editor.EditNotice);
        Assert.Equal("A5", editor.ViewportRows[0].Bytes[0].ValueHex);
        Assert.Equal("20", editor.ViewportRows[0].Bytes[1].ValueHex);
        Assert.Equal("30", editor.ViewportRows[0].Bytes[2].ValueHex);

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
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.DeleteByteCommand.Execute(editor.ViewportRows[0].Bytes[1]);

        HexEditorChangedBlockViewModel block = Assert.Single(editor.ChangedBlocks);
        Assert.Equal("0x000001", block.StartAddress);
        Assert.Equal("0x00003E", block.EndAddress);
        Assert.Contains("Deleted 1 byte(s) at 0x000001", block.ReasonTooltip, StringComparison.Ordinal);
        Assert.False(block.HasDataChanges);
        Assert.True(block.HasStructuralChanges);
        Assert.False(editor.ViewportRows[0].Bytes[1].IsDataChanged);
        Assert.True(editor.ViewportRows[0].Bytes[1].IsStructuralChanged);
    }

    /// <summary>Connects multi-byte insertion, concise structural endpoints, and human-readable block causes.</summary>
    [Fact]
    public async Task HexEditorExplainsBatchInsertAndShowsOnlyStructuralBlockEndpoints()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-batch-insert");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(64));
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.RequestInsertBytesBeforeCommand.Execute(editor.ViewportRows[0].Bytes[1]);

        Assert.True(editor.IsInsertBytesPromptOpen);
        Assert.Equal("0x000001", editor.InsertTargetAddress);
        Assert.Equal(editor.Text.HexEditorInsertBytesBeforeTitle, editor.InsertBytesPromptTitle);
        editor.InsertByteCount = 3;
        editor.ConfirmInsertBytesCommand.Execute(null);

        Assert.False(editor.IsInsertBytesPromptOpen);
        Assert.Equal("0x43 bytes", editor.WorkingLengthLabel);
        HexEditorChangedBlockViewModel block = Assert.Single(editor.ChangedBlocks);
        Assert.Contains("Inserted 3 byte(s) at 0x000001", block.ReasonTooltip, StringComparison.Ordinal);
        IReadOnlyList<HexEditorByteCellViewModel> cells = [.. editor.ViewportRows.SelectMany(row => row.Bytes)];
        HexEditorByteCellViewModel start = Assert.Single(cells, cell => cell.IsStructuralBoundaryStart);
        HexEditorByteCellViewModel end = Assert.Single(cells, cell => cell.IsStructuralBoundaryEnd);
        Assert.Equal("01", start.StructuralBoundaryLabel);
        Assert.Equal("01", end.StructuralBoundaryLabel);
        Assert.All(cells.Where(cell => cell.IsStructuralChanged), cell => Assert.Equal(0, cell.StructuralBlockIndex));
        Assert.False(cells[4].IsDataChanged);
        Assert.True(cells[4].IsStructuralChanged);
        Assert.False(cells[4].IsStructuralBoundary);

        editor.IsOriginalRowsVisible = true;
        Assert.Equal(5, editor.ViewportRows.Count(row => row.IsOriginalRowVisible));

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
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.SetByteToFfCommand.Execute(editor.ViewportRows[0].Bytes[2]);
        int initialVersion = editor.HistoryFeedbackVersion;

        editor.UndoCommand.Execute(null);

        Assert.Equal(initialVersion + 1, editor.HistoryFeedbackVersion);
        Assert.Equal(["0x000002"], editor.HistoryFeedbackAddresses);

        editor.RedoCommand.Execute(null);

        Assert.Equal(initialVersion + 2, editor.HistoryFeedbackVersion);
        Assert.Equal(["0x000002"], editor.HistoryFeedbackAddresses);
    }

    /// <summary>Expands the bounded renderer by logical rows while preserving a stable full-document scrollbar.</summary>
    [Fact]
    public async Task HexEditorViewportHeightExpandsOnlyTheBoundedPage()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-viewport-height");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(16 * 80));
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.SetViewportHeight(500);

        Assert.Equal(500, editor.HexViewportHeight);
        Assert.Equal(20, editor.VisibleRowCount);
        Assert.Equal(60, editor.DocumentScrollMaximum);
        Assert.Equal(20, editor.ViewportRows.Count);

        editor.IsOriginalRowsVisible = true;
        Assert.Equal(20, editor.VisibleRowCount);
        Assert.Equal(60, editor.DocumentScrollMaximum);
        Assert.Equal(20, editor.ViewportRows.Count);

        editor.RangeStartAddress = "0x000000";
        editor.RangeEndAddress = "0x000000";
        editor.RangeValue = "FF";
        editor.ApplyOverwriteRangeCommand.Execute(null);

        Assert.Equal(19, editor.VisibleRowCount);
        Assert.Equal(60, editor.DocumentScrollMaximum);
        Assert.Equal(19, editor.ViewportRows.Count);
        Assert.Equal(20, editor.ViewportRows.Sum(row => row.IsOriginalRowVisible ? 2 : 1));
    }

    /// <summary>Scopes save, undo, and redo shortcuts to the active raw utility page.</summary>
    [Fact]
    public async Task HexEditorStopsBackgroundWorkAndSaveShortcutOutsideItsPage()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-page-scope");
        string sourcePath = workspace.Write("source.bin", CreateHexPattern(16 * 40));
        MainWindowViewModel shell = ShellViewModelFactory.Create();

        shell.ShowHexEditorCommand.Execute(null);
        await shell.HexEditorWorkspace.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        shell.HexEditorWorkspace.SetByteToFfCommand.Execute(shell.HexEditorWorkspace.ViewportRows[0].Bytes[0]);
        Assert.True(shell.RequestHexEditorSaveCommand.CanExecute(null));
        Assert.True(shell.RequestHexEditorUndoCommand.CanExecute(null));

        HexEditorByteCellViewModel cell = shell.HexEditorWorkspace.ViewportRows[0].Bytes[1];
        shell.HexEditorWorkspace.BeginByteEditCommand.Execute(cell);
        Assert.True(shell.HexEditorWorkspace.IsInlineEditActive);
        Assert.False(shell.RequestHexEditorSaveCommand.CanExecute(null));
        Assert.False(shell.RequestHexEditorUndoCommand.CanExecute(null));
        Assert.False(shell.RequestHexEditorRedoCommand.CanExecute(null));
        shell.HexEditorWorkspace.CancelByteEditCommand.Execute(cell);
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
}
