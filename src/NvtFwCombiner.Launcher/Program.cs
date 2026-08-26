using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Launcher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            (string managedRoot, string statePath) = Parse(args);
            LauncherReadyInheritance outerReady = LauncherBootstrapRuntime.CaptureNestedReadyContext();
            if (outerReady.Outcome == LauncherReadyInheritanceOutcome.InvalidInheritedContext)
            {
                return 16;
            }
            var stateStore = new JsonVersionManagerStateStore(statePath);
            var repository = new FileSystemManagedVersionRepository();
            var coordinator = new ManagedActivationCoordinator(
                managedRoot,
                stateStore,
                repository,
                new AnonymousPipeManagedApplicationProcess(statePath));
            ManagedLauncherResult result = coordinator.RunAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (result.Outcome is ManagedLauncherOutcome.Ready or ManagedLauncherOutcome.RolledBack)
            {
                if (outerReady.Outcome == LauncherReadyInheritanceOutcome.Inherited &&
                    !LauncherBootstrapRuntime.ReportNestedReadyAsync(
                            outerReady,
                            managedRoot,
                            statePath,
                            CancellationToken.None)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult())
                {
                    return 16;
                }
            }
            return result.Outcome switch
            {
                ManagedLauncherOutcome.Ready => 0,
                ManagedLauncherOutcome.RolledBack => 1,
                ManagedLauncherOutcome.InvalidState => 10,
                ManagedLauncherOutcome.NoActiveVersion => 11,
                ManagedLauncherOutcome.DamagedVersion => 12,
                ManagedLauncherOutcome.StartFailed => 13,
                ManagedLauncherOutcome.StateUnavailable => 14,
                ManagedLauncherOutcome.Busy => 15,
                ManagedLauncherOutcome.TerminationUnconfirmed =>
                    LauncherBootstrapRuntime.UnconfirmedTerminationExitCode,
                _ => 99,
            };
        }
        catch (ArgumentException)
        {
            return 20;
        }
        catch (InvalidOperationException)
        {
            return 21;
        }
    }

    private static (string ManagedRoot, string StatePath) Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string managedRoot = AppContext.BaseDirectory;
        string statePath = JsonVersionManagerStateStore.GetDefaultPath();
        for (int index = 0; index < args.Length; index++)
        {
            string value = index + 1 < args.Length
                ? args[++index]
                : throw new ArgumentException("Launcher option is missing its value.", nameof(args));
            switch (args[index - 1])
            {
                case "--managed-root":
                    managedRoot = value;
                    break;
                case "--state-path":
                    statePath = value;
                    break;
                default:
                    throw new ArgumentException("Unknown launcher option.", nameof(args));
            }
        }
        return (Path.GetFullPath(managedRoot), Path.GetFullPath(statePath));
    }
}
