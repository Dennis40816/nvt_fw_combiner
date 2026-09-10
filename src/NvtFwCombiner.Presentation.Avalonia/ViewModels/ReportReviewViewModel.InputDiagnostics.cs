using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReportReviewViewModel
{
    private static Dictionary<int, InputDiagnosticEvidence?> IndexInputDiagnostics(
        IReadOnlyList<InputDiagnosticSummary>? entries, int issueCount)
    {
        var indexed = new Dictionary<int, InputDiagnosticEvidence?>();
        foreach (InputDiagnosticSummary entry in entries ?? [])
        {
            if (entry.IssueIndex >= 0 && entry.IssueIndex < issueCount &&
                !indexed.TryAdd(entry.IssueIndex, entry.Evidence))
            {
                // Ambiguous evidence is ignored, never guessed from code or message equality.
                indexed[entry.IssueIndex] = null;
            }
        }
        return indexed;
    }

    private static Dictionary<int, InputDiagnosticEvidence?> ParseInputDiagnostics(
        JsonElement root, int issueCount, CancellationToken cancellationToken)
    {
        var indexed = new Dictionary<int, InputDiagnosticEvidence?>();
        if (!root.TryGetProperty("InputDiagnostics", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return indexed;
        }
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("IssueIndex", out JsonElement indexValue) ||
                indexValue.ValueKind != JsonValueKind.Number || !indexValue.TryGetInt32(out int index) || index < 0 || index >= issueCount)
            {
                continue;
            }
            if (!indexed.TryAdd(index, null))
            {
                indexed[index] = null;
                continue;
            }
            try
            {
                if (string.IsNullOrWhiteSpace(GetStringOrNull(entry, "SlotId")) ||
                    !entry.TryGetProperty("Evidence", out JsonElement evidence) || evidence.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                ByteRange? range = null;
                if (evidence.TryGetProperty("SourceRange", out JsonElement rangeValue) && rangeValue.ValueKind != JsonValueKind.Null)
                {
                    range = new ByteRange(rangeValue.GetProperty("Start").GetInt64(), rangeValue.GetProperty("Length").GetInt64());
                    if (rangeValue.TryGetProperty("EndExclusive", out JsonElement end) && end.GetInt64() != range.Value.EndExclusive)
                    {
                        continue;
                    }
                }
                long? repeatedByte = DiagnosticLong(evidence, "RepeatedByte");
                indexed[index] = new InputDiagnosticEvidence(GetString(evidence, "AddressSpaceId"),
                    DiagnosticLong(evidence, "ActualLength"), DiagnosticLong(evidence, "RequiredEndExclusive"), range,
                    repeatedByte is { } value ? checked((byte)value) : null);
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException or
                InvalidOperationException or KeyNotFoundException or OverflowException or FormatException)
            {
                // Optional display evidence cannot prevent opening the original run report.
            }
        }
        return indexed;
    }

    private static long? DiagnosticLong(JsonElement value, string property)
    {
        return !value.TryGetProperty(property, out JsonElement number) || number.ValueKind == JsonValueKind.Null
            ? null : number.GetInt64();
    }
}
