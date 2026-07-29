using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateGeneralMergeDraft(
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> inputs,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues)
    {
        List<GeneralMappingDraftRow> rows = [];
        List<CompositionIssue> issueList = [];
        foreach (WorkbenchGeneralMergeMappingInput input in inputs)
        {
            if (!TryCreateGeneralMergeDraftRow(
                    input,
                    out GeneralMappingDraftRow? row,
                    out CompositionIssue? issue))
            {
                issueList.Add(issue!);
                continue;
            }

            rows.Add(row);
        }

        return TryCompleteGeneralMappingDraft(
            rows,
            issueList,
            WorkbenchIssueCodes.GeneralMergeRangeInvalid,
            out draft,
            out issues);
    }

    private static bool TryCreateGeneralMergeDraftRow(
        WorkbenchGeneralMergeMappingInput input,
        [NotNullWhen(true)] out GeneralMappingDraftRow? row,
        out CompositionIssue? issue)
    {
        bool sourceParsed = AuthoringByteRangeCodec.TryParseStartAndLength(
            input.SourceStart,
            input.Length,
            out ByteRange sourceRange,
            out AuthoringRangeTextIssue? sourceIssue);
        bool targetParsed = AuthoringByteRangeCodec.TryParseStartAndLength(
            input.TargetStart,
            input.Length,
            out ByteRange targetRange,
            out AuthoringRangeTextIssue? targetIssue);
        if (!sourceParsed || !targetParsed)
        {
            AuthoringRangeTextIssue rangeIssue = sourceIssue ?? targetIssue!;
            row = null;
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralMergeRangeInvalid,
                $"General Merge mapping '{input.MappingId}' is invalid: {rangeIssue.Message}",
                input.MappingId);
            return false;
        }

        try
        {
            row = new GeneralMappingDraftRow(
                input.MappingId,
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(input.FilePath),
                sourceRange,
                CompositionAddressSpaceIds.OutputImage,
                targetRange,
                OverlapPolicy.Reject,
                input.Alignment,
                input.Reason ?? "Copy explicit General Merge mapping.",
                WorkbenchGeneralMergeIds.OutputRegionId,
                input.Provenance);
            issue = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            row = null;
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralMergeRangeInvalid,
                $"General Merge mapping '{input.MappingId}' is invalid: {exception.Message}",
                input.MappingId);
            return false;
        }
    }

    private static bool TryCreateLegacyGeneralReplaceDraft(
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> patchInputs,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues)
    {
        List<GeneralMappingDraftRow> rows = [];
        List<CompositionIssue> issueList = [];
        foreach (WorkbenchGeneralReplaceMappingInput input in mappingInputs)
        {
            if (!TryParseLegacyInclusiveRange(
                    input.MappingId,
                    input.TargetStart,
                    input.TargetEndInclusive,
                    out ByteRange targetRange,
                    out CompositionIssue? issue))
            {
                issueList.Add(issue!);
                continue;
            }

            TryAddGeneralReplaceDraftRow(
                input.MappingId,
                GeneralMappingSource.File(input.FilePath),
                targetRange,
                "Replace explicit General range.",
                rows,
                issueList);
        }

        foreach (WorkbenchGeneralReplacePatchInput input in patchInputs)
        {
            if (!TryParseLegacyInclusiveRange(
                    input.PatchId,
                    input.TargetStart,
                    input.TargetEndInclusive,
                    out ByteRange targetRange,
                    out CompositionIssue? issue))
            {
                issueList.Add(issue!);
                continue;
            }

            GeneralMappingSource source = input.Kind switch
            {
                WorkbenchGeneralReplacePatchKind.Overwrite =>
                    GeneralMappingSource.HexOverwrite(input.Value, input.PatchId),
                WorkbenchGeneralReplacePatchKind.Fill =>
                    GeneralMappingSource.HexFill(input.Value, input.PatchId),
                _ => throw new InvalidOperationException(
                    "Unknown General Replace patch kind."),
            };
            TryAddGeneralReplaceDraftRow(
                input.PatchId,
                source,
                targetRange,
                source.Kind == GeneralMappingSourceKind.HexFill
                    ? "Fill hexadecimal General range."
                    : "Overwrite hexadecimal General range.",
                rows,
                issueList);
        }

        return TryCompleteGeneralMappingDraft(
            rows,
            issueList,
            WorkbenchIssueCodes.GeneralReplacePatchIdDuplicate,
            out draft,
            out issues);
    }

    internal static bool TryCreateGeneralReplaceDraft(
        IEnumerable<GeneralMappingDraftRow> rows,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return TryCompleteGeneralMappingDraft(
            [.. rows],
            [],
            WorkbenchIssueCodes.GeneralReplacePatchIdDuplicate,
            out draft,
            out issues);
    }

    private static void TryAddGeneralReplaceDraftRow(
        string mappingId,
        GeneralMappingSource source,
        ByteRange targetRange,
        string reason,
        List<GeneralMappingDraftRow> rows,
        List<CompositionIssue> issues)
    {
        try
        {
            rows.Add(new GeneralMappingDraftRow(
                mappingId,
                ExplicitMappingOperationKind.ReplaceRange,
                source,
                new ByteRange(0, targetRange.Length),
                CompositionAddressSpaceIds.OutputImage,
                targetRange,
                OverlapPolicy.Reject,
                alignment: 1,
                reason));
        }
        catch (ArgumentException exception)
        {
            issues.Add(new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                $"General Replace mapping or patch '{mappingId}' is invalid: {exception.Message}",
                mappingId));
        }
    }

    private static bool TryCompleteGeneralMappingDraft(
        List<GeneralMappingDraftRow> rows,
        List<CompositionIssue> issueList,
        string invalidDraftIssueCode,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (issueList.Count == 0)
        {
            try
            {
                draft = new GeneralMappingDraftState(rows);
                issues = [];
                return true;
            }
            catch (ArgumentException exception)
            {
                issueList.Add(new CompositionIssue(
                    invalidDraftIssueCode,
                    exception.Message,
                    "mapping"));
            }
        }

        draft = null;
        issues = issueList;
        return false;
    }

    private static bool TryParseLegacyInclusiveRange(
        string id,
        string targetStart,
        string targetEndInclusive,
        out ByteRange targetRange,
        out CompositionIssue? issue)
    {
        targetRange = default;
        if (!AuthoringByteRangeCodec.TryParseNonNegativeLong(targetStart, out long start) ||
            !AuthoringByteRangeCodec.TryParseNonNegativeLong(targetEndInclusive, out long endInclusive) ||
            endInclusive < start)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                $"General Replace mapping or patch '{id}' must use a valid legacy inclusive start/end range.",
                id);
            return false;
        }

        try
        {
            targetRange = new ByteRange(start, checked(endInclusive - start + 1));
            issue = null;
            return true;
        }
        catch (OverflowException)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                $"General Replace mapping or patch '{id}' range exceeds the supported address size.",
                id);
            return false;
        }
    }
}
