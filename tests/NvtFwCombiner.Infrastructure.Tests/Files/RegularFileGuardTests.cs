using System.Text;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Tests open-handle identity validation at the bundle file boundary.</summary>
public sealed class RegularFileGuardTests
{
    /// <summary>Verifies the regular file handle opened from the validated path is accepted.</summary>
    [Fact]
    public void RequireOpenHandleAcceptsValidatedFileIdentity()
    {
        using var workspace = TempWorkspace.Create("nfc-regular-file-handle");
        string path = workspace.Write("profile.json", Encoding.UTF8.GetBytes("{}"));
        using FileStream stream = File.OpenRead(path);

        RegularFileGuard.RequireOpenHandle(stream.SafeFileHandle, path, "profile.json");
    }

    /// <summary>Verifies a regular handle cannot be substituted for a different validated path.</summary>
    [Fact]
    public void RequireOpenHandleRejectsSubstitutedFileIdentity()
    {
        using var workspace = TempWorkspace.Create("nfc-regular-file-handle");
        string openedPath = workspace.Write("opened.json", Encoding.UTF8.GetBytes("{}"));
        string validatedPath = workspace.Write("validated.json", Encoding.UTF8.GetBytes("{}"));
        using FileStream stream = File.OpenRead(openedPath);

        UnauthorizedAccessException exception = Assert.Throws<UnauthorizedAccessException>(() =>
            RegularFileGuard.RequireOpenHandle(
                stream.SafeFileHandle,
                validatedPath,
                "validated.json"));

        Assert.Contains("does not match the validated path", exception.Message, StringComparison.Ordinal);
    }
}
