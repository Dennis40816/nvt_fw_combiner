using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Tests managed-root discovery independently of an absolute installation location.</summary>
public sealed class ManagedInstallationLayoutTests
{
    /// <summary>A side-by-side version payload resolves to the stable root before and after root relocation.</summary>
    [Fact]
    public void SideBySidePayloadResolvesRelocatableStableRoot()
    {
        using var workspace = TempWorkspace.Create();
        string firstRoot = workspace.PathFor("first-managed-root");
        string payload = Path.Combine(firstRoot, "versions", "0.10.6");
        _ = Directory.CreateDirectory(payload);
        string relocatedRoot = workspace.PathFor("relocated-managed-root");

        string first = ManagedInstallationLayout.ResolveManagedRoot(payload);
        Directory.Move(firstRoot, relocatedRoot);
        string relocated = ManagedInstallationLayout.ResolveManagedRoot(
            Path.Combine(relocatedRoot, "versions", "0.10.6"));

        Assert.Equal(Path.GetFullPath(firstRoot), first);
        Assert.Equal(Path.GetFullPath(relocatedRoot), relocated);
    }

    /// <summary>An unmanaged development base remains its own root and is never inferred from its parent.</summary>
    [Fact]
    public void UnmanagedApplicationBaseRemainsItsOwnRoot()
    {
        using var workspace = TempWorkspace.Create();
        string applicationBase = workspace.PathFor("development-output");
        _ = Directory.CreateDirectory(applicationBase);

        string resolved = ManagedInstallationLayout.ResolveManagedRoot(applicationBase);

        Assert.Equal(Path.GetFullPath(applicationBase), resolved);
    }
}
