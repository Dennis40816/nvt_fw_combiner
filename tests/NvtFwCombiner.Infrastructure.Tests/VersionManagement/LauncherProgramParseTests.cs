using System.Reflection;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises the actual private Launcher argument parser without starting an update process.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class LauncherProgramParseTests
{
    /// <summary>An explicit state path reaches the actual Launcher parser under the test guard.</summary>
    [Fact]
    public void ExplicitStatePathBypassesForbiddenDefault()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        string managedRoot = workspace.PathFor("managed root");
        string statePath = workspace.PathFor("custom state/state.json");
        WithForbiddenDefault(() =>
        {
            (string actualRoot, string actualState) = Parse(
                ["--managed-root", managedRoot, "--state-path", statePath]);
            Assert.Equal(Path.GetFullPath(managedRoot), actualRoot);
            Assert.Equal(Path.GetFullPath(statePath), actualState);
        });
    }

    /// <summary>Omitting the state path requires guarded default resolution.</summary>
    [Fact]
    public void OmittedStatePathRejectsForbiddenDefault()
    {
        WithForbiddenDefault(() =>
            _ = Assert.Throws<InvalidOperationException>(() => Parse([])));
    }

    /// <summary>Malformed arguments retain argument errors ahead of the default guard.</summary>
    [Theory]
    [InlineData("--unknown", "value")]
    [InlineData("--state-path", null)]
    [InlineData("--state-path", "")]
    public void InvalidArgumentsKeepArgumentErrorsBeforeDefaultResolution(string option, string? value)
    {
        string[] args = value is null ? [option] : [option, value];
        WithForbiddenDefault(() =>
            _ = Assert.Throws<ArgumentException>(() => Parse(args)));
    }

    private static (string ManagedRoot, string StatePath) Parse(string[] args)
    {
        Type program = Assembly.Load("NvtFwCombiner.Launcher")
            .GetType("NvtFwCombiner.Launcher.Program", throwOnError: true)!;
        MethodInfo parse = program.GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            return ((string ManagedRoot, string StatePath))parse.Invoke(null, [args])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static void WithForbiddenDefault(Action assertion)
    {
        const string switchName = "NvtFwCombiner.LocalState.CurrentUserFolderForbidden";
        bool wasSet = AppContext.TryGetSwitch(switchName, out bool previous);
        try
        {
            AppContext.SetSwitch(switchName, true);
            assertion();
        }
        finally
        {
            AppContext.SetSwitch(switchName, wasSet && previous);
        }
    }
}
