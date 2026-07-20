using System.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Captures opt-in process-local startup milestones without affecting normal launches.</summary>
internal sealed class StartupTraceSession
{
    internal const string OutputPathEnvironmentVariable = "NFC_STARTUP_TRACE_PATH";
    private readonly List<StartupTracePoint>? _points;
    private readonly Func<long> _timestampProvider;
    private readonly Func<DateTimeOffset> _utcNowProvider;
    private readonly long _originTimestamp;
    private readonly DateTimeOffset _startedUtc;
    private readonly string? _outputPath;
    private bool _isComplete;

    private StartupTraceSession(
        string? outputPath,
        Func<long> timestampProvider,
        Func<DateTimeOffset> utcNowProvider,
        long originTimestamp,
        DateTimeOffset startedUtc)
    {
        _outputPath = outputPath;
        _timestampProvider = timestampProvider;
        _utcNowProvider = utcNowProvider;
        _originTimestamp = originTimestamp;
        _startedUtc = startedUtc;
        if (outputPath is not null)
        {
            _points = [new StartupTracePoint("managed-entry", 0)];
        }
    }

    internal static StartupTraceSession Disabled { get; } =
        new(null, Stopwatch.GetTimestamp, GetUtcNow, 0, default);

    internal bool IsEnabled => _points is not null;

    internal static StartupTraceSession StartFromEnvironment()
    {
        return Create(Environment.GetEnvironmentVariable(OutputPathEnvironmentVariable));
    }

    internal static StartupTraceSession Create(
        string? outputPath,
        Func<long>? timestampProvider = null,
        Func<DateTimeOffset>? utcNowProvider = null)
    {
        string? normalizedPath = string.IsNullOrWhiteSpace(outputPath) ? null : outputPath.Trim();
        Func<long> timestamps = timestampProvider ?? Stopwatch.GetTimestamp;
        Func<DateTimeOffset> utcNow = utcNowProvider ?? GetUtcNow;
        return new StartupTraceSession(
            normalizedPath,
            timestamps,
            utcNow,
            timestamps(),
            utcNow());
    }

    internal void Mark(string stage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        if (_points is null || _isComplete)
        {
            return;
        }

        double elapsedMilliseconds = Stopwatch.GetElapsedTime(
            _originTimestamp,
            _timestampProvider()).TotalMilliseconds;
        _points.Add(new StartupTracePoint(stage, elapsedMilliseconds));
    }

    internal bool Complete(string finalStage)
    {
        if (_points is null || _outputPath is null || _isComplete)
        {
            return false;
        }

        Mark(finalStage);
        _isComplete = true;
        return StartupTraceFileSink.TryWrite(
            _outputPath,
            _startedUtc,
            _utcNowProvider(),
            _points);
    }

    private static DateTimeOffset GetUtcNow()
    {
        return DateTimeOffset.UtcNow;
    }
}

internal sealed record StartupTracePoint(string Stage, double ElapsedMilliseconds);
