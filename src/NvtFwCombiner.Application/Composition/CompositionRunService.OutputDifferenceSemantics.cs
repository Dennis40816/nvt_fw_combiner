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
        if (TpHeaderCatalog.IsHeaderSection(expectation.SectionId) &&
            TpHeaderCatalog.TryFindField(icId, range, out TpHeaderField? field))
        {
            return new OutputDifferenceSemantic(
                TpBinaryCategoryIds.TpFlashHeader,
                "TP Flash Header",
                $"{icId.ToLowerInvariant()}-header:{field!.FieldId}",
                field.DisplayName,
                $"Expected: postbuild recalculated {field.DisplayName}.");
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
}
