using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

public sealed partial class FileSystemManagedVersionRepositoryTests
{
    internal static UpdateCatalogVersionSnapshot CreatePackageForManagedSetup(
        string sourceRoot,
        string version,
        bool includeManagedLauncher = false,
        bool useStandaloneLauncherProbe = false)
    {
        return CreatePackage(
            sourceRoot,
            version,
            includeManagedLauncher: includeManagedLauncher,
            managedLauncherBytes: useStandaloneLauncherProbe
                ? File.ReadAllBytes(Path.Combine(Environment.SystemDirectory, "where.exe"))
                : null);
    }

    /// <summary>An ordinary update cannot bootstrap a missing managed root before custody exists.</summary>
    [Fact]
    public async Task InstallRejectsMissingManagedRootWithoutCreatingResidue()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string missingRoot = workspace.PathFor("missing-managed-root");
        UpdateCatalogVersionSnapshot package = CreatePackage(sourceRoot, "0.10.6");

        ManagedVersionInstallResult result = await new FileSystemManagedVersionRepository().InstallAsync(
            missingRoot,
            sourceRoot,
            package,
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.PromotionFailed, result.Issue);
        Assert.False(Directory.Exists(missingRoot));
        Assert.False(File.Exists(missingRoot));
    }

    /// <summary>A nested junction race fails typed without writing or promoting outside custody.</summary>
    [Fact]
    public async Task InstallRejectsNestedJunctionRaceWithoutOutsideWriteOrPromotion()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        string outside = workspace.PathFor("outside");
        _ = Directory.CreateDirectory(outside);
        string sentinel = Path.Combine(outside, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "outside-owner", TestContext.Current.CancellationToken);
        bool nestedWriteBlocked = false;
        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            Directory.EnumerateDirectories,
            beforePackagePromotion: null,
            afterPackageDirectoryCreated: directory =>
            {
                if (string.Equals(Path.GetFileName(directory), "external-tools", StringComparison.Ordinal))
                {
                    nestedWriteBlocked = JunctionWasBlocked(
                        Path.Combine(directory, "crc-worker"),
                        outside);
                }
            });

        ManagedVersionInstallResult result = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.CleanupIncomplete, result.Issue);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
        Assert.NotEmpty(Directory.EnumerateFileSystemEntries(Path.Combine(managedRoot, ".staging")));
        Assert.Equal("outside-owner", await File.ReadAllTextAsync(
            sentinel,
            TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(outside, "Nfc.CrcWorker.exe")));
        Assert.False(nestedWriteBlocked);
    }

    /// <summary>Held files deny rewrites and pre-promotion topology rejects a late child.</summary>
    [Fact]
    public async Task InstallBlocksVerifiedFileRewriteAndRejectsLateChildBeforePromotion()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        bool verifiedWriteBlocked = false;
        bool lateChildBlocked = false;
        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            Directory.EnumerateDirectories,
            beforePackagePromotion: stagingRoot =>
            {
                verifiedWriteBlocked = WriteWasBlocked(Path.Combine(stagingRoot, "README.txt"));
                lateChildBlocked = WriteWasBlocked(Path.Combine(stagingRoot, "late-child.txt"));
            });

        ManagedVersionInstallResult result = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.CleanupIncomplete, result.Issue);
        Assert.True(verifiedWriteBlocked);
        Assert.False(lateChildBlocked);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
        Assert.NotEmpty(Directory.EnumerateFileSystemEntries(Path.Combine(managedRoot, ".staging")));
    }

    /// <summary>A foreign child that prevents exact cleanup is reported as recovery residue.</summary>
    [Fact]
    public async Task InstallReportsCleanupIncompleteWhenForeignChildPreventsExactCleanup()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        using var cancellation = new CancellationTokenSource();
        bool injected = false;
        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            Directory.EnumerateDirectories,
            afterPackageDirectoryCreated: directory =>
            {
                if (!injected)
                {
                    injected = true;
                    File.WriteAllText(Path.Combine(directory, "foreign-child.txt"), "foreign");
                    cancellation.Cancel();
                }
            });

        ManagedVersionInstallResult result = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            cancellation.Token);

        Assert.True(injected);
        Assert.Equal(ManagedVersionInstallIssue.CleanupIncomplete, result.Issue);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
        Assert.NotEmpty(Directory.EnumerateFileSystemEntries(Path.Combine(managedRoot, ".staging")));
    }

    /// <summary>Cancellation after nested staging creation removes only the exact owned tree.</summary>
    [Fact]
    public async Task InstallCancellationAfterNestedCreationCleansOwnedTree()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        string outside = workspace.PathFor("outside-sentinel.txt");
        await File.WriteAllTextAsync(outside, "outside", TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        bool cancelled = false;
        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            Directory.EnumerateDirectories,
            afterPackageDirectoryCreated: _ =>
            {
                if (!cancelled)
                {
                    cancelled = true;
                    cancellation.Cancel();
                }
            });

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await repository.InstallAsync(
                managedRoot,
                sourceRoot,
                CreatePackage(sourceRoot, "0.10.6"),
                cancellation.Token));

        Assert.True(cancelled);
        Assert.False(Directory.Exists(Path.Combine(managedRoot, "versions", "0.10.6")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(managedRoot, ".staging")));
        Assert.Equal("outside", await File.ReadAllTextAsync(outside, TestContext.Current.CancellationToken));
    }

    /// <summary>Non-replacing promotion preserves a destination created immediately before rename.</summary>
    [Fact]
    public async Task InstallPromotionNeverReplacesLateDestination()
    {
        using var workspace = TempWorkspace.Create();
        string sourceRoot = workspace.PathFor("source");
        string managedRoot = CreateManagedRoot(workspace, "managed");
        string target = Path.Combine(managedRoot, "versions", "0.10.6");
        string sentinel = Path.Combine(target, "owner.txt");
        var repository = new FileSystemManagedVersionRepository(
            FileSystemManagedVersionRepository.MaximumExpandedBytes,
            Directory.EnumerateDirectories,
            beforePackagePromotion: _stagingRoot =>
            {
                _ = Directory.CreateDirectory(target);
                File.WriteAllText(sentinel, "destination-owner");
            });

        ManagedVersionInstallResult result = await repository.InstallAsync(
            managedRoot,
            sourceRoot,
            CreatePackage(sourceRoot, "0.10.6"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ManagedVersionInstallIssue.IdentityConflict, result.Issue);
        Assert.Equal("destination-owner", await File.ReadAllTextAsync(
            sentinel,
            TestContext.Current.CancellationToken));
        Assert.Equal(["owner.txt"], Directory.EnumerateFiles(target).Select(Path.GetFileName));
    }

    /// <summary>The held staging parent permits rename and exact final-tree rollback.</summary>
    [Fact]
    public void PreparedTreePromotesWithHeldParentAndDeletesExactFinalTree()
    {
        using var workspace = TempWorkspace.Create();
        string managedRoot = CreateManagedRoot(workspace, "managed");
        Assert.Equal(
            WindowsStableCustodyIssue.None,
            WindowsStableRelativeWriteRoot.TryAcquire(
                managedRoot,
                out WindowsStableRelativeWriteRoot? acquiredRoot));
        using WindowsStableRelativeWriteRoot writeRoot = acquiredRoot!;
        Assert.Equal(
            WindowsStableCustodyIssue.None,
            writeRoot.TryCreateVersionTree(
                "0.10.6",
                new WindowsStableTreeReservation(
                    Files: 1,
                    Directories: 1,
                    Bytes: 4,
                    new WindowsStableTreeLimits(1, 1, 4)),
                afterDirectoryCreated: null,
                out WindowsStableRelativeWriteTree? createdTree));
        using WindowsStableRelativeWriteTree tree = createdTree!;
        using (FileStream payload = tree.CreateFile("nested/payload.bin"))
        {
            payload.Write([1, 2, 3, 4]);
        }

        Assert.Equal(WindowsStableCustodyIssue.None, tree.PrepareForPromotion());
        Assert.Equal(WindowsStableCustodyIssue.None, tree.Promote());
        string finalPath = Path.Combine(managedRoot, "versions", "0.10.6");
        WindowsStableCustodyResult captured = tree.CapturePromotedImmutableTree(
            finalPath,
            TestContext.Current.CancellationToken);
        Assert.True(captured.IsAcquired);
        captured.Custody!.Dispose();

        Assert.Equal(WindowsStableCustodyIssue.None, tree.RollbackPromotionAndCleanup());
        Assert.False(Directory.Exists(finalPath));
    }

    /// <summary>Cancellation after promotion cannot prevent exact cleanup or delete a reused staging name.</summary>
    [Fact]
    public void PromotedTreeCancellationCleansHeldRootAndPreservesForeignStagingReplacement()
    {
        using var workspace = TempWorkspace.Create();
        string managedRoot = CreateManagedRoot(workspace, "managed");
        Assert.Equal(
            WindowsStableCustodyIssue.None,
            WindowsStableRelativeWriteRoot.TryAcquire(
                managedRoot,
                out WindowsStableRelativeWriteRoot? acquiredRoot));
        using WindowsStableRelativeWriteRoot writeRoot = acquiredRoot!;
        Assert.Equal(
            WindowsStableCustodyIssue.None,
            writeRoot.TryCreateVersionTree(
                "0.10.6",
                new WindowsStableTreeReservation(
                    Files: 1,
                    Directories: 1,
                    Bytes: 4,
                    new WindowsStableTreeLimits(1, 1, 4)),
                afterDirectoryCreated: null,
                out WindowsStableRelativeWriteTree? createdTree));
        using WindowsStableRelativeWriteTree tree = createdTree!;
        using (FileStream payload = tree.CreateFile("nested/payload.bin"))
        {
            payload.Write([1, 2, 3, 4]);
        }

        Assert.Equal(WindowsStableCustodyIssue.None, tree.PrepareForPromotion());
        Assert.Equal(WindowsStableCustodyIssue.None, tree.Promote());
        string finalPath = Path.Combine(managedRoot, "versions", "0.10.6");
        string reusedStaging = tree.StagingPath;
        _ = Directory.CreateDirectory(reusedStaging);
        string foreign = Path.Combine(reusedStaging, "foreign.txt");
        File.WriteAllText(foreign, "foreign");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Assert.ThrowsAny<OperationCanceledException>(() =>
            tree.CapturePromotedImmutableTree(finalPath, cancellation.Token));

        Assert.Equal(WindowsStableCustodyIssue.None, tree.RollbackPromotionAndCleanup());
        Assert.False(Directory.Exists(finalPath));
        Assert.Equal("foreign", File.ReadAllText(foreign));
    }

    /// <summary>A released descendant replacement is never mistaken for an owned file.</summary>
    [Fact]
    public void PreparedTreeRejectsAndPreservesSubstitutedDescendant()
    {
        using var workspace = TempWorkspace.Create();
        string managedRoot = CreateManagedRoot(workspace, "managed");
        Assert.Equal(
            WindowsStableCustodyIssue.None,
            WindowsStableRelativeWriteRoot.TryAcquire(
                managedRoot,
                out WindowsStableRelativeWriteRoot? acquiredRoot));
        using WindowsStableRelativeWriteRoot writeRoot = acquiredRoot!;
        Assert.Equal(
            WindowsStableCustodyIssue.None,
            writeRoot.TryCreateVersionTree(
                "0.10.6",
                new WindowsStableTreeReservation(
                    Files: 1,
                    Directories: 1,
                    Bytes: 4,
                    new WindowsStableTreeLimits(1, 1, 16)),
                afterDirectoryCreated: null,
                out WindowsStableRelativeWriteTree? createdTree));
        using WindowsStableRelativeWriteTree tree = createdTree!;
        using (FileStream payload = tree.CreateFile("nested/payload.bin"))
        {
            payload.Write([1, 2, 3, 4]);
        }
        Assert.Equal(WindowsStableCustodyIssue.None, tree.PrepareForPromotion());
        string stagedPayload = Path.Combine(tree.StagingPath, "nested", "payload.bin");
        string releasedOriginal = workspace.PathFor("released-original.bin");
        File.Move(stagedPayload, releasedOriginal);
        File.WriteAllText(stagedPayload, "foreign");

        Assert.Equal(WindowsStableCustodyIssue.None, tree.Promote());
        string finalPath = Path.Combine(managedRoot, "versions", "0.10.6");
        WindowsStableCustodyResult captured = tree.CapturePromotedImmutableTree(
            finalPath,
            TestContext.Current.CancellationToken);
        Assert.False(captured.IsAcquired);
        Assert.Equal(WindowsStableCustodyIssue.Changed, captured.Issue);
        Assert.Equal(WindowsStableCustodyIssue.Changed, tree.RollbackPromotionAndCleanup());
        Assert.Equal("foreign", File.ReadAllText(Path.Combine(finalPath, "nested", "payload.bin")));
        Assert.Equal([1, 2, 3, 4], File.ReadAllBytes(releasedOriginal));
    }

    private static bool WriteWasBlocked(string path)
    {
        try
        {
            File.WriteAllText(path, "attacker");
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool JunctionWasBlocked(string path, string target)
    {
        try
        {
            _ = Directory.CreateSymbolicLink(path, target);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
