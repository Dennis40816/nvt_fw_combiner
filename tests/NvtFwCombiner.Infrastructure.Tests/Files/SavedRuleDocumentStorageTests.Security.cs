using System.Security.AccessControl;
using System.Security.Principal;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

public sealed partial class SavedRuleDocumentStorageTests
{
    /// <summary>Configured roots and resolved targets remain fail-closed against path replacement.</summary>
    [Fact]
    public async Task StorageRejectsOverlapEscapeAndResolvedTargetReparse()
    {
        using var workspace = TempWorkspace.Create("nfc-saved-rule-path-guard");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        _ = Assert.Throws<ArgumentException>(() =>
            new SavedRuleDocumentStorage([authoringRoot], [authoringRoot]));
        var storage = new SavedRuleDocumentStorage(
            [authoringRoot],
            [catalogRoot]);
        _ = Assert.Throws<UnauthorizedAccessException>(() =>
            storage.ResolveTarget(workspace.PathFor("outside.json")));

        string targetPath = Path.Combine(authoringRoot, "target.json");
        SavedRuleDocumentStorageLocation target =
            storage.ResolveTarget(targetPath);
        string outsidePath = workspace.PathFor("outside-target.json");
        await File.WriteAllBytesAsync(
            outsidePath,
            Document("outside"),
            TestContext.Current.CancellationToken);
        try
        {
            _ = File.CreateSymbolicLink(targetPath, outsidePath);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or
                PlatformNotSupportedException or IOException)
        {
            return;
        }

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await storage.WriteAsync(
                target,
                Document("after"),
                TestContext.Current.CancellationToken));
        Assert.Equal(
            Document("outside"),
            await File.ReadAllBytesAsync(
                outsidePath,
                TestContext.Current.CancellationToken));
    }

    /// <summary>The Windows scope anchors nested target directories until promotion completes.</summary>
    [Fact]
    public void WindowsWriteScopePinsNestedTargetDirectoryUntilDisposed()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-saved-rule-directory-lease");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string intermediateDirectory = Path.Combine(authoringRoot, "nested");
        string nestedDirectory = Path.Combine(intermediateDirectory, "rules");
        _ = Directory.CreateDirectory(nestedDirectory);
        string movedRoot = workspace.PathFor("authoring-moved");
        string movedIntermediate = Path.Combine(authoringRoot, "nested-moved");
        string movedDirectory = Path.Combine(authoringRoot, "nested", "rules-moved");
        string targetPath = Path.Combine(nestedDirectory, "rule.json");

        using (AtomicFileWriteScope.Open(targetPath))
        {
            _ = Assert.Throws<IOException>(() =>
                Directory.Move(authoringRoot, movedRoot));
            _ = Assert.Throws<IOException>(() =>
                Directory.Move(intermediateDirectory, movedIntermediate));
            _ = Assert.Throws<IOException>(() =>
                Directory.Move(nestedDirectory, movedDirectory));
            string anchorPath = Assert.Single(
                Directory.EnumerateFiles(nestedDirectory),
                path => Path.GetFileName(path).EndsWith(
                    ".lease",
                    StringComparison.Ordinal));
            _ = Assert.Throws<IOException>(() => File.Delete(anchorPath));
        }

        Assert.Empty(Directory.EnumerateFiles(nestedDirectory));
        Directory.Move(nestedDirectory, movedDirectory);
        Directory.Move(movedDirectory, nestedDirectory);
    }

    /// <summary>Writing a child document does not require authority to delete its directory.</summary>
    [Fact]
    public async Task WindowsStorageDoesNotRequireDirectoryDeleteAuthority()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-saved-rule-directory-acl");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        var directory = new DirectoryInfo(authoringRoot);
        DirectorySecurity security = directory.GetAccessControl(
            AccessControlSections.Access);
        string originalAccess = security.GetSecurityDescriptorSddlForm(
            AccessControlSections.Access);
        SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User ??
            throw new InvalidOperationException("The Windows test user has no SID.");
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.Delete,
            InheritanceFlags.None,
            PropagationFlags.None,
            AccessControlType.Deny));
        directory.SetAccessControl(security);
        try
        {
            var storage = new SavedRuleDocumentStorage(
                [authoringRoot],
                [catalogRoot]);
            string targetPath = Path.Combine(authoringRoot, "rule.json");

            await storage.WriteAsync(
                storage.ResolveTarget(targetPath),
                Document("after"),
                TestContext.Current.CancellationToken);

            Assert.Equal(
                Document("after"),
                await File.ReadAllBytesAsync(
                    targetPath,
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            var restored = new DirectorySecurity();
            restored.SetSecurityDescriptorSddlForm(
                originalAccess,
                AccessControlSections.Access);
            directory.SetAccessControl(restored);
        }
    }

    /// <summary>A failed atomic promotion removes its staging and lease files and preserves the destination.</summary>
    [Fact]
    public async Task FailedWindowsPromotionRemovesTemporaryArtifact()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = TempWorkspace.Create("nfc-saved-rule-move-failure");
        string authoringRoot = CreateDirectory(workspace, "authoring");
        string catalogRoot = CreateDirectory(workspace, "catalog");
        var storage = new SavedRuleDocumentStorage(
            [authoringRoot],
            [catalogRoot]);
        string targetPath = Path.Combine(authoringRoot, "rule.json");
        SavedRuleDocumentStorageLocation target =
            storage.ResolveTarget(targetPath);
        byte[] original = Document("before");
        await File.WriteAllBytesAsync(
            targetPath,
            original,
            TestContext.Current.CancellationToken);
        await using (FileStream lockStream = new(
                         targetPath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.None))
        {
            _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await storage.WriteAsync(
                    target,
                    Document("after"),
                    TestContext.Current.CancellationToken));
        }

        Assert.Equal(original, await File.ReadAllBytesAsync(
            targetPath,
            TestContext.Current.CancellationToken));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(authoringRoot),
            path => Path.GetFileName(path).EndsWith(
                ".tmp",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(authoringRoot),
            path => Path.GetFileName(path).EndsWith(
                ".lease",
                StringComparison.Ordinal));
    }
}
