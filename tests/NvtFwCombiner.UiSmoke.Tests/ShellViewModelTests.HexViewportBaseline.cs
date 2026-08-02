using System.Diagnostics;
using Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>
    /// Records the released pre-extraction renderer/window cost so #191 can prove that the
    /// source-neutral viewport remains bounded and does not regress hot navigation or hover.
    /// </summary>
    [Fact]
    public async Task HexEditorReleasedViewportBaselineIsBounded()
    {
        const int documentLength = 1024 * 1024;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-hex-viewport-baseline");
        string sourcePath = workspace.Write("baseline.bin", CreateHexPattern(documentLength));
        MainWindowViewModel shell = ShellViewModelFactory.Create();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;
        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);

        Assert.Equal(12, editor.ViewportRows.Count);
        Assert.Equal(16, editor.ViewportRows[0].Bytes.Count);
        Assert.Equal(192, editor.ViewportRows.Sum(row => row.Bytes.Count));
        Assert.All(editor.ViewportRows, row => Assert.InRange(row.Bytes.Count, 1, 16));

        int lastStartRow = editor.DocumentScrollMaximum;
        editor.SetViewportStartRowCommand.Execute(lastStartRow);
        editor.SetViewportStartRowCommand.Execute(0);
        long scrollAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long scrollStarted = Stopwatch.GetTimestamp();
        for (int iteration = 0; iteration < 128; iteration++)
        {
            editor.SetViewportStartRowCommand.Execute(iteration % 2 == 0 ? lastStartRow : 0);
        }

        TimeSpan scrollElapsed = Stopwatch.GetElapsedTime(scrollStarted);
        long scrollAllocated = GC.GetAllocatedBytesForCurrentThread() - scrollAllocatedBefore;

        var viewport = new HexEditorViewportControl
        {
            DataContext = editor,
        };
        viewport.Measure(new Size(1080, 300));
        viewport.Arrange(new Rect(0, 0, 1080, 300));
        long hoverAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long hoverStarted = Stopwatch.GetTimestamp();
        for (int iteration = 0; iteration < 10_000; iteration++)
        {
            viewport.UpdateHoveredCell(iteration % 2 == 0
                ? new Point(117, 1)
                : new Point(147, 1));
        }

        TimeSpan hoverElapsed = Stopwatch.GetElapsedTime(hoverStarted);
        long hoverAllocated = GC.GetAllocatedBytesForCurrentThread() - hoverAllocatedBefore;

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"HEX_VIEWPORT_RELEASED_BASELINE bytes={documentLength}; rows={editor.ViewportRows.Count}; " +
            $"scrollIterations=128; scrollMs={scrollElapsed.TotalMilliseconds:F3}; " +
            $"scrollAllocated={scrollAllocated}; hoverIterations=10000; " +
            $"hoverMs={hoverElapsed.TotalMilliseconds:F3}; hoverAllocated={hoverAllocated}");
        Assert.InRange(scrollAllocated, 0, 64L * 1024 * 1024);
        Assert.InRange(hoverAllocated, 0, 512L * 1024);
    }
}
