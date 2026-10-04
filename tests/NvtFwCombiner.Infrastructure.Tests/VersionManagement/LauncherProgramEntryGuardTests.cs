using System.Reflection;
using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Exercises actual Launcher entry guards before state or application startup.</summary>
[Collection(nameof(ReadyProbeProcessSerialGroup))]
public sealed class LauncherProgramEntryGuardTests
{
    /// <summary>Argument rejection precedes forbidden default state and invalid inherited contexts.</summary>
    [Fact]
    public void InvalidArgumentsExit20BeforeDefaultStateAndInheritedContext()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        WithIsolatedContext(workspace, () =>
        {
            Environment.SetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment, "invalid");
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment, "invalid");

            Assert.Equal(20, InvokeMain(["--managed-root", workspace.PathFor("managed root"), "--unknown", "value"]));
            Assert.Equal("invalid", Environment.GetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment));
            Assert.Equal("invalid", Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment));
        });
    }

    /// <summary>Omitting an explicit state path rejects the forbidden default before inherited contexts.</summary>
    [Fact]
    public void ForbiddenDefaultStateExits21BeforeInheritedContext()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        WithIsolatedContext(workspace, () =>
        {
            Environment.SetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment, "invalid");
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment, "invalid");

            Assert.Equal(21, InvokeMain(["--managed-root", workspace.PathFor("managed root")]));
            Assert.Equal("invalid", Environment.GetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment));
            Assert.Equal("invalid", Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment));
        });
    }

    /// <summary>Invalid outer READY is consumed and rejected before the inherited lifetime is inspected.</summary>
    [Fact]
    public void InvalidOuterReadyExits16BeforeInheritedLifetimeAndStartup()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        WithIsolatedContext(workspace, () =>
        {
            Environment.SetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment, "invalid");
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment, "invalid");

            Assert.Equal(16, InvokeMain([
                "--managed-root", workspace.PathFor("managed root"),
                "--state-path", workspace.PathFor("state/version-manager.v1.json"),
            ]));
            Assert.Null(Environment.GetEnvironmentVariable(AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment));
            Assert.Equal("invalid", Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment));
        });
    }

    /// <summary>With outer READY absent, invalid inherited lifetime is consumed and rejected before startup.</summary>
    [Fact]
    public void InvalidInheritedLifetimeExits22BeforeStartup()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        WithIsolatedContext(workspace, () =>
        {
            Environment.SetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment, "invalid");

            Assert.Equal(22, InvokeMain([
                "--managed-root", workspace.PathFor("managed root"),
                "--state-path", workspace.PathFor("state/version-manager.v1.json"),
            ]));
            Assert.Null(Environment.GetEnvironmentVariable(ManagedProcessLifetimeLease.ContextEnvironment));
        });
    }

    private static int InvokeMain(string[] args)
    {
        Type program = Assembly.Load("NvtFwCombiner.Launcher")
            .GetType("NvtFwCombiner.Launcher.Program", throwOnError: true)!;
        MethodInfo main = program.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            return (int)main.Invoke(null, [args])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static void WithIsolatedContext(TempWorkspace workspace, Action assertion)
    {
        string[] environmentNames = LauncherEntryEnvironment.InheritedNames;
        Dictionary<string, string?> previousEnvironment = environmentNames.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);
        bool wasSet = AppContext.TryGetSwitch(
            JsonVersionManagerStateStore.CurrentUserLocalStateForbiddenSwitch,
            out bool previousSwitch);
        try
        {
            foreach (string name in environmentNames)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            Environment.SetEnvironmentVariable("LOCALAPPDATA", workspace.PathFor("local app data"));
            AppContext.SetSwitch(JsonVersionManagerStateStore.CurrentUserLocalStateForbiddenSwitch, true);

            assertion();

            // State startup acquires a writer lease, which would create the state directory and lock file.
            Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.Root));
        }
        finally
        {
            foreach ((string name, string? value) in previousEnvironment)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
            AppContext.SetSwitch(
                JsonVersionManagerStateStore.CurrentUserLocalStateForbiddenSwitch,
                wasSet && previousSwitch);
        }
    }
}
