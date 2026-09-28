using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Recoverable UI arguments still reach the presentation host and report surface.</summary>
public sealed class DesktopStartupArgumentTests
{
    /// <summary>UI parse issues do not suppress startup before the report stage can display them.</summary>
    [Theory]
    [InlineData("--page", "invalid")]
    [InlineData("--page", "home", "--page", "merge")]
    [InlineData("--page")]
    [InlineData("--report", "\0")]
    public void RecoverableUiArgumentsReachHostConstruction(params string[] arguments)
    {
        using var workspace = TempWorkspace.Create("invalid-desktop-arguments");
        int hostConstructions = 0;
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            DesktopApplication.Run(
                () =>
                {
                    hostConstructions++;
                    throw new InvalidOperationException("Host factory reached.");
                },
                new LocalFileStore(),
                workspace.Root,
                arguments));

        Assert.Equal("Host factory reached.", failure.Message);
        Assert.Equal(1, hostConstructions);
    }

    /// <summary>Repeated page selection is reported by the same UI startup issue path.</summary>
    [Fact]
    public void DuplicatePageSelectionProducesStartupIssue()
    {
        UiLaunchOptions options = UiLaunchOptions.Parse(["--page", "home", "--page", "merge"]);

        Assert.Contains("Duplicate option '--page'.", options.Issues);
    }
}
