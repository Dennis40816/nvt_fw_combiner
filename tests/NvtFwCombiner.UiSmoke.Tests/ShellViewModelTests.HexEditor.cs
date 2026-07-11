using System.Globalization;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>UI-state coverage for the standalone profile-independent raw BIN utility.</summary>
public sealed partial class ShellViewModelTests
{
    /// <summary>Locks raw Hex Editor state to an isolated in-memory document without IC or Replace state.</summary>
    [Fact]
    public async Task HexEditorLoadsOneMemoryDocumentAndFocusesMatchingRowAndColumn()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-load");
        byte[] source = CreateHexPattern(16 * 40);
        string sourcePath = workspace.Write("source.bin", source);
        MainWindowViewModel shell = ShellViewModelFactory.Create();

        shell.ShowHexEditorCommand.Execute(null);
        await shell.HexEditorWorkspace.LoadAsync(sourcePath, TestContext.Current.CancellationToken);

        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;
        Assert.True(shell.IsHexEditorVisible);
        Assert.False(shell.IsReplaceVisible);
        Assert.True(editor.HasDocument);
        Assert.Equal(32, editor.ViewportRows.Count);
        Assert.True(editor.HasMoreRows);
        Assert.Equal("source.bin", editor.SourceName);

        HexEditorViewportRowViewModel row = editor.ViewportRows[0];
        editor.SelectByteCommand.Execute(row.Bytes[5]);

        Assert.True(row.IsSelected);
        Assert.True(row.Bytes[5].IsSelected);
        Assert.True(editor.ColumnHeaders[5].IsSelected);
        Assert.False(editor.ColumnHeaders[4].IsSelected);
        Assert.Equal(row.Bytes[5].Address, editor.RangeStartAddress);
        Assert.Equal(row.Bytes[5].Address, editor.RangeEndAddress);
    }

    /// <summary>Progressive rendering appends all later rows without rereading the selected source file.</summary>
    [Fact]
    public async Task HexEditorProgressivelyAppendsRowsAndExportsOnlyANewBin()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-raw-hex-progressive");
        byte[] source = CreateHexPattern(16 * 40);
        string sourcePath = workspace.Write("source.bin", source);
        string outputPath = workspace.PathFor("edited.bin");
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;

        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);
        editor.LoadNextPageCommand.Execute(null);
        Assert.Equal(40, editor.ViewportRows.Count);
        Assert.False(editor.HasMoreRows);

        HexEditorViewportRowViewModel firstRow = editor.ViewportRows[0];
        HexEditorByteCellViewModel target = firstRow.Bytes[1];
        editor.InsertZeroAfterCommand.Execute(target);
        editor.SetByteToFfCommand.Execute(editor.ViewportRows[0].Bytes[0]);
        firstRow = editor.ViewportRows[0];

        Assert.True(editor.CanSave);
        Assert.Equal(source[0].ToString("X2", CultureInfo.InvariantCulture), firstRow.Bytes[0].OriginalHex);
        Assert.Equal("FF", firstRow.Bytes[0].ValueHex);
        Assert.Equal("00", firstRow.Bytes[2].ValueHex);
        Assert.False(firstRow.Bytes[3].IsChanged);
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

    /// <summary>Limits the global save shortcut and progressive work to the active raw utility page.</summary>
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

        shell.ShowHomeCommand.Execute(null);

        Assert.False(shell.HexEditorWorkspace.IsPageActive);
        Assert.False(shell.RequestHexEditorSaveCommand.CanExecute(null));
        shell.RequestHexEditorSaveCommand.Execute(null);
        Assert.False(shell.HexEditorWorkspace.IsSaveConfirmationOpen);
    }

    private static byte[] CreateHexPattern(int length)
    {
        return [.. Enumerable.Range(0, length).Select(index => (byte)(index % 251))];
    }
}
