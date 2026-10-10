using System.Diagnostics.CodeAnalysis;

namespace NvtFwCombiner.DistributionLauncher;

internal sealed class LauncherUiEventFailureReporter(Action<string> showOutcome)
{
    private readonly Action<string> _showOutcome = showOutcome ?? throw new ArgumentNullException(nameof(showOutcome));

    internal void Report(string operation, Exception exception)
    {
        _showOutcome($"Unexpected error during {operation}: {exception.Message}");
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The last-resort diagnostic sink must not fail on the UI dispatcher.")]
    internal static void ReportEmergency(string operation, Exception exception)
    {
        try
        {
            Console.Error.WriteLine($"UI event '{operation}' failed: {exception}");
        }
        catch (Exception)
        {
        }
    }
}
