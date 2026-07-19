using System.Text;
using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    internal static JsonValueSlice[] IndexOutputDifferences(
        ReadOnlySpan<byte> reportUtf8,
        CancellationToken cancellationToken)
    {
        var reader = new Utf8JsonReader(reportUtf8);
        JsonValueSlice[] slices = [];
        int cancellationStride = 0;
        while (reader.Read())
        {
            if ((cancellationStride++ & 0xff) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (reader.TokenType != JsonTokenType.PropertyName ||
                reader.CurrentDepth != 1 ||
                !reader.ValueTextEquals(nameof(OutputDifferences)))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!reader.Read())
            {
                break;
            }

            slices = reader.TokenType == JsonTokenType.StartArray
                ? ReadJsonArraySlices(ref reader, cancellationToken)
                : [];
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                SkipJsonValue(ref reader, cancellationToken);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return AddCharBounds(reportUtf8, slices, cancellationToken);
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
                SkipJsonValue(ref reader, cancellationToken);
            }

            int endExclusive = checked((int)reader.BytesConsumed);
            slices.Add(new JsonValueSlice(start, checked(endExclusive - start), 0, 0));
        }

        return [.. slices];
    }

    private static JsonValueSlice[] AddCharBounds(
        ReadOnlySpan<byte> reportUtf8,
        JsonValueSlice[] slices,
        CancellationToken cancellationToken)
    {
        int byteCursor = 0;
        int charCursor = 0;
        for (int index = 0; index < slices.Length; index++)
        {
            if ((index & 0xff) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            JsonValueSlice slice = slices[index];
            int leadingByteCount = checked(slice.Start - byteCursor);
            charCursor = checked(
                charCursor + Encoding.UTF8.GetCharCount(reportUtf8.Slice(byteCursor, leadingByteCount)));
            int charLength = Encoding.UTF8.GetCharCount(reportUtf8.Slice(slice.Start, slice.Length));
            slices[index] = slice with { CharStart = charCursor, CharLength = charLength };
            byteCursor = checked(slice.Start + slice.Length);
            charCursor = checked(charCursor + charLength);
        }

        return slices;
    }

    internal static void SkipJsonValue(
        ref Utf8JsonReader reader,
        CancellationToken cancellationToken)
    {
        if (reader.TokenType is not JsonTokenType.StartObject and not JsonTokenType.StartArray)
        {
            throw new InvalidOperationException("The JSON reader is not positioned on a compound value.");
        }

        JsonTokenType endToken = reader.TokenType == JsonTokenType.StartObject
            ? JsonTokenType.EndObject
            : JsonTokenType.EndArray;
        int valueDepth = reader.CurrentDepth;
        int cancellationStride = 0;
        cancellationToken.ThrowIfCancellationRequested();
        while (reader.Read())
        {
            if ((cancellationStride++ & 0xff) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (reader.TokenType == endToken && reader.CurrentDepth == valueDepth)
            {
                return;
            }
        }

        throw new JsonException("The JSON compound value ended before its closing token.");
    }

    internal readonly record struct JsonValueSlice(int Start, int Length, int CharStart, int CharLength);
}
