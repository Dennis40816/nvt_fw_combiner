using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.LauncherBootstrap;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            (string managedRoot, string statePath) = Parse(args);
            return LauncherBootstrapRuntime.RunAsync(managedRoot, statePath, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
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
        string? statePath = null;
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            string value = index + 1 < args.Length
                ? args[++index]
                : throw new ArgumentException("Bootstrap option is missing its value.", nameof(args));
            switch (option)
            {
                case "--managed-root":
                    managedRoot = value;
                    break;
                case "--state-path":
                    statePath = value;
                    break;
                default:
                    throw new ArgumentException("Unknown Bootstrap option.", nameof(args));
            }
        }
        return (Path.GetFullPath(managedRoot), Path.GetFullPath(
            statePath ?? throw new ArgumentException("Bootstrap requires --state-path.", nameof(args))));
    }
}
