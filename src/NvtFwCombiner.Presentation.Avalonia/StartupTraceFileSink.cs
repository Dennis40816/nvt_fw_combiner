using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Writes one diagnostic-only startup trace without replacing an existing file.</summary>
internal static class StartupTraceFileSink
{
    internal const string SchemaVersion = "nfc-startup-trace-v2";

    internal static bool TryWrite(
        string outputPath,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        IReadOnlyList<StartupTracePoint> points)
    {
        try
        {
            Write(outputPath, startedUtc, completedUtc, points);
            return true;
        }
        catch (IOException exception)
        {
            Trace.TraceWarning("Startup trace was not written: {0}", exception.Message);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            Trace.TraceWarning("Startup trace was not written: {0}", exception.Message);
            return false;
        }
        catch (NotSupportedException exception)
        {
            Trace.TraceWarning("Startup trace was not written: {0}", exception.Message);
            return false;
        }
        catch (ArgumentException exception)
        {
            Trace.TraceWarning("Startup trace was not written: {0}", exception.Message);
            return false;
        }
    }

    private static void Write(
        string outputPath,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        IReadOnlyList<StartupTracePoint> points)
    {
        using var stream = new FileStream(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", SchemaVersion);
        writer.WriteNumber("processId", Environment.ProcessId);
        writer.WriteString("runtime", RuntimeInformation.FrameworkDescription);
        writer.WriteString("osArchitecture", RuntimeInformation.OSArchitecture.ToString());
        writer.WriteString("processArchitecture", RuntimeInformation.ProcessArchitecture.ToString());
        writer.WriteString("startedUtc", startedUtc);
        writer.WriteString("completedUtc", completedUtc);
        writer.WriteStartArray("stages");

        double previousElapsed = 0;
        foreach (StartupTracePoint point in points)
        {
            writer.WriteStartObject();
            writer.WriteString("name", point.Stage);
            writer.WriteNumber("elapsedMilliseconds", point.ElapsedMilliseconds);
            writer.WriteNumber("deltaMilliseconds", point.ElapsedMilliseconds - previousElapsed);
            writer.WriteNumber("allocatedBytesSinceManagedEntry", point.AllocatedBytesSinceManagedEntry);
            writer.WriteNumber("allocationDeltaBytes", point.AllocationDeltaBytes);
            writer.WriteEndObject();
            previousElapsed = point.ElapsedMilliseconds;
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }
}
