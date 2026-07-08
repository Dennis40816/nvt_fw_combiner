namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies external combiner versions are documented as exact string tokens.</summary>
    [Fact]
    public void ExternalCombinerVersionsAreDocumentedAsStringTokens()
    {
        string adr = ReadText("docs/adr/0006-external-combiner-tool-runner.md");

        Assert.Contains("`toolVersion` is always a string", adr, StringComparison.Ordinal);
        Assert.Contains("`1.10` and `1.9` are exact version tokens", adr, StringComparison.Ordinal);
    }

    /// <summary>Verifies UI planning documents keep firmware behavior out of ViewModels.</summary>
    [Fact]
    public void UiDocumentsForbidFirmwareSemanticsInViewModels()
    {
        string boundaries = ReadText("docs/ui/viewmodel-boundaries.md");

        Assert.Contains("byte range arithmetic", boundaries, StringComparison.Ordinal);
        Assert.Contains("CRC/Header calculation or `combiner.exe` invocation", boundaries, StringComparison.Ordinal);
        Assert.Contains("No `File.ReadAllBytes` or `Process.Start` in ViewModels", boundaries, StringComparison.Ordinal);
    }

    /// <summary>Verifies Presentation reaches firmware workflow catalogs only through the Bootstrap workbench facade.</summary>
    [Fact]
    public void PresentationUsesBootstrapFacadeInsteadOfFirmwareCatalogs()
    {
        string project = ReadText("src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj");
        string presentationSource = ReadPresentationSources();
        string[] forbiddenTokens =
        [
            "NvtFwCombiner.Application.",
            "NvtFwCombiner.Domain.",
            "NvtFwCombiner.Infrastructure.",
            "NvtFwCombiner.Profiles",
            "GenFlashVersionCatalog",
            "TpFlashMapCatalog",
            "TpHeaderCatalog",
            "LegacyCombinerPostbuildCatalog",
            "DpPerspectiveCatalog",
            "NT51950",
            "PostbuildSetup_",
        ];

        Assert.Contains("NvtFwCombiner.Bootstrap.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Domain.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles.csproj", project, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService", presentationSource, StringComparison.Ordinal);
        foreach (string token in forbiddenTokens)
        {
            Assert.DoesNotContain(token, presentationSource, StringComparison.Ordinal);
        }
    }

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
        Assert.Contains("WorkbenchAddressSpaceIds.LdInput", presentationSource, StringComparison.Ordinal);
        foreach (string addressSpaceLiteral in new[]
        {
            "\"dp-input\"",
            "\"tp-input\"",
            "\"ld-input\"",
            "\"output-image\"",
            "\"reference-base\"",
            "\"dp-replacement\"",
            "\"ld-replacement\"",
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
        Assert.Contains("WorkbenchSlotIds.MergeLd", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.ReplaceBase", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.ReplaceDp", presentationSource, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSlotIds.TryFormatReplaceCtrlRamLabel", presentationSource, StringComparison.Ordinal);
        foreach (string slotLiteral in new[]
        {
            "\"merge-dp\"",
            "\"merge-tp\"",
            "\"merge-ld\"",
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

    /// <summary>Verifies the Presentation runner remains a thin split adapter over Bootstrap workbench contracts.</summary>
    [Fact]
    public void UiCompositionRunnerConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.cs");
        string catalog = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Catalog.cs");
        string common = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Common.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.FirmwareFacts.cs");
        string merge = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Merge.cs");
        string replace = ReadText("src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");

        Assert.Contains("public static partial class UiCompositionRunner", root, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchCompositionService.", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFirmwareSlotFacts", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStandardMergeMemoryMapRows", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetReplaceMemoryMapRows", root, StringComparison.Ordinal);
        Assert.Contains("GetSupportedIcIds", catalog, StringComparison.Ordinal);
        Assert.Contains("GetDefaultIcId", catalog, StringComparison.Ordinal);
        Assert.Contains("GetSettingsSnapshot", catalog, StringComparison.Ordinal);
        Assert.Contains("private static MemoryMapRowViewModel ToMemoryMapRow", common, StringComparison.Ordinal);
        Assert.Contains("GetFirmwareSlotFacts", facts, StringComparison.Ordinal);
        Assert.Contains("CreateFlashCodeOutputFileName", facts, StringComparison.Ordinal);
        Assert.Contains("WorkbenchReplaceModes", ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs"), StringComparison.Ordinal);
        Assert.Contains("GetStandardMergeMemoryMapRows", merge, StringComparison.Ordinal);
        Assert.Contains("RunGeneralMergeAsync", merge, StringComparison.Ordinal);
        Assert.Contains("GetReplaceMemoryMapRows", replace, StringComparison.Ordinal);
        Assert.Contains("RunReplaceAsync", replace, StringComparison.Ordinal);
    }

    /// <summary>Verifies firmware slot state, icons, and fact badges stay split by UI responsibility.</summary>
    [Fact]
    public void FirmwareSlotViewModelConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.cs");
        string icons = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.Icons.cs");
        string state = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.State.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotFactViewModel.cs");
        string kind = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotKind.cs");

        Assert.Contains("public sealed partial class FirmwareSlotViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public partial string? FilePath", root, StringComparison.Ordinal);
        Assert.Contains("public void ApplyDisplayText", root, StringComparison.Ordinal);
        Assert.Contains("public void SetFirmwareFacts", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotIconPathData", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public IBrush SlotBackgroundBrush", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record FirmwareSlotFactViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum FirmwareSlotKind", root, StringComparison.Ordinal);
        Assert.Contains("SlotIconPathData", icons, StringComparison.Ordinal);
        Assert.Contains("InferSlotKind", icons, StringComparison.Ordinal);
        Assert.Contains("SlotBackgroundBrush", state, StringComparison.Ordinal);
        Assert.Contains("RequirementBadgeForegroundBrush", state, StringComparison.Ordinal);
        Assert.Contains("public sealed record FirmwareSlotFactViewModel", facts, StringComparison.Ordinal);
        Assert.Contains("public enum FirmwareSlotKind", kind, StringComparison.Ordinal);
    }

    /// <summary>Verifies report line rows, chips, groups, and flow nodes stay split by UI responsibility.</summary>
    [Fact]
    public void ReportLineViewModelConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportLineViewModel.cs");
        string badges = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportLineBadgeViewModel.cs");
        string facts = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportLineFactViewModel.cs");
        string rangeRows = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportRangeTableRowViewModel.cs");
        string differenceRows = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportDifferenceSummaryRowViewModel.cs");
        string inputGroups = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportInputGroupViewModel.cs");
        string flowNodes = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportOperationFlowNodeViewModel.cs");

        Assert.Contains("public sealed class ReportLineViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public static ReportLineViewModel Empty", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportLineBadgeViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportLineFactViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record ReportRangeTableRowViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed record ReportDifferenceSummaryRowViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportInputGroupViewModel", root, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class ReportOperationFlowNodeViewModel", root, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportLineBadgeViewModel", badges, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportLineFactViewModel", facts, StringComparison.Ordinal);
        Assert.Contains("public sealed record ReportRangeTableRowViewModel", rangeRows, StringComparison.Ordinal);
        Assert.Contains("public sealed record ReportDifferenceSummaryRowViewModel", differenceRows, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportInputGroupViewModel", inputGroups, StringComparison.Ordinal);
        Assert.Contains("public sealed class ReportOperationFlowNodeViewModel", flowNodes, StringComparison.Ordinal);
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
}
