namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Shared repository-source helpers for focused architecture boundary suites.</summary>
[Collection(ArchitectureBoundaryCollection.Name)]
public abstract partial class RepositoryBoundaryTestBase
{
    private protected static void AssertArchitectureBoundaryTestsUseReviewedSerialTopology()
    {
        string directory = Path.Combine(
            Root.FullName,
            "tests",
            "NvtFwCombiner.Architecture.Tests");
        (string Name, string Source)[] boundaryFiles =
        [
            .. Directory.GetFiles(directory, "RepositoryBoundaryTests*.cs", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(path => (Path.GetFileName(path), File.ReadAllText(path))),
        ];
        string sources = string.Join(
            Environment.NewLine,
            boundaryFiles.Select(static file => file.Source));

        Assert.DoesNotContain(
            "public sealed partial class " + "RepositoryBoundaryTests",
            sources,
            StringComparison.Ordinal);

        string[] expectedBoundaryClasses =
        [
            nameof(ApplicationBoundaryTests),
            nameof(BootstrapCliBoundaryTests),
            nameof(CanonicalCompositionBoundaryTests),
            nameof(InfrastructureBoundaryTests),
            nameof(PackageTrustIndexBoundaryTests),
            nameof(PresentationBoundaryTests),
            nameof(ProfileBoundaryTests),
            nameof(RepositoryGovernanceBoundaryTests),
            nameof(RetirementBoundaryTests),
        ];
        Type[] boundaryTypes =
        [
            .. typeof(RepositoryBoundaryTestBase).Assembly.GetTypes()
                .Where(type => type.IsSubclassOf(typeof(RepositoryBoundaryTestBase)) &&
                    !type.IsAbstract)
                .OrderBy(static type => type.Name, StringComparer.Ordinal),
        ];
        Assert.Equal(expectedBoundaryClasses, boundaryTypes.Select(static type => type.Name));

        string[] allTestClasses =
        [
            .. boundaryTypes.Select(static type => type.Name),
            nameof(ProjectDependencyTests),
        ];
        Assert.Equal(
            allTestClasses.Order(StringComparer.Ordinal),
            typeof(RepositoryBoundaryTestBase).Assembly.GetTypes()
                .Where(static type => type.GetMethods().Any(static method =>
                    method.IsDefined(typeof(FactAttribute), inherit: true) ||
                    method.IsDefined(typeof(TheoryAttribute), inherit: true)))
                .Select(static type => type.Name)
                .Order(StringComparer.Ordinal));

        Assert.All(
            boundaryTypes,
            static type =>
            {
                Xunit.v3.ICollectionAttribute collection = Assert.Single(
                    type.GetCustomAttributes(inherit: true)
                        .OfType<Xunit.v3.ICollectionAttribute>());
                Assert.Equal(ArchitectureBoundaryCollection.Name, collection.Name);

                int methodCount = type.GetMethods().Count(static method =>
                    method.IsDefined(typeof(FactAttribute), inherit: true) ||
                    method.IsDefined(typeof(TheoryAttribute), inherit: true));
                Assert.True(
                    methodCount <= 35,
                    $"{type.Name} declares {methodCount} test methods; the limit is 35.");
            });
        Assert.Empty(
            typeof(ProjectDependencyTests).GetCustomAttributes(inherit: true)
                .OfType<Xunit.v3.ICollectionAttribute>());

        string[] processOwners =
        [
            .. boundaryFiles
                .Where(static file => file.Source.Contains(
                    "ProcessStart" + "Info",
                    StringComparison.Ordinal))
                .Select(static file => file.Name),
        ];
        Assert.Equal(["RepositoryBoundaryTests.PackageTrustIndex.cs"], processOwners);

        string packageTrust = Assert.Single(
            boundaryFiles,
            static file => file.Name == "RepositoryBoundaryTests.PackageTrustIndex.cs").Source;
        Assert.Contains(
            "[Collection(ArchitectureBoundaryCollection.Name)]",
            packageTrust,
            StringComparison.Ordinal);
        Assert.Contains("[CollectionDefinition(Name)]", packageTrust, StringComparison.Ordinal);
        Assert.DoesNotContain("DisableParallelization", packageTrust, StringComparison.Ordinal);
    }

    private protected static void AssertBootstrapTestsDoNotMutateTheSharedCatalogPublication()
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

    private protected static void AssertUiRuntimeControlConstructionIsSerialized()
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
            groups,
            StringComparison.Ordinal);
    }
}
