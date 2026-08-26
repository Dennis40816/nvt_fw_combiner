using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies immutable Bootstrap argument ownership and defaults.</summary>
public sealed class LauncherBootstrapLaunchOptionsTests
{
    /// <summary>Explorer and zero-argument shortcuts use the existing canonical per-user state owner.</summary>
    [Fact]
    public void ZeroArgumentsUseBootstrapDirectoryAndCanonicalStatePath()
    {
        using TempWorkspace workspace = TempWorkspace.Create();

        LauncherBootstrapLaunchOptions result = LauncherBootstrapLaunchOptions.Parse([], workspace.Root);

        Assert.Equal(Path.GetFullPath(workspace.Root), result.ManagedRoot);
        Assert.Equal(Path.GetFullPath(JsonVersionManagerStateStore.GetDefaultPath()), result.StatePath);
    }

    /// <summary>A desktop-provided custom state path survives Bootstrap parsing byte-for-path.</summary>
    [Fact]
    public void ExplicitArgumentsPreserveExactFullPaths()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string managedRoot = Path.Combine(workspace.Root, "managed root");
        string statePath = Path.Combine(workspace.Root, "custom state", "state.json");

        LauncherBootstrapLaunchOptions result = LauncherBootstrapLaunchOptions.Parse(
            ["--managed-root", managedRoot, "--state-path", statePath],
            "ignored");

        Assert.Equal(Path.GetFullPath(managedRoot), result.ManagedRoot);
        Assert.Equal(Path.GetFullPath(statePath), result.StatePath);
    }

    /// <summary>Unknown options remain fail-closed.</summary>
    [Fact]
    public void UnknownOptionIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() => LauncherBootstrapLaunchOptions.Parse(
            ["--unknown", "value"],
            AppContext.BaseDirectory));
    }
}
