using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises the single Windows no-follow tree-custody owner.</summary>
public sealed class WindowsStablePathCustodyTests
{
    /// <summary>Closed custody duplicates the exact promoted handle without a path-substitution gap.</summary>
    [Fact]
    public void HeldDirectoryTransfersExactIdentityAndDeleteDenialToClosedCustody()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("promoted-root");
        _ = Directory.CreateDirectory(root);
        string payload = Path.Combine(root, "payload.bin");
        File.WriteAllBytes(payload, [1, 2, 3]);
        Assert.Equal(
            WindowsStableCustodyIssue.None,
            WindowsStableRelativeWriteRoot.TryAcquire(
                root,
                out WindowsStableRelativeWriteRoot? heldRoot));
        using (heldRoot)
        {
            WindowsStableCustodyResult captured =
                WindowsStablePathCustody.TryCaptureImmutableTreeFromHeldDirectory(
                    root,
                    heldRoot!.RootHandle,
                    new WindowsStableTreeLimits(1, 0, 3),
                    TestContext.Current.CancellationToken);
            using WindowsStablePathCustody custody = Assert.IsType<WindowsStablePathCustody>(
                captured.Custody);
            Assert.Equal(WindowsStableCustodyIssue.None, captured.Issue);
            Assert.True(custody.HasSameRootIdentity(heldRoot.RootHandle));
            heldRoot.Dispose();
            _ = Assert.Throws<IOException>(() => Directory.Move(root, root + ".replaced"));
            _ = Assert.Throws<IOException>(() => File.WriteAllBytes(payload, [9]));
            Assert.True(custody.RevalidateClosedTree());
        }
        Directory.Delete(root, recursive: true);
    }

    /// <summary>Reservation admits every exact dimension and rejects each independent one-over value.</summary>
    [Fact]
    public void TreeReservationPairsExactAndOneOverBoundaries()
    {
        var limits = new WindowsStableTreeLimits(3, 4, 5);

        Assert.True(new WindowsStableTreeReservation(3, 4, 5, limits).IsWithinLimits);
        Assert.False(new WindowsStableTreeReservation(4, 4, 5, limits).IsWithinLimits);
        Assert.False(new WindowsStableTreeReservation(3, 5, 5, limits).IsWithinLimits);
        Assert.False(new WindowsStableTreeReservation(3, 4, 6, limits).IsWithinLimits);
    }

    /// <summary>Held bytes cannot change and a late namespace addition invalidates the tree.</summary>
    [Fact]
    public void ImmutableTreeLocksContentAndDetectsAddedChildUntilDisposed()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("version");
        string executable = Path.Combine(root, "NvtFwCombiner.exe");
        string library = Path.Combine(root, "runtime", "NvtFwCombiner.dll");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(library)!);
        File.WriteAllBytes(executable, [1, 2, 3]);
        File.WriteAllBytes(library, [4, 5, 6]);

        WindowsStableCustodyResult acquired =
            WindowsStablePathCustody.TryAcquireImmutableTree(
                root,
                cancellationToken: TestContext.Current.CancellationToken);
        using WindowsStablePathCustody custody = Assert.IsType<WindowsStablePathCustody>(
            acquired.Custody);

        Assert.Equal(WindowsStableCustodyIssue.None, acquired.Issue);
        _ = Assert.Throws<IOException>(() => File.WriteAllBytes(executable, [9]));
        _ = Assert.Throws<IOException>(() => File.Move(library, library + ".moved"));
        File.WriteAllText(Path.Combine(root, "unexpected.dll"), "foreign");
        Assert.False(custody.RevalidateClosedTree());

        custody.Dispose();
        Directory.Delete(root, recursive: true);
        Assert.False(Directory.Exists(root));
    }

    /// <summary>Ancestor custody blocks a same-name replacement before the relative root open.</summary>
    [Fact]
    public void FileCustodyCannotMixAnAncestorReplacementWithTheOriginalLeaf()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string parent = workspace.PathFor("parent");
        _ = Directory.CreateDirectory(parent);
        string file = Path.Combine(parent, "probe.exe");
        File.WriteAllBytes(file, [1, 2, 3]);
        bool blocked = false;

        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireFile(
            file,
            stage =>
            {
                if (stage != WindowsStableCustodyStage.BeforeRootOpen)
                {
                    return;
                }
                try
                {
                    Directory.Move(parent, parent + ".displaced");
                }
                catch (IOException)
                {
                    blocked = true;
                }
            },
            TestContext.Current.CancellationToken);

        using WindowsStablePathCustody custody = Assert.IsType<WindowsStablePathCustody>(
            acquired.Custody);
        Assert.True(blocked);
        using FileStream stream = custody.OpenReadOnlyFile("probe.exe");
        Assert.Equal(3, stream.Length);
    }

    /// <summary>A reparse child never enters immutable-tree custody.</summary>
    [Fact]
    public void ImmutableTreeRejectsReparseChild()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("version");
        string target = workspace.PathFor("target.txt");
        _ = Directory.CreateDirectory(root);
        File.WriteAllText(target, "target");
        try
        {
            _ = File.CreateSymbolicLink(Path.Combine(root, "linked.txt"), target);
        }
        catch (UnauthorizedAccessException exception)
        {
            Assert.Skip($"Symbolic-link privilege is unavailable: {exception.Message}");
        }

        WindowsStableCustodyResult acquired =
            WindowsStablePathCustody.TryAcquireImmutableTree(
                root,
                cancellationToken: TestContext.Current.CancellationToken);

        acquired.Custody?.Dispose();
        Assert.Null(acquired.Custody);
        Assert.Equal(WindowsStableCustodyIssue.ReparsePoint, acquired.Issue);
    }

    /// <summary>Relative and device paths never acquire filesystem authority.</summary>
    [Theory]
    [InlineData("relative\\version")]
    [InlineData("\\\\?\\C:\\managed\\version")]
    [InlineData("C:\\managed\\version:stream")]
    public void UnsafeAuthorityPathIsRejected(string path)
    {
        WindowsStableCustodyResult acquired =
            WindowsStablePathCustody.TryAcquireImmutableTree(
                path,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(acquired.Custody);
        Assert.Equal(WindowsStableCustodyIssue.InvalidPath, acquired.Issue);
    }

    /// <summary>Custody rejects a tree before opening more than the installed-file authority.</summary>
    [Fact]
    public void ImmutableTreeEnforcesPackageEntryBoundAndReleasesPartialCustody()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("version");
        _ = Directory.CreateDirectory(root);
        for (int index = 0;
             index <= FileSystemManagedVersionRepository.MaximumInstalledFiles;
             index++)
        {
            File.WriteAllBytes(Path.Combine(root, $"{index:D5}.bin"), []);
        }

        WindowsStableCustodyResult acquired =
            WindowsStablePathCustody.TryAcquireImmutableTree(
                root,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(acquired.Custody);
        Assert.Equal(WindowsStableCustodyIssue.Unavailable, acquired.Issue);
        Directory.Delete(root, recursive: true);
    }

    /// <summary>Custody rejects a sparse member beyond package plus admission bytes.</summary>
    [Fact]
    public void ImmutableTreeEnforcesPackageByteBoundAndReleasesPartialCustody()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("version");
        _ = Directory.CreateDirectory(root);
        string oversized = Path.Combine(root, "oversized.bin");
        using (FileStream stream = File.Create(oversized))
        {
            stream.SetLength(FileSystemManagedVersionRepository.MaximumInstalledBytes + 1);
        }

        WindowsStableCustodyResult acquired =
            WindowsStablePathCustody.TryAcquireImmutableTree(
                root,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(acquired.Custody);
        Assert.Equal(WindowsStableCustodyIssue.Unavailable, acquired.Issue);
        File.Delete(oversized);
    }

    /// <summary>Cancellation during tree capture releases every partially acquired handle.</summary>
    [Fact]
    public void ImmutableTreeCancellationReleasesPartialCustody()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("version");
        _ = Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "payload.bin"), [1, 2, 3]);
        using var cancellation = new CancellationTokenSource();

        _ = Assert.ThrowsAny<OperationCanceledException>(() =>
            WindowsStablePathCustody.TryAcquireImmutableTree(
                root,
                stage =>
                {
                    if (stage == WindowsStableCustodyStage.AfterTreeCaptured)
                    {
                        cancellation.Cancel();
                    }
                },
                cancellation.Token));

        string released = root + ".released";
        Directory.Move(root, released);
        string releasedPayload = Path.Combine(released, "payload.bin");
        File.WriteAllBytes(releasedPayload, [4, 5, 6]);
        Assert.Equal([4, 5, 6], File.ReadAllBytes(releasedPayload));
    }

    /// <summary>The declared installed-file ceiling including admission remains admissible.</summary>
    [Fact]
    public void ImmutableTreeAcceptsExactPackageEntryBound()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("version");
        _ = Directory.CreateDirectory(root);
        for (int index = 0;
             index < FileSystemManagedVersionRepository.MaximumInstalledFiles;
             index++)
        {
            File.WriteAllBytes(Path.Combine(root, $"{index:D5}.bin"), []);
        }

        WindowsStableCustodyResult acquired =
            WindowsStablePathCustody.TryAcquireImmutableTree(
                root,
                cancellationToken: TestContext.Current.CancellationToken);

        using WindowsStablePathCustody custody = Assert.IsType<WindowsStablePathCustody>(
            acquired.Custody);
        Assert.Equal(WindowsStableCustodyIssue.None, acquired.Issue);
        custody.Dispose();
        Directory.Delete(root, recursive: true);
    }

    /// <summary>Package plus admission byte authority remains admissible without materializing bytes.</summary>
    [Fact]
    public void ImmutableTreeAcceptsExactPackageByteBound()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("version");
        _ = Directory.CreateDirectory(root);
        string boundary = Path.Combine(root, "boundary.bin");
        using (FileStream stream = File.Create(boundary))
        {
            stream.SetLength(FileSystemManagedVersionRepository.MaximumInstalledBytes);
        }

        WindowsStableCustodyResult acquired =
            WindowsStablePathCustody.TryAcquireImmutableTree(
                root,
                cancellationToken: TestContext.Current.CancellationToken);

        using WindowsStablePathCustody custody = Assert.IsType<WindowsStablePathCustody>(
            acquired.Custody);
        Assert.Equal(WindowsStableCustodyIssue.None, acquired.Issue);
        custody.Dispose();
        File.Delete(boundary);
    }

    /// <summary>File and directory ceilings are independent rather than one shared entry count.</summary>
    [Fact]
    public void ImmutableTreeAppliesIndependentFileAndDirectoryLimits()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("version");
        _ = Directory.CreateDirectory(Path.Combine(root, "one", "two"));
        File.WriteAllBytes(Path.Combine(root, "one", "payload.bin"), [1]);
        var limits = new WindowsStableTreeLimits(
            maximumFiles: 1,
            maximumDirectories: 1,
            maximumBytes: 1);

        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireImmutableTree(
            root,
            treeLimits: limits,
            cancellationToken: TestContext.Current.CancellationToken);

        acquired.Custody?.Dispose();
        Assert.Null(acquired.Custody);
        Assert.Equal(WindowsStableCustodyIssue.Unavailable, acquired.Issue);
        Directory.Delete(root, recursive: true);
    }

    /// <summary>The exact independent directory ceiling remains admissible.</summary>
    [Fact]
    public void ImmutableTreeAcceptsExactDirectoryLimit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = TempWorkspace.Create();
        string root = workspace.PathFor("version");
        _ = Directory.CreateDirectory(Path.Combine(root, "one", "two"));
        File.WriteAllBytes(Path.Combine(root, "one", "payload.bin"), [1]);
        var limits = new WindowsStableTreeLimits(1, 2, 1);

        WindowsStableCustodyResult acquired = WindowsStablePathCustody.TryAcquireImmutableTree(
            root,
            treeLimits: limits,
            cancellationToken: TestContext.Current.CancellationToken);

        using WindowsStablePathCustody custody = Assert.IsType<WindowsStablePathCustody>(
            acquired.Custody);
        Assert.Equal(WindowsStableCustodyIssue.None, acquired.Issue);
        custody.Dispose();
        Directory.Delete(root, recursive: true);
    }
}
