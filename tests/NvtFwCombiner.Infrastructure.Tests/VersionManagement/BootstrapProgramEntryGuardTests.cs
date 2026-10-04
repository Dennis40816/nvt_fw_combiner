using System.Reflection;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies immutable Bootstrap host guards through its real executable entry point.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class BootstrapProgramEntryGuardTests
{
    /// <summary>Malformed arguments return InvalidArguments/20 before default state-path resolution.</summary>
    [Theory]
    [InlineData("--unknown", "value")]
    [InlineData("--managed-root", null)]
    [InlineData("--state-path", null)]
    [InlineData("--managed-root", "")]
    [InlineData("--state-path", "")]
    public void MalformedArgumentsReturnInvalidArgumentsExitCode(string option, string? value)
    {
        string[] args = value is null ? [option] : [option, value];

        Assert.Equal(20, RunBootstrapEntry(args));
    }

    /// <summary>A forbidden default state path returns InvariantViolation/21 at the host boundary.</summary>
    [Fact]
    public void ForbiddenDefaultStatePathReturnsInvariantViolationExitCode()
    {
        Assert.Equal(21, RunBootstrapEntry([]));
    }

    private static int RunBootstrapEntry(string[] args)
    {
        const string switchName = "NvtFwCombiner.LocalState.CurrentUserFolderForbidden";
        bool wasSet = AppContext.TryGetSwitch(switchName, out bool previous);
        try
        {
            AppContext.SetSwitch(switchName, true);
            Assembly bootstrap = Assembly.LoadFrom(Path.Combine(
                AppContext.BaseDirectory,
                "launcher-bootstrap",
                "NvtFwCombiner.LauncherBootstrap.dll"));
            MethodInfo entryPoint = bootstrap.EntryPoint!;

            return Assert.IsType<int>(entryPoint.Invoke(null, [args]));
        }
        finally
        {
            AppContext.SetSwitch(switchName, wasSet && previous);
        }
    }
}
