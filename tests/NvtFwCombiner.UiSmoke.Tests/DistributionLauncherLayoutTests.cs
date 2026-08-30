using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.Threading;
using NvtFwCombiner.DistributionLauncher;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Measured geometry regressions for the standalone distribution Launcher.</summary>
[Collection(UiProcessWideObservationCollection.Name)]
public sealed class DistributionLauncherLayoutTests
{
    /// <summary>The approved compact pencil remains outside rather than overlaying the path field.</summary>
    [AvaloniaFact]
    public async Task SetupPathPencilIsCompactAndOutsideThePathField()
    {
        using var window = new LauncherWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        Border path = Assert.IsType<Border>(window.FindControl<Border>("InstallLocationField"));
        Button pencil = Assert.IsType<Button>(window.FindControl<Button>("EditLocationButton"));

        Assert.InRange(pencil.Bounds.Width, 33.5, 34.5);
        Assert.True(
            pencil.Bounds.Left >= path.Bounds.Right,
            $"Expected external pencil after path field, got field {path.Bounds} and pencil {pencil.Bounds}.");
    }
}
