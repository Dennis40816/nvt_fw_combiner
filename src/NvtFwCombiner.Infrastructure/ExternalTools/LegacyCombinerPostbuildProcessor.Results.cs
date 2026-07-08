using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

public sealed partial class LegacyCombinerPostbuildProcessor
{
    private static ExternalProcessorResult CreateCheckedSuccess(
        ReadOnlyMemory<byte> inputBytes,
        ReadOnlyMemory<byte> outputBytes,
        IReadOnlyList<ByteRange> allowedWriteRanges)
    {
        IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(inputBytes.Span, outputBytes.Span);
        ChangedRangeVerdict verdict = new ChangedRangePolicy(allowedWriteRanges).Evaluate(changedRanges);
        return verdict.IsAllowed
            ? ExternalProcessorResult.Success(outputBytes, changedRanges)
            : ExternalProcessorResult.Failed([
                new CompositionIssue(
                    "external-tool.write-range.violation",
                    $"External processor changed bytes outside declared write ranges: {FormatRanges(verdict.ViolatingRanges)}."),
            ]);
    }

    private static ExternalProcessorResult Fail(string code, string message)
    {
        return ExternalProcessorResult.Failed([new CompositionIssue(code, message)]);
    }

    private static string FormatProcessOutput(ExternalProcessResult processResult)
    {
        string stderr = Shorten(processResult.StandardError);
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return $"stderr: {stderr}";
        }

        string stdout = Shorten(processResult.StandardOutput);
        return string.IsNullOrWhiteSpace(stdout)
            ? "No process output was captured."
            : $"stdout: {stdout}";
    }

    private static string FormatRanges(IReadOnlyList<ByteRange> ranges)
    {
        return ranges.Count == 0
            ? "none"
            : string.Join(
                ", ",
                ranges
                    .Take(12)
                    .Select(range => FormattableString.Invariant(
                        $"0x{range.Start:X}-0x{range.EndExclusive - 1:X} (len 0x{range.Length:X})"))) +
            (ranges.Count > 12
                ? FormattableString.Invariant($" ... {ranges.Count - 12} more")
                : string.Empty);
    }

    private static string Shorten(string value)
    {
        string compact = value.ReplaceLineEndings(" ").Trim();
        return compact.Length <= 240 ? compact : $"{compact[..240]}...";
    }
}
