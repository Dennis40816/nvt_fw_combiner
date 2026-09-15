using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Serializes the durable external report only when a client explicitly requests JSON.</summary>
public static class CompositionRunReportJson
{
    /// <summary>Whether persisted data identifies a readable Application run, not verified output.</summary>
    public enum ReadCompleteness
    {
        /// <summary>Keep raw evidence readable without claiming a run outcome.</summary>
        Unknown,
        /// <summary>The legacy identity and issue evidence are present; existing outcome rules apply.</summary>
        Recognized,
    }

    /// <summary>Assesses the Application persisted projection without applying the canonical wire schema.</summary>
    public static ReadCompleteness AssessReadCompleteness(JsonElement root, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ReadCompleteness.Unknown;
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!names.Add(property.Name))
            {
                return ReadCompleteness.Unknown;
            }
        }
        string[] identity = ["ProfileId", "IcId", "ModeId", "ExperienceId", "CompositionKind", "RunId", "StartedAtUtc"];
        if (identity.Any(name => !IsString(root, name) || string.IsNullOrWhiteSpace(root.GetProperty(name).GetString())) ||
            !root.TryGetProperty("Issues", out JsonElement issues) || issues.ValueKind != JsonValueKind.Array)
        {
            return ReadCompleteness.Unknown;
        }
        foreach (JsonElement issue in issues.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (issue.ValueKind != JsonValueKind.Object || !IsString(issue, "Code") || !IsString(issue, "Message"))
            {
                return ReadCompleteness.Unknown;
            }
            names.Clear();
            foreach (JsonProperty property in issue.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!names.Add(property.Name) || (property.Name is "Severity" or "severity" &&
                    property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
                {
                    return ReadCompleteness.Unknown;
                }
            }
            if (issue.TryGetProperty("Severity", out JsonElement severity) &&
                issue.TryGetProperty("severity", out JsonElement legacySeverity) &&
                !string.Equals(severity.GetString(), legacySeverity.GetString(), StringComparison.Ordinal))
            {
                return ReadCompleteness.Unknown;
            }
        }
        return ReadCompleteness.Recognized;
    }

    private static bool IsString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String;
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        // New audit contracts use generated metadata; the existing report graph retains its legacy resolver.
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            AbMergeFormatReportJsonContext.Default,
            new DefaultJsonTypeInfoResolver()),
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
