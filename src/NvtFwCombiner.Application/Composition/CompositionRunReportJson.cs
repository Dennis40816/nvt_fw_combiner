using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Serializes the durable external report only when a client explicitly requests JSON.</summary>
public static class CompositionRunReportJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serializes one typed result using the existing composition-report-v1 projection.</summary>
    public static string Serialize(CompositionRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result.Report, Options);
    }

    /// <summary>Serializes one typed plan-only report while explicitly omitting an output artifact.</summary>
    public static string SerializeDiagnosticPreview(CompositionRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.DiagnosticPreview is null)
        {
            throw new ArgumentException(
                "A diagnostic Preview report requires its explicit plan-only marker.",
                nameof(report));
        }

        JsonObject projection = JsonSerializer.SerializeToNode(report, Options)!.AsObject();
        projection[nameof(CompositionRunReport.Output)] = null;
        return projection.ToJsonString(Options);
    }
}
