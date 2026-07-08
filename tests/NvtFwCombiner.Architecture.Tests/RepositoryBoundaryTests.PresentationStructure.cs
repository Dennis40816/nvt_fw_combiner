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
        Assert.Contains("GetStandardMergeMemoryMapRows", merge, StringComparison.Ordinal);
        Assert.Contains("RunGeneralMergeAsync", merge, StringComparison.Ordinal);
        Assert.Contains("GetReplaceMemoryMapRows", replace, StringComparison.Ordinal);
        Assert.Contains("RunReplaceAsync", replace, StringComparison.Ordinal);
    }
}
