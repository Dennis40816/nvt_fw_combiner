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
        string directory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "ViewModels");
        string viewModels = string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "*.cs").Select(File.ReadAllText));

        Assert.Contains("byte range arithmetic", boundaries, StringComparison.Ordinal);
        Assert.Contains("CRC/Header calculation or `combiner.exe` invocation", boundaries, StringComparison.Ordinal);
        Assert.Contains("No `File.ReadAllBytes` or `Process.Start` in ViewModels", boundaries, StringComparison.Ordinal);
        Assert.DoesNotMatch(PublicViewModelTypeRegex(), viewModels);
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"(?m)^public\s+(?:(?:sealed|abstract|static|partial|readonly)\s+)*(?:(?:class|record|interface|struct|delegate|enum)\s+)")]
    private static partial System.Text.RegularExpressions.Regex PublicViewModelTypeRegex();

    /// <summary>Verifies Presentation consumes focused Application contracts, never concrete firmware adapters.</summary>
    [Fact]
    public void PresentationUsesFocusedApplicationContractsInsteadOfConcreteAdapters()
    {
        string project = ReadText("src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj");
        string compositionRoot = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/PresentationCompositionServices.cs");
        string hostServices = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/PresentationHostServices.cs");
        string desktopProgram = ReadText(
            "src/NvtFwCombiner.Desktop/Program.cs");
        string applicationContracts = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs");
        string applicationGlobalUsing = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ApplicationCompositionGlobalUsings.cs");
        string presentationConsumers = ReadPresentationSources(
            "PresentationCompositionServices.cs",
            "PresentationHostServices.cs");
        string injectedViewModels = string.Join(
            Environment.NewLine,
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowContextSetupViewModel.cs"));
        string progressProjection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/CompositionRunProgressViewModel.cs");
        string progressResources = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ShellTextResources.RunProgress.cs");
        string progressConsumer = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/CompositionRunPresentationViewModel.cs");
        string inspectionProjection = string.Join(
            Environment.NewLine,
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHexDiffViewModel.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHexDiffRangeViewModels.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHexDiffViewportAdapter.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.Bindings.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.Factory.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.OutputDifferences.cs"));
        string externalLifecycleConsumers = string.Join(
            Environment.NewLine,
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/PresentationHostServices.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MessageCenterViewModel.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ShellTextResources.MessageCenter.cs"));
        string presentationSource = ReadPresentationSources(
            "PresentationHostServices.cs",
            "MessageCenterViewModel.cs",
            "ShellTextResources.MessageCenter.cs",
            "CompositionRunProgressViewModel.cs",
            "ShellTextResources.RunProgress.cs",
            "CompositionRunPresentationViewModel.cs",
            "CompositionRunContracts.cs",
            "ReportPresentationViewModel.cs",
            "ReportHexDiffViewModel.cs",
            "ReportHexDiffRangeViewModels.cs",
            "ReportHexDiffViewportAdapter.cs",
            "ReportReviewViewModel.cs",
            "ReportReviewViewModel.Bindings.cs",
            "ReportReviewViewModel.Factory.cs",
            "ReportReviewViewModel.OutputDifferences.cs");
        string[] forbiddenTokens =
        [
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

        Assert.DoesNotContain("NvtFwCombiner.Bootstrap.csproj", project, StringComparison.Ordinal);
        Assert.Contains("NvtFwCombiner.Application.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Domain.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchCompositionService", presentationSource, StringComparison.Ordinal);
        Assert.Contains("ICompositionCapabilityExperience", compositionRoot, StringComparison.Ordinal);
        Assert.Contains("IStandardMergeAuthoring", compositionRoot, StringComparison.Ordinal);
        Assert.Contains("IAbMergeAuthoring", compositionRoot, StringComparison.Ordinal);
        Assert.Contains("IDpReplaceAuthoring", compositionRoot, StringComparison.Ordinal);
        Assert.Contains("IGeneralAuthoring", compositionRoot, StringComparison.Ordinal);
        Assert.Contains("ICtrlRamAuthoring", compositionRoot, StringComparison.Ordinal);
        Assert.Contains("ICompositionOutputNaming", compositionRoot, StringComparison.Ordinal);
        Assert.Contains("ICompositionExecution", compositionRoot, StringComparison.Ordinal);
        Assert.Contains("interface ICompositionCapabilityExperience", applicationContracts, StringComparison.Ordinal);
        Assert.Contains("interface ICtrlRamAuthoring", applicationContracts, StringComparison.Ordinal);
        Assert.Contains("interface ICompositionOutputNaming", applicationContracts, StringComparison.Ordinal);
        Assert.Contains("interface ICompositionExecution", applicationContracts, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.Execution", presentationConsumers, StringComparison.Ordinal);
        Assert.Contains("_compositionServices.OutputNaming", presentationConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("_compositionServices.AbMergeDeliveryPlanning", presentationConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("_compositionServices.Memory", presentationConsumers, StringComparison.Ordinal);
        Assert.Contains("public sealed class PresentationCompositionServices", compositionRoot, StringComparison.Ordinal);
        Assert.DoesNotContain("static class PresentationCompositionServices", compositionRoot, StringComparison.Ordinal);
        Assert.DoesNotContain("PresentationCompositionServices.", presentationConsumers, StringComparison.Ordinal);
        Assert.Contains("CompositionHostServices", desktopProgram, StringComparison.Ordinal);
        Assert.Contains("PresentationHostServices", hostServices, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionHostServices", presentationConsumers, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(
            injectedViewModels,
            "private readonly PresentationCompositionServices _compositionServices;"));
        string[] concreteAdapterTokens =
        [
            "CanonicalCapabilityProjection",
            "CanonicalAuthoringAdapter",
            "CompositionAuthoringSessionAdapter",
            "CompositionExecutionAdapter",
            "CompositionMemoryProjection",
            "FirmwareInspectionAdapter",
            "CompositionOutputNaming.",
        ];
        foreach (string token in concreteAdapterTokens)
        {
            Assert.DoesNotContain(token, presentationConsumers, StringComparison.Ordinal);
        }

        Assert.Contains("NvtFwCombiner.Application.HexEditor", presentationSource, StringComparison.Ordinal);
        Assert.Contains("global using NvtFwCombiner.Application.Composition", applicationGlobalUsing, StringComparison.Ordinal);
        Assert.Contains("CompositionRunPhase", progressProjection, StringComparison.Ordinal);
        Assert.Contains("CompositionRunPhase", progressResources, StringComparison.Ordinal);
        Assert.Contains("CompositionRunProgressFeed", progressConsumer, StringComparison.Ordinal);
        Assert.Contains("CompositionRunInspectionSnapshot", inspectionProjection, StringComparison.Ordinal);
        Assert.Contains("IExternalProcessorEnvironmentLoader", externalLifecycleConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure", externalLifecycleConsumers, StringComparison.Ordinal);
        Assert.DoesNotContain("new ExternalProcessorEnvironmentLoader", externalLifecycleConsumers, StringComparison.Ordinal);
        foreach (string token in forbiddenTokens)
        {
            if (!StringComparer.Ordinal.Equals(token, "NvtFwCombiner.Domain."))
            {
                Assert.DoesNotContain(token, presentationSource, StringComparison.Ordinal);
            }
            Assert.DoesNotContain(token, progressProjection, StringComparison.Ordinal);
            Assert.DoesNotContain(token, progressResources, StringComparison.Ordinal);
            Assert.DoesNotContain(token, progressConsumer, StringComparison.Ordinal);
            if (!StringComparer.Ordinal.Equals(token, "NvtFwCombiner.Domain."))
            {
                Assert.DoesNotContain(token, inspectionProjection, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>Keeps Windows process launch inside one constrained Infrastructure adapter.</summary>
    [Fact]
    public void FileRevealProcessAuthorityStaysOutsidePresentation()
    {
        string port = ReadText("src/NvtFwCombiner.Application/Ports/IFileRevealService.cs");
        string hostFactory = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
        string adapter = ReadText("src/NvtFwCombiner.Infrastructure/Shell/WindowsExplorerFileRevealService.cs");
        string presentation = string.Join(
            Environment.NewLine,
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs"),
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.BuildCompleted.cs"));

        Assert.Contains("interface IFileRevealService", port, StringComparison.Ordinal);
        Assert.Contains("new WindowsExplorerFileRevealService()", hostFactory, StringComparison.Ordinal);
        Assert.Contains("Environment.SpecialFolder.Windows", adapter, StringComparison.Ordinal);
        Assert.Contains("Path.IsPathFullyQualified", adapter, StringComparison.Ordinal);
        Assert.Contains("UseShellExecute = false", adapter, StringComparison.Ordinal);
        Assert.Contains("Process.Start(startInfo)", adapter, StringComparison.Ordinal);
        Assert.Contains("IFileRevealService", presentation, StringComparison.Ordinal);
        Assert.Contains("RevealFileCommand", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Infrastructure", presentation, StringComparison.Ordinal);
    }

}
