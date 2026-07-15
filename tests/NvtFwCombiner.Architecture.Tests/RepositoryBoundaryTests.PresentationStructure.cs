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

    /// <summary>Verifies Presentation uses Application only for the raw utility contract, never firmware catalogs.</summary>
    [Fact]
    public void PresentationUsesBootstrapFacadeInsteadOfFirmwareCatalogs()
    {
        string project = ReadText("src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj");
        string presentationSource = ReadPresentationSources();
        string[] forbiddenTokens =
        [
            "NvtFwCombiner.Application.Composition",
            "NvtFwCombiner.Application.ExternalTools",
            "NvtFwCombiner.Application.FlashMaps",
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
        Assert.Contains("NvtFwCombiner.Application.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Domain.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles.csproj", project, StringComparison.Ordinal);
        Assert.Contains("WorkbenchCompositionService", presentationSource, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Application.HexEditor", presentationSource, StringComparison.Ordinal);
        foreach (string token in forbiddenTokens)
        {
            Assert.DoesNotContain(token, presentationSource, StringComparison.Ordinal);
        }
    }

}
