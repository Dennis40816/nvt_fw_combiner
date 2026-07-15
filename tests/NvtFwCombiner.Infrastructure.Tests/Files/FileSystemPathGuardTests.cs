using System.Text;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests bundle-relative path confinement in the shared filesystem guard.</summary>
public sealed class FileSystemPathGuardTests
{
    /// <summary>Verifies a canonical manifest path resolves to one existing file under the root.</summary>
    [Fact]
    public void ResolveExistingManifestFileUnderRootReturnsConfinedFile()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-path");
        string expected = workspace.Write("profiles/profile.json", Encoding.UTF8.GetBytes("{}"));

        string actual = FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(
            "profiles/profile.json",
            workspace.Root);

        Assert.Equal(Path.GetFullPath(expected), actual);
    }

    /// <summary>Verifies absolute, traversal, alternate-separator, ADS, and empty segments fail closed.</summary>
    [Theory]
    [InlineData("../outside.json")]
    [InlineData("profiles/../outside.json")]
    [InlineData("./profiles/profile.json")]
    [InlineData("profiles//profile.json")]
    [InlineData("profiles\\profile.json")]
    [InlineData("profiles/profile.json:stream")]
    [InlineData("/profiles/profile.json")]
    [InlineData("C:/profiles/profile.json")]
    public void ResolveExistingManifestFileUnderRootRejectsPathSyntax(string manifestPath)
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-path");

        _ = Assert.Throws<ArgumentException>(() =>
            FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(manifestPath, workspace.Root));
    }

    /// <summary>Verifies missing files and directories cannot masquerade as bundle entry files.</summary>
    [Fact]
    public void ResolveExistingManifestFileUnderRootRequiresFile()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-path");
        _ = Directory.CreateDirectory(workspace.PathFor("profiles/directory.json"));

        _ = Assert.Throws<FileNotFoundException>(() =>
            FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(
                "profiles/missing.json",
                workspace.Root));
        _ = Assert.Throws<FileNotFoundException>(() =>
            FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(
                "profiles/directory.json",
                workspace.Root));
    }

    /// <summary>Verifies the selected bundle root must already exist and cannot be a file.</summary>
    [Fact]
    public void ResolveExistingManifestFileUnderRootRequiresDirectoryRoot()
    {
        using var workspace = TempWorkspace.Create("nfc-bundle-path");
        string fileRoot = workspace.Write("root.bin", []);

        _ = Assert.Throws<DirectoryNotFoundException>(() =>
            FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(
                "profiles/profile.json",
                workspace.PathFor("missing")));
        _ = Assert.Throws<DirectoryNotFoundException>(() =>
            FileSystemPathGuard.ResolveExistingManifestFileUnderRoot(
                "profiles/profile.json",
                fileRoot));
    }
}
