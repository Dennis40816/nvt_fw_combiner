using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static ReportHexDiffRangeViewModel ParseHexDiffRange(
        string reportJson,
        JsonValueSlice slice,
        ReportHexDiffRangeDescriptor descriptor,
        string outputSpaceId,
        long outputSize,
        ShellLanguage language)
    {
        using var document = JsonDocument.Parse(reportJson.AsMemory(slice.CharStart, slice.CharLength));
        JsonElement difference = document.RootElement;
        ReportLineViewModel detail = ParseOutputDifference(difference, language);
        OutputDifferenceReplaySegment? replay = ParseHexDiffReplay(
            difference,
            descriptor,
            outputSize);
        return new ReportHexDiffRangeViewModel(
            descriptor,
            detail,
            outputSpaceId,
            language,
            replay);
    }

    private static ReportHexDiffRangeViewModel ProjectHexDiffRange(
        OutputDifferenceSummary difference,
        ReportHexDiffRangeDescriptor descriptor,
        string outputSpaceId,
        long outputSize,
        ShellLanguage language)
    {
        OutputDifferenceReplaySegment? replay = difference.Replay is { } candidate &&
            candidate.MatchesPersistableAlignedContext(outputSize, difference.Range) &&
            candidate.MatchesDifferenceEvidence(
                difference.Range,
                difference.ChangedByteCount,
                difference.BeforeSha256,
                difference.AfterSha256)
                ? candidate
                : null;
        return new ReportHexDiffRangeViewModel(
            descriptor,
            ProjectOutputDifference(difference, language),
            outputSpaceId,
            language,
            replay);
    }

    private static OutputDifferenceReplaySegment? ParseHexDiffReplay(
        JsonElement difference,
        ReportHexDiffRangeDescriptor descriptor,
        long outputSize)
    {
        if (!difference.TryGetProperty("Replay", out JsonElement replay) ||
            replay.ValueKind != JsonValueKind.Object ||
            !TryGetHexDiffRange(replay, out long start, out long length, out long endExclusive) ||
            start < 0 || length <= 0 || start > long.MaxValue - length ||
            endExclusive != start + length ||
            start % HexViewportSnapshot.BytesPerRow != 0)
        {
            return null;
        }

        string? beforeBase64 = GetStringOrNull(replay, "BeforeBytes");
        string? afterBase64 = GetStringOrNull(replay, "AfterBytes");
        string? replayBeforeSha256 = GetStringOrNull(replay, "BeforeSha256");
        string? replayAfterSha256 = GetStringOrNull(replay, "AfterSha256");
        string? differenceBeforeSha256 = GetStringOrNull(difference, "BeforeSha256");
        string? differenceAfterSha256 = GetStringOrNull(difference, "AfterSha256");
        if (string.IsNullOrWhiteSpace(beforeBase64) ||
            string.IsNullOrWhiteSpace(afterBase64) ||
            string.IsNullOrWhiteSpace(replayBeforeSha256) ||
            string.IsNullOrWhiteSpace(replayAfterSha256) ||
            string.IsNullOrWhiteSpace(differenceBeforeSha256) ||
            string.IsNullOrWhiteSpace(differenceAfterSha256))
        {
            return null;
        }

        try
        {
            if (descriptor.Start < start ||
                descriptor.Length > length ||
                descriptor.Start > endExclusive - descriptor.Length)
            {
                return null;
            }

            var segment = new OutputDifferenceReplaySegment(
                start,
                Convert.FromBase64String(beforeBase64),
                Convert.FromBase64String(afterBase64));
            return string.Equals(segment.BeforeSha256, replayBeforeSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segment.AfterSha256, replayAfterSha256, StringComparison.OrdinalIgnoreCase) &&
                segment.MatchesPersistableAlignedContext(
                    outputSize,
                    descriptor.Start,
                    descriptor.Length) &&
                segment.MatchesDifferenceEvidence(
                    descriptor.Start,
                    descriptor.Length,
                    descriptor.ChangedByteCount,
                    differenceBeforeSha256,
                    differenceAfterSha256)
                ? segment
                : null;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return null;
        }
    }
}
