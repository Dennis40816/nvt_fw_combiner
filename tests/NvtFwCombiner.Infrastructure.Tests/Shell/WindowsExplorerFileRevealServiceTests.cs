using System.Diagnostics;
using NvtFwCombiner.Infrastructure.Shell;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Shell;

/// <summary>Verifies the constrained Windows Explorer file-reveal adapter.</summary>
public sealed class WindowsExplorerFileRevealServiceTests
{
    /// <summary>The adapter uses only an existing absolute system Explorer and exact file argument.</summary>
    [Fact]
    public void StartInfoUsesTrustedAbsoluteExplorerPathAndExactFile()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-explorer-reveal");
        string windowsDirectory = workspace.PathFor("Windows");
        string explorerPath = workspace.Write("Windows/explorer.exe", [0x00]);
        string outputPath = workspace.Write("output/firmware with spaces.bin", [0x01]);

        ProcessStartInfo startInfo = Assert.IsType<ProcessStartInfo>(
            WindowsExplorerFileRevealService.TryCreateStartInfo(outputPath, windowsDirectory));

        Assert.Equal(explorerPath, startInfo.FileName);
        Assert.True(Path.IsPathFullyQualified(startInfo.FileName));
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(Path.GetDirectoryName(outputPath), startInfo.WorkingDirectory);
        Assert.Collection(
            startInfo.ArgumentList,
            argument => Assert.Equal("/select,", argument),
            argument => Assert.Equal(outputPath, argument));
    }

    /// <summary>Relative, missing target, and missing Explorer paths fail closed.</summary>
    [Fact]
    public void StartInfoRejectsUnresolvedPaths()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-explorer-reveal-invalid");
        string windowsDirectory = workspace.PathFor("Windows");
        _ = workspace.Write("Windows/explorer.exe", [0x00]);

        Assert.Null(WindowsExplorerFileRevealService.TryCreateStartInfo("firmware.bin", windowsDirectory));
        Assert.Null(WindowsExplorerFileRevealService.TryCreateStartInfo(
            workspace.PathFor("missing.bin"),
            windowsDirectory));
        Assert.Null(WindowsExplorerFileRevealService.TryCreateStartInfo(
            workspace.Write("output/firmware.bin", [0x01]),
            workspace.PathFor("missing-windows")));
    }
}
