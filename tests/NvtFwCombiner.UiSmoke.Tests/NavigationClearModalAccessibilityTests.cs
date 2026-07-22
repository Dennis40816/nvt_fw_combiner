using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <inheritdoc/>
public sealed class NavigationClearModalAccessibilityTests
{
    /// <summary>The confirmation keeps keyboard focus inside the modal and exposes a predictable cancel path.</summary>
    [Fact]
    public void NavigationClearConfirmationProvidesKeyboardIsolationAndEscapeCancel()
    {
        string xaml = File.ReadAllText(
            RepositoryPaths.FromRepositoryRoot(
                "src",
                "NvtFwCombiner.Presentation.Avalonia",
                "Views",
                "NavigationClearConfirmationModal.axaml"));

        Assert.Contains("KeyboardNavigation.TabNavigation=\"Cycle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"NavigationClearConfirmationModal_OnKeyDown\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CancelButton\"", xaml, StringComparison.Ordinal);
    }
}
