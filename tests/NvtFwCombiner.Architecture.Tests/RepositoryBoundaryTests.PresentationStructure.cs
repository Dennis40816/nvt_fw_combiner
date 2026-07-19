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

    /// <summary>Verifies Presentation uses Application only for approved typed contracts, never firmware catalogs.</summary>
    [Fact]
    public void PresentationUsesBootstrapFacadeInsteadOfFirmwareCatalogs()
    {
        string project = ReadText("src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj");
        string progressProjection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/CompositionRunProgressViewModel.cs");
        string progressResources = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ShellTextResources.RunProgress.cs");
        string progressConsumer = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.RunLifecycle.cs");
        string inspectionProjection = string.Join(
            Environment.NewLine,
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Report.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHexDiffViewModel.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.Bindings.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.Factory.cs"));
        string presentationSource = ReadPresentationSources(
            "CompositionRunProgressViewModel.cs",
            "ShellTextResources.RunProgress.cs",
            "MainWindowViewModel.RunLifecycle.cs",
            "MainWindowViewModel.Report.cs",
            "ReportHexDiffViewModel.cs",
            "ReportReviewViewModel.cs",
            "ReportReviewViewModel.Bindings.cs",
            "ReportReviewViewModel.Factory.cs");
        string[] forbiddenTokens =
        [
            "NvtFwCombiner.Application.Composition",
            "NvtFwCombiner.Application.ExternalTools",
            "NvtFwCombiner.Application.FlashMaps",
            "NvtFwCombiner.Domain.",
            "NvtFwCombiner.Infrastructure.",
            "NvtFwCombiner.Profiles",
            "GenFlashVersionCatalog",
            "BuiltInTpFlashMapCatalog",
            "TpHeaderCatalog",
            "LegacyCombinerPostbuildCatalog",
            "DpPerspectiveCatalog",
            "NT51950",
            "PostbuildSetup_",
        ];

        Assert.Contains("NvtFwCombiner.Bootstrap.csproj", project, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Application.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Domain.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles.csproj", project, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService", presentationSource, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Application.HexEditor", presentationSource, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Application.Composition", progressProjection, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Application.Composition", progressResources, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Application.Composition", progressConsumer, StringComparison.Ordinal);
        Assert.Contains("CompositionRunInspectionSnapshot", inspectionProjection, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Application.Composition", inspectionProjection, StringComparison.Ordinal);
        foreach (string token in forbiddenTokens)
        {
            Assert.DoesNotContain(token, presentationSource, StringComparison.Ordinal);
            if (!string.Equals(token, "NvtFwCombiner.Application.Composition", StringComparison.Ordinal))
            {
                Assert.DoesNotContain(token, progressProjection, StringComparison.Ordinal);
                Assert.DoesNotContain(token, progressResources, StringComparison.Ordinal);
                Assert.DoesNotContain(token, progressConsumer, StringComparison.Ordinal);
                Assert.DoesNotContain(token, inspectionProjection, StringComparison.Ordinal);
            }
        }
    }

}
