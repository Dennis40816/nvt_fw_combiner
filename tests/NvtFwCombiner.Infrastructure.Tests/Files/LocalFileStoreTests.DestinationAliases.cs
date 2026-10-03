using System.Runtime.InteropServices;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

public sealed partial class LocalFileStoreTests
{
    /// <summary>A broken symbolic link occupies the destination without creating its target or staging files.</summary>
    [Fact]
    public async Task InspectDestinationReportsBrokenSymbolicLinkAsExistingWithoutWrites()
    {
        using var workspace = TempWorkspace.Create("destination-broken-link");
        string destination = workspace.PathFor("capture.png");
        string target = workspace.PathFor("missing.png");
        _ = File.CreateSymbolicLink(destination, target);

        LocalFileDestinationInfo info = await new LocalFileStore().InspectDestinationAsync(
            destination, TestContext.Current.CancellationToken);

        Assert.Equal(new LocalFileDestinationInfo(true, false, true, true), info);
        Assert.Equal(target, new FileInfo(destination).LinkTarget);
        Assert.False(File.Exists(target));
        Assert.False(Directory.Exists(target));
        Assert.Equal([destination], Directory.GetFileSystemEntries(workspace.Root));
    }

    /// <summary>Only ENOTDIR and ELOOP open failures identify a non-exact Unix directory path.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnixDirectoryInspectionRefusesNotDirectoryAndSymbolicLinkLoop(bool symbolicLinkLoop)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }
        using var workspace = TempWorkspace.Create("destination-unix-open-alias");
        string path = workspace.PathFor("parent");
        if (symbolicLinkLoop)
        {
            _ = Directory.CreateSymbolicLink(path, path);
            path = Path.Combine(path, "child");
        }
        else
        {
            _ = workspace.Write("parent", [1, 2, 3]);
        }

        int flags = OperatingSystem.IsLinux() ? 0x00010000 | 0x00020000 : 0x00100000 | 0x00000100;
        int descriptor = UnixOpenInspectionDirectory(path, flags, 0);
        int error = Marshal.GetLastPInvokeError();
        Assert.Equal(-1, descriptor);
        Assert.Equal(symbolicLinkLoop ? (OperatingSystem.IsLinux() ? 40 : 62) : 20, error);
        Assert.False(UnixAtomicFileWriteScope.HasExactDirectoryPath(path));
        _ = Assert.Throws<IOException>(() => UnixAtomicFileWriteScope.Open(Path.Combine(path, "capture.png")));
        _ = Assert.Single(Directory.EnumerateFileSystemEntries(workspace.Root));
    }

    /// <summary>Both direct and ancestor symlinks have a non-exact parent before any atomic staging.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InspectDestinationRefusesSymbolicParentAndAncestorWithoutWrites(bool ancestor)
    {
        using var workspace = TempWorkspace.Create("destination-parent-alias");
        string realParent = workspace.PathFor("real");
        string alias = workspace.PathFor("link");
        _ = Directory.CreateDirectory(Path.Combine(realParent, "child"));
        _ = Directory.CreateSymbolicLink(alias, realParent);
        string parent = ancestor ? Path.Combine(alias, "child") : alias;
        string realDestination = Path.Combine(ancestor ? Path.Combine(realParent, "child") : realParent, "capture.png");
        var store = new LocalFileStore();
        CancellationToken token = TestContext.Current.CancellationToken;

        Assert.Equal(new LocalFileDestinationInfo(true, false, false, false),
            await store.InspectDestinationAsync(Path.Combine(parent, "capture.png"), token));
        Assert.Equal(new LocalFileDestinationInfo(true, false, false, true),
            await store.InspectDestinationAsync(realDestination, token));
        Assert.Empty(Directory.EnumerateFiles(workspace.Root, "*", SearchOption.AllDirectories));
    }

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int UnixOpenInspectionDirectory(string path, int flags, uint mode);
}
