using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static IEnumerable<OutputDifferenceExpectation> CreateOutputDifferenceExpectations(
        CompositionRunRequest request)
    {
        string targetSpaceId = request.Plan.OutputSpaceId;
        string? referenceSpaceId = request.Plan.OutputInitialization.ReferenceSpaceId;
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
                        FormatDifferenceSectionLabel(binding.SourceSpaceId, operation.Reason),
                        TpHeaderSectionIds.CtrlRamReplacement,
                        null);
                }

                foreach (ByteRange allowedWriteRange in invocation.AllowedWriteRanges)
                {
                    foreach (ByteRange processorOnlyRange in SubtractRanges(allowedWriteRange, stagedRanges))
                    {
                        foreach (ByteRange processorSegment in SplitRangeByWriteSectionBoundaries(
                                     processorOnlyRange,
                                     invocation.AllowedWriteRangeSections))
                        {
                            ExternalProcessorWriteRangeSection? section = FindProcessorWriteSection(
                                invocation,
                                processorSegment);
                            string? sectionId = section?.SectionId;
                            string sectionLabel = FormatProcessorWriteSectionLabel(sectionId);
                            yield return new OutputDifferenceExpectation(
                                processorSegment,
                                OutputDifferenceClassifications.PostbuildCrcHeader,
                                true,
                                $"{operation.OperationId}: {invocation.ProcessorId}",
                                $"Accepted: this range is inside the {request.Profile.IcId} / {icNumber} approved {sectionLabel} postbuild write ranges.",
                                sectionLabel,
                                sectionId,
                                section is not null && section.TryMapRangeToSourceRange(processorSegment, out ByteRange sourceRange)
                                    ? sourceRange
                                    : null);
                        }
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
            "Reference-preserved range",
            null,
            null);
                continue;
            }

            yield return new OutputDifferenceExpectation(
                operation.TargetRange,
            OutputDifferenceClassifications.DeclaredReplacement,
            true,
            operation.OperationId,
            $"Accepted: declared Replace mapping '{operation.OperationId}' writes this final range.",
            FormatDifferenceSectionLabel(operation.SourceSpaceId, operation.Reason),
            null,
            null);
        }
    }

    private static bool IsReferencePreserveOperation(CompositionOperation operation, string? referenceSpaceId)
    {
        return referenceSpaceId is not null &&
            operation.Kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange &&
            string.Equals(operation.SourceSpaceId, referenceSpaceId, StringComparison.Ordinal) &&
            operation.SourceRange == operation.TargetRange;
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
            "Unexpected range",
            null,
            null);
    }

    private sealed record OutputDifferenceExpectation(
        ByteRange Range,
        string Classification,
        bool IsAccepted,
        string Evidence,
        string Explanation,
        string SectionLabel,
        string? SectionId,
        ByteRange? SourceRange);
}
