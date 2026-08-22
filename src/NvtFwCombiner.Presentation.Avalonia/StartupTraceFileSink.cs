using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Writes one diagnostic-only startup trace without replacing an existing file.</summary>
internal static class StartupTraceFileSink
{
    internal const string SchemaVersion = "nfc-startup-trace-v3";
    internal static bool TryWrite(
        string outputPath,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        IReadOnlyList<StartupTracePoint> points,
        IReadOnlyList<ShellPreloadStageSnapshot> preloadStages)
    {
        try
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
            writer.WriteStartArray("preloadStages");
            foreach (ShellPreloadStageSnapshot stage in preloadStages)
            {
                writer.WriteStartObject();
                writer.WriteString("id", stage.Id);
                writer.WriteString("state", stage.State.ToString());
                writer.WritePropertyName("completedWork");
                writer.WriteRawValue(stage.CurrentAttempt?.CompletedWork?.ToString(CultureInfo.InvariantCulture) ?? "null");
                writer.WritePropertyName("totalWork");
                writer.WriteRawValue(stage.CurrentAttempt?.TotalWork?.ToString(CultureInfo.InvariantCulture) ?? "null");
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            NotSupportedException or ArgumentException)
        {
            Trace.TraceWarning("Startup trace was not written: {0}", exception.Message);
            return false;
        }
    }

}
