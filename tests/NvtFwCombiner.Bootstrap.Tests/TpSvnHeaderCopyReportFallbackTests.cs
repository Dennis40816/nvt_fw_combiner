using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// TP-SVN-MODEL-1113-01 acceptance T5 (review F-4): with the misdeclared svn-auto-build-version TP Header field
/// removed, a postbuild Header-copy difference inside +0x24..+0x28 keeps the TP Flash Header category and the
/// accepted postbuild classification, and now uses the section subject and label instead of a field label.
/// </summary>
public sealed class TpSvnHeaderCopyReportFallbackTests
{
    private const string CaseId = "nt51923-fw141-single-auto-prj-662-20260717";
    private const int HeaderCopyStamp = 0x30334;

    /// <summary>The Header-copy stamp difference is an accepted postbuild Header-copy section difference.</summary>
    [Fact]
    public async Task HeaderCopyStampDifferenceUsesTheSectionSubjectAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", CaseId);
        string Path(string artifactId)
        {
            return CanonicalGoldenTestData.ArtifactPath(CanonicalGoldenTestData.Artifact(goldenCase, artifactId));
        }

        byte[] reference = File.ReadAllBytes(Path("tp-input"));
        Assert.Equal(Convert.ToHexString(reference, 0x24, 4), Convert.ToHexString(reference, HeaderCopyStamp, 4));
        reference.AsSpan(HeaderCopyStamp, 4).Clear();
        using TempWorkspace workspace = TempWorkspace.Create("nfc-tp-svn-header-copy-report");
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = workspace.Write("reference.bin", reference),
            ["replace-ctrlram-normal"] = Path("postbuild-normal-ctrlram"),
            ["replace-ctrlram-mp"] = Path("postbuild-mp-ctrlram"),
            ["replace-ctrlram-vn"] = Path("postbuild-vn-ctrlram"),
            ["replace-ctrlram-nf"] = Path("postbuild-nf-ctrlram"),
        };

        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunAsync(
            BootstrapTestHost.Canonical, "NT51923", IcNumberSelectionTokens.SingleChip, ExperienceIds.CtrlRamReplace,
            slotPaths, build: false, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        OutputDifferenceSummary difference = Assert.Single(result.Report.OutputDifferences, static item =>
            item.Range.Start < HeaderCopyStamp + 4 && HeaderCopyStamp < item.Range.EndExclusive);
        Assert.Equal(new ByteRange(HeaderCopyStamp, 4), difference.Range);
        Assert.True(difference.IsAccepted);
        OutputDifferenceSemantic semantic = Assert.IsType<OutputDifferenceSemantic>(difference.Semantic);
        Assert.Equal(OutputDifferenceSemanticCategoryIds.TpFlashHeader, semantic.CategoryId);
        Assert.True(PostbuildWriteSectionSemantics.IsHeaderSection(semantic.SubjectId), semantic.SubjectId);
        Assert.Equal(difference.SectionLabel, semantic.SubjectLabel);
        Assert.DoesNotContain("svn", semantic.SubjectId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Report.Issues, static issue => issue.Code == ReportIssueCodes.UnexpectedOutputDifference);
    }
}
