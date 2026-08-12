using System.Diagnostics;
using Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class UiPerformanceObservationTests
{
    /// <summary>
    /// Proves the source-neutral viewport remains bounded and improves the released
    /// 10,175,656-byte scroll, 3,360,000-byte selection, and 400,000-byte hover
    /// allocation baselines.
    /// </summary>
    [Fact]
    public async Task HexViewportRefactorBeatsReleasedAllocationBaseline()
    {
        const int documentLength = 1024 * 1024;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-hex-viewport-baseline");
        string sourcePath = workspace.Write("baseline.bin", CreateHexPattern(documentLength));
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        HexEditorWorkspaceViewModel editor = shell.HexEditorWorkspace;
        await editor.LoadAsync(sourcePath, TestContext.Current.CancellationToken);

        Assert.Equal(12, editor.ViewportSnapshot.Rows.Count);
        Assert.Equal(16, editor.ViewportSnapshot.Rows[0].Cells.Count);
        Assert.Equal(192, editor.ViewportSnapshot.Rows.Sum(row => row.Cells.Count));
        Assert.All(editor.ViewportSnapshot.Rows, row => Assert.InRange(row.Cells.Count, 1, 16));

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

        long selectionAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long selectionStarted = Stopwatch.GetTimestamp();
        for (int iteration = 0; iteration < 10_000; iteration++)
        {
            editor.SelectByte(iteration % 2);
        }

        TimeSpan selectionElapsed = Stopwatch.GetElapsedTime(selectionStarted);
        long selectionAllocated = GC.GetAllocatedBytesForCurrentThread() - selectionAllocatedBefore;

        var viewport = new HexViewportControl
        {
            Snapshot = editor.ViewportSnapshot,
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
            $"HEX_VIEWPORT_REFACTORED bytes={documentLength}; rows={editor.ViewportSnapshot.Rows.Count}; " +
            $"scrollIterations=128; scrollMs={scrollElapsed.TotalMilliseconds:F3}; " +
            $"scrollAllocated={scrollAllocated}; selectionIterations=10000; " +
            $"selectionMs={selectionElapsed.TotalMilliseconds:F3}; selectionAllocated={selectionAllocated}; " +
            $"hoverIterations=10000; " +
            $"hoverMs={hoverElapsed.TotalMilliseconds:F3}; hoverAllocated={hoverAllocated}");
        Assert.InRange(scrollAllocated, 0, 10_150_000);
        Assert.InRange(selectionAllocated, 0, 1_500_000);
        Assert.InRange(hoverAllocated, 0, 350_000);
    }
}
