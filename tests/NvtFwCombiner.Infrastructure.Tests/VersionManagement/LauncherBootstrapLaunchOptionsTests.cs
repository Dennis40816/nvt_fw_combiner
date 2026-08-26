using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies immutable Bootstrap argument ownership and defaults.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
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

    /// <summary>The canonical default honors the process-local application-data boundary used by clean smoke.</summary>
    [Fact]
    public void CanonicalDefaultUsesExactLocalApplicationDataEnvironment()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string? previous = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        try
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", workspace.Root);

            string result = JsonVersionManagerStateStore.GetDefaultPath();

            Assert.Equal(
                Path.Combine(Path.GetFullPath(workspace.Root), "NvtFwCombiner", "version-manager.v1.json"),
                result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", previous);
        }
    }

    /// <summary>Unknown options remain fail-closed.</summary>
    [Fact]
    public void UnknownOptionIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() => LauncherBootstrapLaunchOptions.Parse(
            ["--unknown", "value"],
            AppContext.BaseDirectory));
    }

    /// <summary>An explicitly empty managed root or state path never falls back to the working directory.</summary>
    [Theory]
    [InlineData("--managed-root")]
    [InlineData("--state-path")]
    public void EmptyExplicitPathIsRejected(string option)
    {
        _ = Assert.Throws<ArgumentException>(() => LauncherBootstrapLaunchOptions.Parse(
            [option, ""],
            AppContext.BaseDirectory));
    }
}
