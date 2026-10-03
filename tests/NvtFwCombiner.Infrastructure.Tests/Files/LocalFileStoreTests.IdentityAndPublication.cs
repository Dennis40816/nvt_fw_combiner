using System.ComponentModel;
using System.Runtime.InteropServices;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

public sealed partial class LocalFileStoreTests
{
    /// <summary>A Unix native open failure is not reported as a different directory spelling.</summary>
    [Fact]
    public void UnixDirectoryInspectionPropagatesOpenFailures()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        _ = Assert.Throws<IOException>(() => UnixAtomicFileWriteScope.HasExactDirectoryPath(workspace.PathFor("missing")));
    }

    /// <summary>Relative, dot-component and Windows case spellings identify the same existing file.</summary>
    [Fact]
    public async Task IdentityRecognizesDifferentSpellings()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("identity.json", [1, 2, 3]);
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, path);
        var store = new LocalFileStore();
        CancellationToken token = TestContext.Current.CancellationToken;

        Assert.True(await store.RefersToSameFileAsync(path, relative, token));
        Assert.True(await store.RefersToSameFileAsync(path, Path.Combine(workspace.Root, ".", "identity.json"), token));
        if (OperatingSystem.IsWindows())
        {
            Assert.True(await store.RefersToSameFileAsync(path, path.ToUpperInvariant(), token));
        }
    }

    /// <summary>Equal content does not make distinct files identical; absent paths are never identical.</summary>
    [Fact]
    public async Task IdentityRejectsDifferentFilesAndMissingPaths()
    {
        using var workspace = TempWorkspace.Create();
        string first = workspace.Write("first.json", [1, 2, 3]);
        string second = workspace.Write("second.json", [1, 2, 3]);
        string missing = workspace.PathFor("missing.json");
        var store = new LocalFileStore();
        CancellationToken token = TestContext.Current.CancellationToken;

        Assert.False(await store.RefersToSameFileAsync(first, second, token));
        Assert.False(await store.RefersToSameFileAsync(first, missing, token));
        Assert.False(await store.RefersToSameFileAsync(missing, first, token));
        Assert.False(await store.RefersToSameFileAsync(missing, missing, token));
    }

    /// <summary>Physical identity detects a differently named hard link on supported filesystems.</summary>
    [Fact]
    public async Task IdentityRecognizesHardLinks()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("identity.json", [1, 2, 3]);
        string alias = workspace.PathFor("alias.png");
        bool linked = OperatingSystem.IsWindows()
            ? WindowsCreateHardLink(alias, path, 0) != 0 : UnixLink(path, alias) == 0;
        int error = Marshal.GetLastPInvokeError();
        Assert.True(linked, new Win32Exception(error).Message);

        Assert.True(await new LocalFileStore().RefersToSameFileAsync(path, alias, TestContext.Current.CancellationToken));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    /// <summary>Create-only publication writes complete bytes and removes its staging artifacts.</summary>
    [Fact]
    public async Task CreateNewPublishesAbsentDestinationWithoutStagingArtifacts()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("capture.png");
        byte[] bytes = [1, 2, 3, 4];

        await new LocalFileStore().WriteAsync(path, bytes, LocalFileWriteMode.CreateNew, TestContext.Current.CancellationToken);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(workspace.Root));
    }

    /// <summary>An existing destination survives create-only failure byte for byte without staging artifacts.</summary>
    [Fact]
    public async Task CreateNewPreservesExistingDestinationAndCleansFailedStaging()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("capture.png", [9, 8, 7]);
        var store = new LocalFileStore();
        byte[] attempted = [1, 2, 3, 4];

        _ = await Assert.ThrowsAsync<IOException>(() =>
            store.WriteAsync(path, attempted, LocalFileWriteMode.CreateNew, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal([9, 8, 7], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(workspace.Root));
    }

    /// <summary>Cancellation of create-only publication leaves no destination or staging file.</summary>
    [Fact]
    public async Task CreateNewCancellationLeavesNoFiles()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("capture.png");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        byte[] bytes = [1, 2, 3];

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new LocalFileStore().WriteAsync(path, bytes, LocalFileWriteMode.CreateNew, cancellation.Token).AsTask());

        Assert.Empty(Directory.GetFiles(workspace.Root));
    }

    /// <summary>The existing write contract and explicit replacement mode keep atomic overwrite behavior.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementStillPublishesCompleteBytes(bool explicitMode)
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("capture.png", [9, 8, 7]);
        var store = new LocalFileStore();
        byte[] bytes = [1, 2, 3, 4];
        await (explicitMode
            ? store.WriteAsync(path, bytes, LocalFileWriteMode.ReplaceExisting, TestContext.Current.CancellationToken)
            : store.WriteAsync(path, bytes, TestContext.Current.CancellationToken));

        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(workspace.Root));
    }

    /// <summary>The legacy write path still creates missing parents and replaces existing bytes.</summary>
    [Fact]
    public async Task LegacyWriteCreatesParentAndReplacesDestination()
    {
        using var workspace = TempWorkspace.Create();
        string path = Path.Combine(workspace.Root, "created", "capture.png");
        var store = new LocalFileStore();

        await store.WriteAsync(path, new byte[] { 1, 2 }, TestContext.Current.CancellationToken);
        await store.WriteAsync(path, new byte[] { 3, 4 }, TestContext.Current.CancellationToken);

        Assert.Equal([3, 4], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    /// <summary>Capture writes cannot make a missing parent, including one removed after inspection.</summary>
    [Fact]
    public async Task RequireExistingParentRejectsMissingOrRemovedParentWithoutArtifacts()
    {
        using var workspace = TempWorkspace.Create();
        string parent = Path.Combine(workspace.Root, "capture-parent");
        string path = Path.Combine(parent, "capture.png");
        var store = new LocalFileStore();
        var options = new LocalFileWriteOptions(LocalFileWriteMode.CreateNew, true);
        CancellationToken token = TestContext.Current.CancellationToken;

        LocalFileDestinationInfo missing = await store.InspectDestinationAsync(path, token);
        Assert.Equal(new LocalFileDestinationInfo(false, false, false, false), missing);
        _ = await Assert.ThrowsAsync<DirectoryNotFoundException>(() => store.WriteAsync(path, new byte[] { 1 }, options, token).AsTask());
        Assert.False(Directory.Exists(parent));

        _ = Directory.CreateDirectory(parent);
        Assert.Equal(new LocalFileDestinationInfo(true, false, false, true), await store.InspectDestinationAsync(path, token));
        Directory.Delete(parent);
        _ = await Assert.ThrowsAsync<DirectoryNotFoundException>(() => store.WriteAsync(path, new byte[] { 1 }, options, token).AsTask());
        Assert.Empty(Directory.GetFileSystemEntries(workspace.Root));
    }

    /// <summary>Explicit capture publication succeeds in an existing parent and keeps create-only protection.</summary>
    [Fact]
    public async Task RequireExistingParentPublishesOnlyAnAbsentDestination()
    {
        using var workspace = TempWorkspace.Create();
        string path = workspace.PathFor("capture.png");
        var store = new LocalFileStore();
        var options = new LocalFileWriteOptions(LocalFileWriteMode.CreateNew, true);
        CancellationToken token = TestContext.Current.CancellationToken;

        await store.WriteAsync(path, new byte[] { 1, 2, 3 }, options, token);
        _ = await Assert.ThrowsAsync<IOException>(() =>
            store.WriteAsync(path, new byte[] { 4, 5 }, options, token).AsTask());

        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(path, token));
        Assert.Equal([path], Directory.GetFiles(workspace.Root));
    }

    /// <summary>Destination inspection identifies an occupied path without creating an anchor or staging file.</summary>
    [Fact]
    public async Task InspectDestinationReportsFileAndDirectoryWithoutMutation()
    {
        using var workspace = TempWorkspace.Create();
        string file = workspace.Write("capture.png", [9, 8]);
        string directory = Path.Combine(workspace.Root, "directory.png");
        _ = Directory.CreateDirectory(directory);
        var store = new LocalFileStore();
        CancellationToken token = TestContext.Current.CancellationToken;

        Assert.Equal(new LocalFileDestinationInfo(true, false, true, true), await store.InspectDestinationAsync(file, token));
        Assert.Equal(new LocalFileDestinationInfo(true, true, false, true), await store.InspectDestinationAsync(directory, token));
        Assert.Equal([file, directory], Directory.GetFileSystemEntries(workspace.Root).OrderBy(path => path, StringComparer.Ordinal));
        Assert.Equal([9, 8], await File.ReadAllBytesAsync(file, token));
    }

    /// <summary>A locked destination survives failed replacement and leaves no staging artifacts.</summary>
    [Fact]
    public async Task LockedReplacementPreservesDestinationAndCleansStaging()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string path = workspace.Write("capture.png", [9, 8, 7]);
        await using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Exception failure = await Assert.ThrowsAnyAsync<Exception>(() => new LocalFileStore().WriteAsync(
                path, new byte[] { 1, 2, 3 },
                new LocalFileWriteOptions(LocalFileWriteMode.ReplaceExisting, true),
                TestContext.Current.CancellationToken).AsTask());
            Assert.True(failure is IOException or UnauthorizedAccessException);
        }

        Assert.Equal([9, 8, 7], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(workspace.Root));
    }

    /// <summary>Windows parent aliases are rejected by metadata inspection without creating a file.</summary>
    [Fact]
    public async Task InspectDestinationRejectsWindowsDirectoryAliasWhenAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string realParent = Path.Combine(workspace.Root, "real-parent");
        string aliasParent = Path.Combine(workspace.Root, "alias-parent");
        _ = Directory.CreateDirectory(realParent);
        _ = Directory.CreateSymbolicLink(aliasParent, realParent);

        LocalFileDestinationInfo info = await new LocalFileStore().InspectDestinationAsync(
            Path.Combine(aliasParent, "capture.png"), TestContext.Current.CancellationToken);
        Assert.True(info.ParentExists);
        Assert.False(info.ParentHasExactPath);
        Assert.Empty(Directory.GetFiles(realParent));
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial int WindowsCreateHardLink(string fileName, string existingFileName, nint securityAttributes);

    [LibraryImport("libc", EntryPoint = "link", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int UnixLink(string existingFileName, string fileName);
}
