using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class DesktopCaptureTests
{
    /// <summary>A broken symbolic destination requires overwrite before any host, window or target is created.</summary>
    [Fact]
    public void BrokenSymbolicDestinationExits64BeforeHostConstructionWithoutWrites()
    {
        using var workspace = TempWorkspace.Create("capture-broken-link");
        string capture = workspace.PathFor("capture.png");
        string target = workspace.PathFor("missing.png");
        _ = File.CreateSymbolicLink(capture, target);
        string[] arguments = ["--report=report.json", "--open-report", $"--capture={capture}"];
        int constructions = 0;

        AssertRefused(arguments, "destination exists; use --overwrite-capture.");
        int exit = DesktopApplication.Run(() =>
        {
            constructions++;
            throw new InvalidOperationException("Refused capture must not compose a host or create a window.");
        }, new LocalFileStore(), workspace.Root, arguments);

        Assert.Equal(64, exit);
        Assert.Equal(0, constructions);
        Assert.Equal(target, new FileInfo(capture).LinkTarget);
        Assert.False(File.Exists(target));
        Assert.False(Directory.Exists(target));
        Assert.Equal([capture], Directory.GetFileSystemEntries(workspace.Root));
    }

    /// <summary>A symlink parent or ancestor refuses capture before host composition or any staging file.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SymbolicDestinationParentExits64BeforeHostConstruction(bool ancestor)
    {
        using var workspace = TempWorkspace.Create("capture-symbolic-parent");
        string realParent = workspace.PathFor("real");
        string alias = workspace.PathFor("link");
        _ = Directory.CreateDirectory(Path.Combine(realParent, "child"));
        _ = Directory.CreateSymbolicLink(alias, realParent);
        string capture = Path.Combine(ancestor ? Path.Combine(alias, "child") : alias, "capture.png");
        int constructions = 0;

        int exit = DesktopApplication.Run(() =>
        {
            constructions++;
            throw new InvalidOperationException("Refused capture must not compose a host or create a window.");
        }, new LocalFileStore(), workspace.Root,
            ["--report=report.json", "--open-report", $"--capture={capture}", "--overwrite-capture"]);

        Assert.Equal(64, exit);
        Assert.Equal(0, constructions);
        Assert.False(File.Exists(capture));
        Assert.Empty(Directory.EnumerateFiles(workspace.Root, "*", SearchOption.AllDirectories));
    }
}
