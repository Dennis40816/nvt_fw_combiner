using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private static OutputDifferenceSemantic CreateOutputDifferenceSemantic(
        CompositionRunRequest request,
        OutputDifferenceExpectation expectation,
        ByteRange range)
    {
        return expectation.Classification switch
        {
            OutputDifferenceClassifications.PostbuildCrcHeader =>
                CreatePostbuildDifferenceSemantic(request.Profile.IcId, expectation, range),
            OutputDifferenceClassifications.DeclaredReplacement =>
                CreateDeclaredReplacementSemantic(expectation),
            OutputDifferenceClassifications.PreservedReference => new OutputDifferenceSemantic(
                "reference-base",
                "Reference base",
                "reference-preserved-range",
                "Reference-preserved range",
                "Unexpected: a range declared to stay from the reference base differs in the final output."),
            _ => new OutputDifferenceSemantic(
                "review-required",
                "Review required",
                "unexpected-range",
                "Unexpected byte range",
                "Not accepted by the selected profile; review before release."),
        };
    }

    private static OutputDifferenceSemantic CreateDeclaredReplacementSemantic(OutputDifferenceExpectation expectation)
    {
        bool isCtrlRam = string.Equals(
            expectation.SectionId,
            TpHeaderSectionIds.CtrlRamReplacement,
            StringComparison.Ordinal);
        return new OutputDifferenceSemantic(
            isCtrlRam ? TpBinaryCategoryIds.CtrlRam : "replacement-data",
            isCtrlRam ? "CtrlRAM" : "Replacement data",
            expectation.SectionId ?? "declared-replacement",
            expectation.SectionLabel,
            $"Expected: this run replaced {expectation.SectionLabel}.");
    }

    private static OutputDifferenceSemantic CreatePostbuildDifferenceSemantic(
        string icId,
        OutputDifferenceExpectation expectation,
        ByteRange range)
    {
        ByteRange fieldRange = MapDifferenceToHeaderSourceRange(expectation, range);
        if (TpHeaderCatalog.IsHeaderSection(expectation.SectionId) &&
            TpHeaderCatalog.TryFindField(icId, fieldRange, out TpHeaderField? field))
        {
            string explanation = expectation.SourceRange is null
                ? $"Expected: postbuild recalculated {field!.DisplayName}."
                : $"Expected: postbuild refreshed {field!.DisplayName} and copied it to {expectation.SectionLabel}.";
            return new OutputDifferenceSemantic(
                TpBinaryCategoryIds.TpFlashHeader,
                "TP Flash Header",
                $"{icId.ToLowerInvariant()}-header:{field!.FieldId}",
                field.DisplayName,
                explanation);
        }

        bool isHeaderCopy = TpHeaderCatalog.IsHeaderSection(expectation.SectionId) ||
                            expectation.SectionId is TpHeaderSectionIds.WindowCopyRight or TpHeaderSectionIds.WindowCopyLeft;
        return isHeaderCopy
            ? new OutputDifferenceSemantic(
                TpBinaryCategoryIds.TpFlashHeader,
                "TP Flash Header",
                expectation.SectionId ?? "header-refresh",
                expectation.SectionLabel,
                $"Expected: postbuild updated {expectation.SectionLabel}.")
            : string.Equals(expectation.SectionId, TpHeaderSectionIds.FirmwareConfigBackup, StringComparison.Ordinal)
                ? new OutputDifferenceSemantic(
                TpBinaryCategoryIds.FirmwareConfiguration,
                "FW Configuration",
                TpHeaderSectionIds.FirmwareConfigBackup,
                expectation.SectionLabel,
                $"Expected: postbuild updated {expectation.SectionLabel}.")
            : new OutputDifferenceSemantic(
                TpBinaryCategoryIds.OtherDocumentedRegion,
                "Other documented regions",
                expectation.SectionId ?? "postbuild-copy",
                expectation.SectionLabel,
                $"Expected: postbuild updated {expectation.SectionLabel}.");
    }

    private static ByteRange MapDifferenceToHeaderSourceRange(
        OutputDifferenceExpectation expectation,
        ByteRange differenceRange)
    {
        if (expectation.SourceRange is not { } sourceRange)
        {
            return differenceRange;
        }

        if (!expectation.Range.Contains(differenceRange))
        {
            throw new InvalidOperationException(
                "A classified output difference must stay inside its declared postbuild write range.");
        }

        long offset = checked(differenceRange.Start - expectation.Range.Start);
        return new ByteRange(checked(sourceRange.Start + offset), differenceRange.Length);
    }
}
