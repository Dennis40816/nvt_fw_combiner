using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies immutable Bootstrap argument ownership and defaults.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class LauncherBootstrapLaunchOptionsTests
{
    private const string ForbiddenSwitch = "NvtFwCombiner.LocalState.CurrentUserFolderForbidden";

    /// <summary>Explorer and zero-argument shortcuts use the existing canonical per-user state owner.</summary>
    [Fact]
    public void ZeroArgumentsUseBootstrapDirectoryAndCanonicalStatePath()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string? previousEnvironment = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        bool wasSet = AppContext.TryGetSwitch(
            ForbiddenSwitch,
            out bool previousSwitch);
        try
        {
            AppContext.SetSwitch(ForbiddenSwitch, false);
            Environment.SetEnvironmentVariable("LOCALAPPDATA", workspace.Root);

            LauncherBootstrapLaunchOptions result = LauncherBootstrapLaunchOptions.Parse([], workspace.Root);

            Assert.Equal(Path.GetFullPath(workspace.Root), result.ManagedRoot);
            Assert.Equal(
                Path.Combine(Path.GetFullPath(workspace.Root), "NvtFwCombiner", "version-manager.v1.json"),
                result.StatePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", previousEnvironment);
            AppContext.SetSwitch(
                ForbiddenSwitch,
                wasSet && previousSwitch);
        }
    }

    /// <summary>A test process cannot use Bootstrap's zero-argument default state path.</summary>
    [Fact]
    public void ZeroArgumentsRejectForbiddenDefault()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        bool wasSet = AppContext.TryGetSwitch(
            ForbiddenSwitch,
            out bool previousSwitch);
        try
        {
            AppContext.SetSwitch(ForbiddenSwitch, true);
            _ = Assert.Throws<InvalidOperationException>(() =>
                LauncherBootstrapLaunchOptions.Parse([], workspace.Root));
        }
        finally
        {
            AppContext.SetSwitch(
                ForbiddenSwitch,
                wasSet && previousSwitch);
        }
    }

    /// <summary>A desktop-provided custom state path survives Bootstrap parsing byte-for-path.</summary>
    [Fact]
    public void ExplicitArgumentsPreserveExactFullPaths()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string managedRoot = Path.Combine(workspace.Root, "managed root");
        string statePath = Path.Combine(workspace.Root, "custom state", "state.json");
        bool wasSet = AppContext.TryGetSwitch(
            ForbiddenSwitch,
            out bool previousSwitch);
        try
        {
            AppContext.SetSwitch(ForbiddenSwitch, true);
            LauncherBootstrapLaunchOptions result = LauncherBootstrapLaunchOptions.Parse(
                ["--managed-root", managedRoot, "--state-path", statePath],
                "ignored");

            Assert.Equal(Path.GetFullPath(managedRoot), result.ManagedRoot);
            Assert.Equal(Path.GetFullPath(statePath), result.StatePath);
        }
        finally
        {
            AppContext.SetSwitch(
                ForbiddenSwitch,
                wasSet && previousSwitch);
        }
    }

    /// <summary>The canonical default honors the process-local application-data boundary used by clean smoke.</summary>
    [Fact]
    public void CanonicalDefaultUsesExactLocalApplicationDataEnvironment()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string? previous = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        bool wasSet = AppContext.TryGetSwitch(
            ForbiddenSwitch,
            out bool previousSwitch);
        try
        {
            AppContext.SetSwitch(ForbiddenSwitch, false);
            Environment.SetEnvironmentVariable("LOCALAPPDATA", workspace.Root);

            string result = JsonVersionManagerStateStore.GetDefaultPath();

            Assert.Equal(
                Path.Combine(Path.GetFullPath(workspace.Root), "NvtFwCombiner", "version-manager.v1.json"),
                result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", previous);
            AppContext.SetSwitch(
                ForbiddenSwitch,
                wasSet && previousSwitch);
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
