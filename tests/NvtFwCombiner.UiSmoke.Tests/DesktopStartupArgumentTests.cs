using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Invalid desktop arguments terminate before composing the presentation host.</summary>
public sealed class DesktopStartupArgumentTests
{
    /// <summary>Malformed UI options never run the host factory.</summary>
    [Fact]
    public void InvalidUiArgumentsFailBeforeHostConstruction()
    {
        using var workspace = TempWorkspace.Create("invalid-desktop-arguments");
        int hostConstructions = 0;
        string[][] cases =
        [
            ["--page"],
            ["--page", "--open-report"],
            ["--report", "\0"],
            ["--workflow", "standard-merge", "--ic", "NT51926", "--ic-num", "single", "--dp", "\0", "--tp", "input.bin"],
        ];
        foreach (string[] arguments in cases)
        {
            int exitCode = DesktopApplication.Run(
                () =>
                {
                    hostConstructions++;
                    throw new InvalidOperationException("Invalid options must not create the host.");
                },
                new LocalFileStore(),
                workspace.Root,
                arguments);

            Assert.Equal(64, exitCode);
        }
        Assert.Equal(0, hostConstructions);
    }
}
