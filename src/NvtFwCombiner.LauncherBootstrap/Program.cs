using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.LauncherBootstrap;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            LauncherBootstrapLaunchOptions options = LauncherBootstrapLaunchOptions.Parse(args, AppContext.BaseDirectory);
            return LauncherBootstrapRuntime.RunEntryAsync(
                    options.ManagedRoot,
                    options.StatePath,
                    CancellationToken.None)
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

}
