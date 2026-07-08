using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private const int OutputDifferenceHexPreviewBytes = 32;

    private static List<OutputDifferenceSummary> CreateOutputDifferences(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        byte[] outputBytes)
    {
        if (execution.Status != CompositionExecutionStatus.Succeeded ||
            request.Profile.CompositionKind != CompositionKind.Replace ||
            request.Plan.Initialization.Kind != ImageInitializationKind.Reference ||
            request.Plan.Initialization.ReferenceSpaceId is not { } referenceSpaceId ||
            !inputBytes.TryGetValue(referenceSpaceId, out byte[]? referenceBytes) ||
            referenceBytes.Length != outputBytes.Length)
        {
            return [];
        }

        IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(referenceBytes, outputBytes);
        if (changedRanges.Count == 0)
        {
            return [];
        }

        OutputDifferenceExpectation[] expectations = [.. CreateOutputDifferenceExpectations(request)];
        List<OutputDifferenceSummary> rows = [];
        int rowIndex = 0;
        foreach (ByteRange changedRange in changedRanges)
        {
            foreach (ByteRange segment in SplitRangeByExpectations(changedRange, expectations))
            {
                OutputDifferenceExpectation expectation = ClassifyDifferenceSegment(segment, expectations);
                rows.Add(new OutputDifferenceSummary(
                    FormattableString.Invariant($"diff-{++rowIndex:D3}"),
                    segment,
                    segment.Length,
                    expectation.Classification,
                    expectation.IsAccepted,
                    expectation.Evidence,
                    expectation.Explanation,
                    expectation.SectionLabel,
                    ToSliceSha256Hex(referenceBytes, segment),
                    ToSliceSha256Hex(outputBytes, segment),
                    ToSliceHexPreview(referenceBytes, segment),
                    ToSliceHexPreview(outputBytes, segment),
                    checked((int)Math.Min(segment.Length, OutputDifferenceHexPreviewBytes)),
                    segment.Length <= OutputDifferenceHexPreviewBytes));
            }
        }

        return rows;
    }

    private static IEnumerable<OutputDifferenceExpectation> CreateOutputDifferenceExpectations(
        CompositionRunRequest request)
    {
        string targetSpaceId = request.Plan.Initialization.TargetSpaceId;
        string? referenceSpaceId = request.Plan.Initialization.ReferenceSpaceId;
        string icNumber = request.IcNumberSelection?.ToStableToken() ?? "unspecified";
        foreach (CompositionOperation operation in request.Plan.OrderedOperations)
        {
            if (!string.Equals(operation.TargetSpaceId, targetSpaceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (operation.Kind == CompositionOperationKind.RunExternalProcessor &&
                operation.ExternalProcessorInvocation is { } invocation)
            {
                ByteRange[] stagedRanges = [.. invocation.StagedSourceBindings.Select(binding => binding.FirmwareRange)];
                foreach (ExternalProcessorStagedSourceBinding binding in invocation.StagedSourceBindings)
                {
                    yield return new OutputDifferenceExpectation(
                        binding.FirmwareRange,
                        OutputDifferenceClassifications.DeclaredReplacement,
                        true,
                        operation.OperationId,
                        $"Accepted: staged replacement source '{binding.SourceSpaceId}' is pasted back by postbuild for {request.Profile.IcId} / {icNumber}.",
                        FormatDifferenceSectionLabel(binding.SourceSpaceId));
                }

                foreach (ByteRange allowedWriteRange in invocation.AllowedWriteRanges)
                {
                    foreach (ByteRange processorOnlyRange in SubtractRanges(allowedWriteRange, stagedRanges))
                    {
                        string sectionLabel = FormatProcessorWriteSectionLabel(invocation, processorOnlyRange);
                        yield return new OutputDifferenceExpectation(
                            processorOnlyRange,
                            OutputDifferenceClassifications.PostbuildCrcHeader,
                            true,
                            $"{operation.OperationId}: {invocation.ProcessorId}",
                            $"Accepted: this range is inside the {request.Profile.IcId} / {icNumber} approved {sectionLabel} postbuild write ranges.",
                            sectionLabel);
                    }
                }

                continue;
            }

            if (IsReferencePreserveOperation(operation, referenceSpaceId))
            {
                yield return new OutputDifferenceExpectation(
                    operation.TargetRange,
                    OutputDifferenceClassifications.PreservedReference,
                    false,
                    operation.OperationId,
                    "Unexpected: this range is declared to be restored from the reference base.",
                    "Reference-preserved range");
                continue;
            }

            yield return new OutputDifferenceExpectation(
                operation.TargetRange,
                OutputDifferenceClassifications.DeclaredReplacement,
                true,
                operation.OperationId,
                $"Accepted: declared Replace mapping '{operation.OperationId}' writes this final range.",
                "Declared replacement");
        }
    }

    private static bool IsReferencePreserveOperation(CompositionOperation operation, string? referenceSpaceId)
    {
        return referenceSpaceId is not null &&
            operation.Kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange &&
            string.Equals(operation.SourceSpaceId, referenceSpaceId, StringComparison.Ordinal) &&
            operation.SourceRange == operation.TargetRange;
    }

    private static IEnumerable<ByteRange> SplitRangeByExpectations(
        ByteRange changedRange,
        IReadOnlyList<OutputDifferenceExpectation> expectations)
    {
        SortedSet<long> points = [changedRange.Start, changedRange.EndExclusive];
        foreach (OutputDifferenceExpectation expectation in expectations)
        {
            ByteRange? overlap = changedRange.Intersect(expectation.Range);
            if (overlap is null)
            {
                continue;
            }

            _ = points.Add(overlap.Value.Start);
            _ = points.Add(overlap.Value.EndExclusive);
        }

        long[] ordered = [.. points];
        for (int index = 0; index < ordered.Length - 1; index++)
        {
            yield return ByteRange.FromStartEndExclusive(ordered[index], ordered[index + 1]);
        }
    }

    private static OutputDifferenceExpectation ClassifyDifferenceSegment(
        ByteRange segment,
        IReadOnlyList<OutputDifferenceExpectation> expectations)
    {
        for (int index = expectations.Count - 1; index >= 0; index--)
        {
            OutputDifferenceExpectation expectation = expectations[index];
            if (!expectation.Range.Contains(segment))
            {
                continue;
            }

            if (string.Equals(expectation.Classification, OutputDifferenceClassifications.PreservedReference, StringComparison.Ordinal))
            {
                break;
            }

            return expectation;
        }

        return new OutputDifferenceExpectation(
            segment,
            OutputDifferenceClassifications.Unexpected,
            false,
            "not-declared",
            "Unexpected: final output differs from the reference base outside declared replacement and IC-number-specific postbuild CRC/header ranges.",
            "Unexpected range");
    }

    private static List<ByteRange> SubtractRanges(ByteRange source, IReadOnlyList<ByteRange> removedRanges)
    {
        ByteRange[] overlaps =
        [
            .. removedRanges
                .Select(source.Intersect)
                .Where(overlap => overlap is not null)
                .Select(overlap => overlap!.Value),
        ];
        if (overlaps.Length == 0)
        {
            return [source];
        }

        SortedSet<long> splitPoints = [source.Start, source.EndExclusive];
        foreach (ByteRange overlap in overlaps)
        {
            _ = splitPoints.Add(overlap.Start);
            _ = splitPoints.Add(overlap.EndExclusive);
        }

        long[] points = [.. splitPoints];
        List<ByteRange> ranges = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var segment = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            if (!overlaps.Any(overlap => overlap.Overlaps(segment)))
            {
                ranges.Add(segment);
            }
        }

        return ranges;
    }

    private static IEnumerable<CompositionIssue> CreateOutputDifferenceIssues(
        IEnumerable<OutputDifferenceSummary> outputDifferences)
    {
        return outputDifferences
            .Where(difference => !difference.IsAccepted)
            .Select(difference => new CompositionIssue(
                ReportIssueCodes.UnexpectedOutputDifference,
                $"Replace output difference '{difference.DifferenceId}' is outside declared replacement ranges and IC-number-specific postbuild CRC/header ranges.",
                difference.DifferenceId));
    }

    private static string ToSliceSha256Hex(byte[] bytes, ByteRange range)
    {
        return ToSha256Hex(bytes.AsSpan(checked((int)range.Start), checked((int)range.Length)));
    }

    private static string ToSliceHexPreview(byte[] bytes, ByteRange range)
    {
        int length = checked((int)Math.Min(range.Length, OutputDifferenceHexPreviewBytes));
        return ToHex(bytes.AsSpan(checked((int)range.Start), length));
    }

    private sealed record OutputDifferenceExpectation(ByteRange Range, string Classification, bool IsAccepted, string Evidence, string Explanation, string SectionLabel);
}
