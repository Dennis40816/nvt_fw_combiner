namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies Presentation receives workflow id tokens through Bootstrap instead of duplicating contract strings.</summary>
    [Fact]
    public void PresentationUsesBootstrapWorkflowIds()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("WorkbenchWorkflowIds.StandardMerge", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchWorkflowIds.GeneralMerge", presentationSource, StringComparison.Ordinal);
        foreach (string workflowLiteral in new[]
        {
            "\"standard-merge\"",
            "\"dp-replace\"",
            "\"ctrlram-replace\"",
            "\"general-merge\"",
            "\"general-replace\"",
        })
        {
            Assert.DoesNotContain(workflowLiteral, presentationSource, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies Presentation receives address-space ids through Bootstrap instead of duplicating contract strings.</summary>
    [Fact]
    public void PresentationUsesBootstrapAddressSpaceIds()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("WorkbenchAddressSpaceIds.DpInput", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchAddressSpaceIds.TpInput", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchAddressSpaceIds.LdcInput", presentationSource, StringComparison.Ordinal);
        foreach (string addressSpaceLiteral in new[]
        {
            "\"dp-input\"",
            "\"tp-input\"",
            "\"ldc-input\"",
            "\"output-image\"",
            "\"reference-base\"",
            "\"dp-replacement\"",
            "\"ldc-replacement\"",
            "\"ctrlram-replacement\"",
        })
        {
            Assert.DoesNotContain(addressSpaceLiteral, presentationSource, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies Presentation receives workbench slot ids through Bootstrap instead of duplicating strings.</summary>
    [Fact]
    public void PresentationUsesBootstrapSlotIds()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("WorkbenchSlotIds.MergeDp", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.MergeTp", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.MergeLdc", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.ReplaceBase", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.ReplaceDp", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.TryFormatReplaceCtrlRamLabel", presentationSource, StringComparison.Ordinal);
        foreach (string slotLiteral in new[]
        {
            "\"merge-dp\"",
            "\"merge-tp\"",
            "\"merge-ldc\"",
            "\"replace-base\"",
            "\"replace-dp\"",
            "\"replace-ctrlram-",
        })
        {
            Assert.DoesNotContain(slotLiteral, presentationSource, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies Presentation uses Bootstrap-projected Merge mode ids in ViewModels and dynamic text.</summary>
    [Fact]
    public void PresentationUsesBootstrapMergeModeIds()
    {
        string viewModels = ReadViewModelPartials();
        string dynamicText = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ShellTextResources.DynamicText.cs");

        Assert.Contains("WorkbenchMergeModes.Standard", viewModels, StringComparison.Ordinal);
        Assert.Contains("WorkbenchMergeModes.AbCode", viewModels, StringComparison.Ordinal);
        Assert.Contains("WorkbenchMergeModes.General", viewModels, StringComparison.Ordinal);
        Assert.Contains("WorkbenchMergeModes.Standard", dynamicText, StringComparison.Ordinal);
        Assert.Contains("WorkbenchMergeModes.General", dynamicText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Normal\"", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AB Code\"", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Normal\" when", dynamicText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"General\" when", dynamicText, StringComparison.Ordinal);
    }

    /// <summary>Verifies report input grouping tokens stay centralized inside Presentation.</summary>
    [Fact]
    public void ReportInputClassificationTokensStayPresentationOwned()
    {
        string tokens = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportInputClassifications.cs");
        string parsing = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.Parsing.cs");
        string inputGroups = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.InputGroups.cs");
        string operationFlow = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.OperationFlow.cs");

        Assert.Contains("public const string Base = \"base\";", tokens, StringComparison.Ordinal);
        Assert.Contains("public const string CtrlRam = \"ctrlram\";", tokens, StringComparison.Ordinal);
        Assert.Contains("public const string Other = \"other\";", tokens, StringComparison.Ordinal);
        Assert.Contains("public const string RoleReplacement = \"replacement\";", tokens, StringComparison.Ordinal);
        Assert.Contains("public const string RoleInput = \"input\";", tokens, StringComparison.Ordinal);
        Assert.Contains("public const string ReferenceSearchTerm = \"reference\";", tokens, StringComparison.Ordinal);
        Assert.Contains("ReportInputClassifications.Base", parsing, StringComparison.Ordinal);
        Assert.Contains("ReportInputClassifications.CtrlRam", parsing, StringComparison.Ordinal);
        Assert.Contains("ReportInputClassifications.Other", parsing, StringComparison.Ordinal);
        Assert.Contains("ReportInputClassifications.RoleReplacement", parsing, StringComparison.Ordinal);
        Assert.Contains("ReportInputClassifications.RoleInput", parsing, StringComparison.Ordinal);
        Assert.Contains("ReportInputClassifications.Base", inputGroups, StringComparison.Ordinal);
        Assert.Contains("ReportInputClassifications.CtrlRam", inputGroups, StringComparison.Ordinal);
        Assert.Contains("ReportInputClassifications.Other", inputGroups, StringComparison.Ordinal);
        Assert.Contains("ReportInputClassifications.Base", operationFlow, StringComparison.Ordinal);
        foreach (string classificationLiteral in new[]
        {
            "\"base\"",
            "\"ctrlram\"",
            "\"other\"",
            "\"replacement\"",
        })
        {
            Assert.DoesNotContain(classificationLiteral, parsing, StringComparison.Ordinal);
            Assert.DoesNotContain(classificationLiteral, inputGroups, StringComparison.Ordinal);
            Assert.DoesNotContain(classificationLiteral, operationFlow, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies CtrlRAM grouping reaches Presentation as typed Bootstrap data.</summary>
    [Fact]
    public void ReplaceRegionGroupsDoNotDependOnLocalizedLabels()
    {
        string models = ReadText(
            "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionModels.cs");
        string builder = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplaceRegionGroupBuilder.cs");

        Assert.Contains("public enum WorkbenchReplaceRegionGroup", models, StringComparison.Ordinal);
        Assert.Contains("GroupBy(static slot => slot.RegionGroup)", builder, StringComparison.Ordinal);
        Assert.Contains("GroupBy(static segment => segment.RegionGroup)", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceLabel", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("Title.Contains", builder, StringComparison.Ordinal);
    }

    /// <summary>Verifies Presentation reads report output-difference classifications through Bootstrap tokens.</summary>
    [Fact]
    public void PresentationUsesBootstrapOutputDifferenceClassifications()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("WorkbenchOutputDifferenceClassifications.DeclaredReplacement", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchOutputDifferenceClassifications.PostbuildCrcHeader", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchOutputDifferenceClassifications.Unexpected", presentationSource, StringComparison.Ordinal);
        foreach (string classificationLiteral in new[]
        {
            "\"DeclaredReplacement\"",
            "\"PostbuildCrcHeader\"",
            "\"PreservedReference\"",
            "\"Unexpected\"",
        })
        {
            Assert.DoesNotContain(classificationLiteral, presentationSource, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies Presentation reads composition issue codes through Bootstrap tokens.</summary>
    [Fact]
    public void PresentationUsesBootstrapCompositionIssueCodes()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("WorkbenchCompositionIssueCodes.InputAddressSpaceLengthMismatch", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionIssueCodes.InputAddressSpaceTruncated", presentationSource, StringComparison.Ordinal);
        foreach (string issueCodeLiteral in new[]
        {
            "\"input.address-space.length-mismatch\"",
            "\"input.address-space.truncated\"",
        })
        {
            Assert.DoesNotContain(issueCodeLiteral, presentationSource, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies Presentation receives UI fallback run issue codes through Bootstrap tokens.</summary>
    [Fact]
    public void PresentationUsesBootstrapWorkbenchIssueCodes()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("WorkbenchIssueCodes.UiRunFailed", presentationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.run.failed\"", presentationSource, StringComparison.Ordinal);
    }
}
