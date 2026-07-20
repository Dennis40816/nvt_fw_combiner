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
        DateTimeOffset started = new(2026, 7, 20, 1, 2, 3, TimeSpan.Zero);
        var utcValues = new Queue<DateTimeOffset>([started, started.AddSeconds(3)]);
        StartupTraceSession trace = StartupTraceSession.Create(
            outputPath,
            timestamps.Dequeue,
            utcValues.Dequeue);

        trace.Mark("options.ready");
        bool written = trace.Complete("window.opened");

        Assert.True(written);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(outputPath));
        JsonElement root = document.RootElement;
        Assert.Equal(StartupTraceFileSink.SchemaVersion, root.GetProperty("schemaVersion").GetString());
        JsonElement[] stages = [.. root.GetProperty("stages").EnumerateArray()];
        Assert.Equal(["managed-entry", "options.ready", "window.opened"],
            stages.Select(stage => stage.GetProperty("name").GetString()));
        Assert.Equal(0, stages[0].GetProperty("elapsedMilliseconds").GetDouble());
        Assert.Equal(1000, stages[1].GetProperty("elapsedMilliseconds").GetDouble());
        Assert.Equal(2000, stages[2].GetProperty("elapsedMilliseconds").GetDouble());
        Assert.Equal(1000, stages[2].GetProperty("deltaMilliseconds").GetDouble());
        Assert.Equal(started, root.GetProperty("startedUtc").GetDateTimeOffset());
        Assert.Equal(started.AddSeconds(3), root.GetProperty("completedUtc").GetDateTimeOffset());
    }

    /// <summary>An existing destination remains byte-for-byte unchanged.</summary>
    [Fact]
    public void TraceNeverOverwritesAnExistingFileOrBlocksStartup()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-startup-trace-existing");
        string outputPath = workspace.Write("startup.json", Encoding.UTF8.GetBytes("owner data"));
        StartupTraceSession trace = StartupTraceSession.Create(outputPath);

        bool written = trace.Complete("window.opened");

        Assert.False(written);
        Assert.Equal("owner data", File.ReadAllText(outputPath));
    }

    /// <summary>Normal launches without a trace path keep the recorder disabled.</summary>
    [Fact]
    public void BlankOutputPathKeepsTracingDisabled()
    {
        StartupTraceSession trace = StartupTraceSession.Create("  ");

        trace.Mark("ignored");

        Assert.False(trace.IsEnabled);
        Assert.False(trace.Complete("ignored"));
    }
}
