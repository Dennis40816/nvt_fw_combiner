namespace NvtFwCombiner.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (NvtFwCombiner.Bootstrap.CompositionHostServices.TryHandleRuntimeTrustProbe(
                args,
                Console.Out,
                out int probeExitCode))
        {
            return probeExitCode;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = HandleCancel;
        Console.CancelKeyPress += cancelHandler;
        try
        {
            return await CliApplication
                .RunAsync(args, Console.Out, Console.Error, cancellation.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }

        void HandleCancel(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }
    }
}
