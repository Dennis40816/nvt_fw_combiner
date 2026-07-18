using System.Globalization;
using System.Text.Json;

namespace NvtFwCombiner.PerformanceProbe;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            ProbeOptions options = ProbeOptions.Parse(args);
            if (options.ShowHelp)
            {
                Console.WriteLine(ProbeOptions.HelpText);
                return 0;
            }

            PerformanceProbeReport report = await PerformanceProbe
                .RunAsync(options, CancellationToken.None)
                .ConfigureAwait(false);
            string json = JsonSerializer.Serialize(report, JsonOptions);
            Console.WriteLine(json);
            if (options.OutputPath is not null)
            {
                WriteNewEvidenceFile(options.OutputPath, json);
            }

            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Performance probe failed: {exception.Message}");
            return 1;
        }
    }

    private static void WriteNewEvidenceFile(string outputPath, string json)
    {
        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(json);
        writer.WriteLine();
        Console.Error.WriteLine($"Performance evidence written to {fullPath}");
    }
}

internal sealed record ProbeOptions(
    int WarmupCount,
    int IterationCount,
    int HeartbeatIntervalMilliseconds,
    string? OutputPath,
    bool ShowHelp)
{
    internal const string HelpText = """
        NVT FW Combiner local performance probe

        Usage:
          dotnet run --project tools/NvtFwCombiner.PerformanceProbe -c Release -- [options]

        Options:
          --warmup <count>       Warm-up runs before the measured warm series (default: 2).
          --iterations <count>   Measured warm runs (default: 10).
          --heartbeat-ms <ms>    Dispatcher heartbeat interval (default: 2).
          --output <path>        Write JSON evidence to a new file; existing files are preserved.
          --help                 Show this help.
        """;

    internal static ProbeOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        int warmupCount = 2;
        int iterationCount = 10;
        int heartbeatIntervalMilliseconds = 2;
        string? outputPath = null;
        bool showHelp = false;
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--warmup":
                    warmupCount = ParsePositiveInt(ReadValue(args, ref index, argument), argument, allowZero: true);
                    break;
                case "--iterations":
                    iterationCount = ParsePositiveInt(ReadValue(args, ref index, argument), argument, allowZero: false);
                    break;
                case "--heartbeat-ms":
                    heartbeatIntervalMilliseconds = ParsePositiveInt(ReadValue(args, ref index, argument), argument, allowZero: false);
                    break;
                case "--output":
                    outputPath = ReadValue(args, ref index, argument);
                    break;
                case "--help" or "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown performance-probe option '{argument}'.");
            }
        }

        return new ProbeOptions(
            warmupCount,
            iterationCount,
            heartbeatIntervalMilliseconds,
            outputPath,
            showHelp);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        return index + 1 < args.Length
            ? args[++index]
            : throw new ArgumentException($"Option '{option}' requires a value.");
    }

    private static int ParsePositiveInt(string value, string option, bool allowZero)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) ||
            result < (allowZero ? 0 : 1))
        {
            string requirement = allowZero ? "a non-negative integer" : "a positive integer";
            throw new ArgumentException($"Option '{option}' requires {requirement}.");
        }

        return result;
    }
}
