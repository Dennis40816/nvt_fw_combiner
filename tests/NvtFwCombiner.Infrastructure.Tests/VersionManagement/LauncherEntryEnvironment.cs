using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Environment variables a test process may inherit that change how the actual Launcher entry point starts.</summary>
internal static class LauncherEntryEnvironment
{
    /// <summary>The local state root and every READY or lifetime variable the Launcher entry point reads.</summary>
    internal static readonly string[] InheritedNames = [
        "LOCALAPPDATA",
        AnonymousPipeManagedLauncherProcess.ReadyPipeHandleEnvironment,
        AnonymousPipeManagedLauncherProcess.ExpectedReadyEnvironment,
        AnonymousPipeManagedApplicationProcess.ReadyPipeHandleEnvironment,
        AnonymousPipeManagedApplicationProcess.ExpectedVersionEnvironment,
        ManagedProcessLifetimeLease.ContextEnvironment,
        ManagedProcessLifetimeLease.HandleEnvironment,
        ManagedProcessLifetimeLease.JobEnvironment,
        ManagedProcessLifetimeLease.StatePathEnvironment,
        ManagedProcessLifetimeLease.KindEnvironment,
    ];
}
