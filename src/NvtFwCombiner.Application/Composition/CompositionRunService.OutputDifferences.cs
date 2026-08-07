using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static List<OutputDifferenceSummary> CreateOutputDifferences(
        CompositionRunRequest request,
        CompositionExecutionResult execution,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        ReadOnlyMemory<byte> outputBytes)
    {
        if (execution.Status != CompositionExecutionStatus.Succeeded ||
            request.CompiledComposition.V2Details.CompositionKind != CompositionKind.Replace ||
            request.CompiledComposition.Plan.OutputInitialization.Kind != ImageInitializationKind.Reference ||
            request.CompiledComposition.Plan.OutputInitialization.ReferenceSpaceId is not { } referenceSpaceId ||
            !inputBytes.TryGetValue(referenceSpaceId, out byte[]? referenceBytes) ||
            referenceBytes.Length != outputBytes.Length)
        {
            return [];
        }

        IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(referenceBytes, outputBytes.Span);
        if (changedRanges.Count == 0)
        {
            return [];
        }

        OutputDifferenceExpectation[] expectations = [.. CreateOutputDifferenceExpectations(request)];
        MetadataInspectionSnapshot? reportMetadata =
            CreateReportMetadataSnapshot(request, inputBytes);
        List<OutputDifferenceSummary> rows = [];
        OutputDifferenceExpectation? cachedSemanticExpectation = null;
        OutputDifferenceSemantic? cachedSemantic = null;
        int rowIndex = 0;
        foreach (ByteRange changedRange in changedRanges)
        {
            foreach (ByteRange segment in SplitRangeByExpectations(changedRange, expectations))
            {
                OutputDifferenceExpectation expectation = ClassifyDifferenceSegment(segment, expectations);
                OutputDifferenceSemantic semantic;
                if (CanShareOutputDifferenceSemantic(expectation))
                {
                    if (ReferenceEquals(expectation, cachedSemanticExpectation) && cachedSemantic is not null)
                    {
                        semantic = cachedSemantic;
                    }
                    else
                    {
                        cachedSemanticExpectation = expectation;
                        semantic = CreateOutputDifferenceSemantic(
                            request,
                            reportMetadata,
                            expectation,
                            segment);
                        cachedSemantic = semantic;
                    }
                }
                else
                {
                    semantic = CreateOutputDifferenceSemantic(
                        request,
                        reportMetadata,
                        expectation,
                        segment);
                }

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
                    ToSliceSha256Hex(outputBytes.Span, segment),
                    ToSliceHexPreview(referenceBytes, segment),
                    ToSliceHexPreview(outputBytes.Span, segment),
                    checked((int)Math.Min(segment.Length, OutputDifferenceHexPreviewBytes)),
                    segment.Length <= OutputDifferenceHexPreviewBytes,
                    semantic,
                    OutputDifferenceReplaySegment.CreatePersistableWithAlignedContext(
                        referenceBytes,
                        outputBytes,
                        segment)));
            }
        }

        return rows;
    }

    private static bool CanShareOutputDifferenceSemantic(OutputDifferenceExpectation expectation)
    {
        return expectation.Classification is
            OutputDifferenceClassifications.DeclaredReplacement or
            OutputDifferenceClassifications.PreservedReference;
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
}
