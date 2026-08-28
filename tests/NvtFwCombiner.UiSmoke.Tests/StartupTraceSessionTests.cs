using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Locks opt-in startup trace timing and filesystem safety.</summary>
public sealed class StartupTraceSessionTests
{
    /// <summary>Writes deterministic ordered milestones and relative durations.</summary>
    [Fact]
    public void EnabledTraceWritesOrderedDeterministicStages()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-startup-trace");
        string outputPath = workspace.PathFor("startup.json");
        long origin = 1234;
        var timestamps = new Queue<long>(
            [origin, origin + Stopwatch.Frequency, origin + (2 * Stopwatch.Frequency)]);
        var allocations = new Queue<long>([100, 260, 500]);
        DateTimeOffset started = new(2026, 7, 20, 1, 2, 3, TimeSpan.Zero);
        var utcValues = new Queue<DateTimeOffset>([started, started.AddSeconds(3)]);
        var trace = StartupTraceSession.Create(
            outputPath,
            timestamps.Dequeue,
            utcValues.Dequeue,
            allocations.Dequeue);
        ShellPreloadStageSnapshot[] preloadStages =
        [
            Stage("canonical-catalog", 1, 2, true),
            Stage("deferred-views", 2, 2, false, 5, 5),
        ];

        trace.Mark("options.ready");
        bool written = trace.Complete("window.opened", preloadStages);

        Assert.True(written);
        using var document = JsonDocument.Parse(File.ReadAllBytes(outputPath));
        JsonElement root = document.RootElement;
        Assert.Equal(StartupTraceFileSink.SchemaVersion, root.GetProperty("schemaVersion").GetString());
        JsonElement[] stages = [.. root.GetProperty("stages").EnumerateArray()];
        Assert.Equal(["managed-entry", "options.ready", "window.opened"],
            stages.Select(stage => stage.GetProperty("name").GetString()));
        Assert.Equal(0, stages[0].GetProperty("elapsedMilliseconds").GetDouble());
        Assert.Equal(1000, stages[1].GetProperty("elapsedMilliseconds").GetDouble());
        Assert.Equal(2000, stages[2].GetProperty("elapsedMilliseconds").GetDouble());
        Assert.Equal(1000, stages[2].GetProperty("deltaMilliseconds").GetDouble());
        Assert.Equal(0, stages[0].GetProperty("allocatedBytesSinceManagedEntry").GetInt64());
        Assert.Equal(160, stages[1].GetProperty("allocatedBytesSinceManagedEntry").GetInt64());
        Assert.Equal(400, stages[2].GetProperty("allocatedBytesSinceManagedEntry").GetInt64());
        Assert.Equal(240, stages[2].GetProperty("allocationDeltaBytes").GetInt64());
        JsonElement[] lifecycle = [.. root.GetProperty("preloadStages").EnumerateArray()];
        Assert.Equal(["canonical-catalog", "deferred-views"],
            lifecycle.Select(stage => stage.GetProperty("id").GetString()));
        Assert.Equal("Succeeded", lifecycle[0].GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, lifecycle[0].GetProperty("completedWork").ValueKind);
        Assert.Equal(JsonValueKind.Null, lifecycle[0].GetProperty("totalWork").ValueKind);
        Assert.Equal(5, lifecycle[1].GetProperty("completedWork").GetInt64());
        Assert.Equal(5, lifecycle[1].GetProperty("totalWork").GetInt64());
        Assert.Equal(started, root.GetProperty("startedUtc").GetDateTimeOffset());
        Assert.Equal(started.AddSeconds(3), root.GetProperty("completedUtc").GetDateTimeOffset());
    }

    private static ShellPreloadStageSnapshot Stage(
        string id,
        int index,
        int count,
        bool isRequired,
        long? completedWork = null,
        long? totalWork = null)
    {
        var identity = new ShellPreloadAttemptIdentity(1, id, 1);
        var attempt = new ShellPreloadAttemptSnapshot(
            identity, ShellPreloadStageState.Succeeded, 1, completedWork, totalWork);
        return new(id, index, count, isRequired, id, "", "", "", false,
            ShellPreloadStageState.Succeeded, attempt, null);
    }

    /// <summary>An existing destination remains byte-for-byte unchanged.</summary>
    [Fact]
    public void TraceNeverOverwritesAnExistingFileOrBlocksStartup()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-startup-trace-existing");
        string outputPath = workspace.Write("startup.json", Encoding.UTF8.GetBytes("owner data"));
        var trace = StartupTraceSession.Create(outputPath);

        bool written = trace.Complete("window.opened");

        Assert.False(written);
        Assert.Equal("owner data", File.ReadAllText(outputPath));
    }

    /// <summary>Normal launches without a trace path keep the recorder disabled.</summary>
    [Fact]
    public void BlankOutputPathKeepsTracingDisabled()
    {
        var trace = StartupTraceSession.Create(
            "  ",
            static () => throw new InvalidOperationException("timestamp provider should stay idle"),
            static () => throw new InvalidOperationException("clock provider should stay idle"),
            static () => throw new InvalidOperationException("allocation provider should stay idle"));

        trace.Mark("ignored");

        Assert.False(trace.IsEnabled);
        Assert.False(trace.Complete("ignored"));
    }

    /// <summary>A normal launch measures managed-entry-to-ready time without enabling trace output.</summary>
    [Fact]
    public void NormalLaunchKeepsOneMonotonicDurationWithoutTraceOutput()
    {
        long origin = 4321;
        var timestamps = new Queue<long>(
            [origin, origin + (3 * Stopwatch.Frequency / 2)]);
        StartupTraceSession trace = StartupTraceSession.Create(
            outputPath: null,
            timestamps.Dequeue,
            static () => DateTimeOffset.UnixEpoch,
            static () => 0,
            measureWithoutOutput: true);

        TimeSpan elapsed = Assert.IsType<TimeSpan>(trace.ElapsedSinceManagedEntry);

        Assert.False(trace.IsEnabled);
        Assert.Equal(1500, elapsed.TotalMilliseconds);
        Assert.False(trace.Complete("ignored"));
    }
}
