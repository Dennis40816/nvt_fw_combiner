namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies Presentation consumes the canonical Domain workflow ids without a Bootstrap mirror.</summary>
    [Fact]
    public void PresentationUsesCanonicalWorkflowIds()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("ExperienceIds.StandardMerge", presentationSource, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.GeneralMerge", presentationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchWorkflowIds", presentationSource, StringComparison.Ordinal);
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

    /// <summary>Verifies Presentation consumes the canonical Domain address-space ids without a Bootstrap mirror.</summary>
    [Fact]
    public void PresentationUsesCanonicalAddressSpaceIds()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("CompositionAddressSpaceIds.DpInput", presentationSource, StringComparison.Ordinal);
        Assert.Contains("CompositionAddressSpaceIds.TpInput", presentationSource, StringComparison.Ordinal);
        Assert.Contains("CompositionAddressSpaceIds.LdcInput", presentationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchAddressSpaceIds", presentationSource, StringComparison.Ordinal);
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

        Assert.Contains("CompositionSlotIds.MergeDp", presentationSource, StringComparison.Ordinal);
        Assert.Contains("CompositionSlotIds.MergeTp", presentationSource, StringComparison.Ordinal);
        Assert.Contains("CompositionSlotIds.MergeLdc", presentationSource, StringComparison.Ordinal);
        Assert.Contains("CompositionSlotIds.ReplaceBase", presentationSource, StringComparison.Ordinal);
        Assert.Contains("CompositionSlotIds.ReplaceDp", presentationSource, StringComparison.Ordinal);
        Assert.Contains("DynamicCtrlRamReplacementIds.TryFormatDisplayLabel", presentationSource, StringComparison.Ordinal);
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

        Assert.Contains("ExperienceIds.StandardMerge", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.AbMerge", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.GeneralMerge", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.StandardMerge", dynamicText, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.GeneralMerge", dynamicText, StringComparison.Ordinal);
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

    /// <summary>Verifies CtrlRAM grouping consumes the Application-owned logical identity.</summary>
    [Fact]
    public void ReplaceRegionGroupsUseTypedGroupsAndStableSourceIdentity()
    {
        string models = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionClientModels.cs");
        string builder = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplaceRegionGroupBuilder.cs");

        Assert.Contains("public enum ReplaceRegionGroup", models, StringComparison.Ordinal);
        Assert.Contains("GroupBy(static slot => slot.RegionGroup)", builder, StringComparison.Ordinal);
        Assert.Contains("segment.LogicalCoverageGroupId", builder, StringComparison.Ordinal);
        Assert.Contains("GroupBy(static entry => entry.Key, StringComparer.Ordinal)", builder, StringComparison.Ordinal);
        Assert.Contains("GroupBy(ResolveDisplayGroup)", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveDisplayId", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedSlotsByRegion", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("segment.SourceSlotId", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("segment.RegionId", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("GroupBy(static segment => segment.RegionGroup)", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("GroupBy(static segment => segment.SourceLabel", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("Title.Contains", builder, StringComparison.Ordinal);
    }

    /// <summary>Verifies memory projection consumes the Domain write-range contract without primitive branching.</summary>
    [Fact]
    public void MemoryProjectionUsesOneDeclaredWriteRangeContract()
    {
        string projector = ReadText(
            "src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.cs");
        string logicalCoverage = ReadText(
            "src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.LogicalCoverage.cs");

        Assert.Contains("operation.DeclaredWriteRanges", projector, StringComparison.Ordinal);
        Assert.Contains("operation.DeclaredWriteRanges", logicalCoverage, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowedWriteRanges", projector, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetRange.Contains(range)", projector, StringComparison.Ordinal);
    }

    /// <summary>Verifies Presentation reads report output-difference classifications from Contracts directly.</summary>
    [Fact]
    public void PresentationUsesContractOutputDifferenceClassifications()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("OutputDifferenceClassifications.DeclaredReplacement", presentationSource, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceClassifications.PostbuildCrcHeader", presentationSource, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceClassifications.Unexpected", presentationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchOutputDifferenceClassifications", presentationSource, StringComparison.Ordinal);
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

    /// <summary>Verifies Presentation reads composition issue codes from Domain directly.</summary>
    [Fact]
    public void PresentationUsesDomainCompositionIssueCodes()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("CompositionIssueCodes.InputAddressSpaceLengthMismatch", presentationSource, StringComparison.Ordinal);
        Assert.Contains("CompositionIssueCodes.InputAddressSpaceTruncated", presentationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchCompositionIssueCodes", presentationSource, StringComparison.Ordinal);
        foreach (string issueCodeLiteral in new[]
        {
            "\"input.address-space.length-mismatch\"",
            "\"input.address-space.truncated\"",
        })
        {
            Assert.DoesNotContain(issueCodeLiteral, presentationSource, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies Presentation receives fallback run issue codes from the Application contract.</summary>
    [Fact]
    public void PresentationUsesApplicationCompositionPlanningIssueCodes()
    {
        string presentationSource = ReadPresentationSources();

        Assert.Contains("CompositionPlanningIssueCodes.UiRunFailed", presentationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.run.failed\"", presentationSource, StringComparison.Ordinal);
    }
}
