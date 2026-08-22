using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Launcher;

internal static class Program
{
    private const string SeedStateFileName = "version-manager.seed.v1.json";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            (string managedRoot, string statePath) = Parse(args);
            var stateStore = new JsonVersionManagerStateStore(statePath);
            var repository = new FileSystemManagedVersionRepository();
            ManagedVersionSeedOutcome seedOutcome = new ManagedVersionSeedBootstrapper(
                    managedRoot,
                    stateStore,
                    new JsonVersionManagerStateStore(Path.Combine(managedRoot, SeedStateFileName)),
                    repository)
                .EnsureInitializedAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            if (seedOutcome is not ManagedVersionSeedOutcome.ExistingState and
                not ManagedVersionSeedOutcome.Seeded)
            {
                return 9;
            }
            var coordinator = new ManagedActivationCoordinator(
                managedRoot,
                stateStore,
                repository,
                new AnonymousPipeManagedApplicationProcess());
            ManagedLauncherResult result = coordinator.RunAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
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
