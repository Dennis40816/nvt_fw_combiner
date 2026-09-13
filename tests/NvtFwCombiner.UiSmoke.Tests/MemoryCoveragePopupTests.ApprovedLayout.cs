using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MemoryCoveragePopupTests
{
    /// <summary>Contiguous slices meet without an inset or a surface-colored stroke, even on hover.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApprovedLocalSlicesHaveNoWhiteSeams(bool dark)
    {
        Window window = CreateWindow(620, dark, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        bar.ShowLabels = true;
        bar.ReducedMotion = true;
        Render();
        try
        {
            Assert.True(MainTarget(bar, 1).Focus(NavigationMethod.Tab));
            Render();
            Border local = Assert.IsType<Border>(FindNamed<Border>(window, "MemoryLocalView"));
            ProportionalStackPanel strip = LocalStrip(local);
            foreach (Control child in strip.Children)
            {
                Border slice = Assert.IsType<Border>(child);
                Assert.Equal(default, slice.BorderThickness);
                Assert.DoesNotContain(slice.GetVisualDescendants().OfType<Border>(), border =>
                    border.BorderThickness != default && border.IsEffectivelyVisible);
            }
            for (int index = 1; index < strip.Children.Count; index++)
            {
                Assert.InRange(Math.Abs(strip.Children[index].Bounds.Left - strip.Children[index - 1].Bounds.Right), 0, 1);
            }
            Border target = Assert.IsType<Border>(strip.Children[5]);
            window.MouseMove(BoundsInWindow(target, window).Center);
            Render();
            Assert.DoesNotContain(target.GetVisualDescendants().OfType<Border>(), border =>
                border.BorderThickness != default && border.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    /// <summary>Pointer focus alone cannot open a card or leave its color slice highlighted.</summary>
    [AvaloniaFact]
    public void PointerFocusDoesNotActivateMemoryWithoutHover()
    {
        Window window = CreateWindow(388, false, MemoryCoverageBarProjectionTests.Example(), out MemoryCoverageBar bar);
        bar.ReducedMotion = true;
        try
        {
            Control slice = MainTarget(bar, 0);
            Assert.True(slice.Focus(NavigationMethod.Pointer));
            Render();
            AssertNoOverlay(window);
            Assert.False(Assert.IsType<MemoryCoverageSegmentViewModel>(slice.DataContext).Interaction.IsActive);
            Assert.True(MainTarget(bar, 1).Focus(NavigationMethod.Pointer));
            Render();
            AssertNoOverlay(window);
        }
        finally { window.Close(); }
    }
}
