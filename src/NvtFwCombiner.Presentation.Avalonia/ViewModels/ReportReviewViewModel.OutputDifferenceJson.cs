using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static JsonValueSlice[] IndexOutputDifferences(
        ReadOnlySpan<byte> reportUtf8,
        CancellationToken cancellationToken)
    {
        var reader = new Utf8JsonReader(reportUtf8);
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName ||
                reader.CurrentDepth != 1 ||
                !reader.ValueTextEquals(nameof(OutputDifferences)))
            {
                continue;
            }

            return reader.Read() && reader.TokenType == JsonTokenType.StartArray
                ? ReadJsonArraySlices(ref reader, cancellationToken)
                : [];
        }

        return [];
    }

    private static JsonValueSlice[] ReadJsonArraySlices(
        ref Utf8JsonReader reader,
        CancellationToken cancellationToken)
    {
        var slices = new List<JsonValueSlice>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int start = checked((int)reader.TokenStartIndex);
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                reader.Skip();
            }

            int endExclusive = checked((int)reader.BytesConsumed);
            slices.Add(new JsonValueSlice(start, checked(endExclusive - start)));
        }

        return [.. slices];
    }

    private readonly record struct JsonValueSlice(int Start, int Length);
}
