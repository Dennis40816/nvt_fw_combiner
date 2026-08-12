namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private static void AssertBootstrapTestsDoNotMutateTheSharedCatalogPublication()
    {
        string directory = Path.Combine(
            Root.FullName,
            "tests",
            "NvtFwCombiner.Bootstrap.Tests");
        string sources = string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain(
            "[Collection(CanonicalCapabilityCatalogPublicationGroup.Name)]",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "DisableParallelization",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BootstrapTestHost.Canonical.Catalog.Reload(",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BootstrapTestHost.Services.Catalog",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BootstrapTestHost.Services.WarmCanonicalCapabilities(",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BootstrapTestHost.Services.CreateSystemInformationService(",
            sources,
            StringComparison.Ordinal);

        string dpReplaceSupport = File.ReadAllText(Path.Combine(
            directory,
            "DpReplaceTestSupport.cs"));
        Assert.DoesNotContain("BootstrapTestHost", dpReplaceSupport, StringComparison.Ordinal);
    }

    private static void AssertUiRuntimeControlConstructionIsSerialized()
    {
        string directory = Path.Combine(
            Root.FullName,
            "tests",
            "NvtFwCombiner.UiSmoke.Tests");
        string runtimeAttribute = "[Collection(UiAvaloniaRuntimeCollection.Name)]";
        foreach (string fileName in new[]
                 {
                     "XamlControlStyleContractTests.cs",
                     "SpaciousPanelTests.cs",
                     "ReportHexDiffViewportAdapterTests.cs",
                 })
        {
            Assert.Contains(
                runtimeAttribute,
                File.ReadAllText(Path.Combine(directory, fileName)),
                StringComparison.Ordinal);
        }

        string groups = File.ReadAllText(Path.Combine(directory, "ShellViewModelTestGroups.cs"));
        int runtimeDefinitionStart = groups.IndexOf(
            "[CollectionDefinition(UiAvaloniaRuntimeCollection.Name)]",
            StringComparison.Ordinal);
        int processWideStart = groups.IndexOf(
            "internal static class UiProcessWideObservationCollection",
            StringComparison.Ordinal);
        Assert.True(runtimeDefinitionStart >= 0);
        Assert.True(processWideStart > runtimeDefinitionStart);
        Assert.DoesNotContain(
            "DisableParallelization",
            groups[runtimeDefinitionStart..processWideStart],
            StringComparison.Ordinal);
        Assert.Contains(
            "[Collection(UiProcessWideObservationCollection.Name)]",
            File.ReadAllText(Path.Combine(directory, "AvaloniaApplicationResourceTests.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "[Collection(UiProcessWideObservationCollection.Name)]",
            File.ReadAllText(Path.Combine(directory, "ReportHistoryPersistenceTests.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "[CollectionDefinition(UiProcessWideObservationCollection.Name, DisableParallelization = true)]",
            groups,
            StringComparison.Ordinal);
        Assert.Contains(
            "[Collection(UiProcessWideObservationCollection.Name)]",
            groups,
            StringComparison.Ordinal);
    }
}
