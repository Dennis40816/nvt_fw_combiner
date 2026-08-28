namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Repository-level architecture boundary checks that do not depend on production assemblies.</summary>
public sealed partial class RepositoryBoundaryTests
{
    private static readonly DirectoryInfo Root = LocateRepositoryRoot();

    /// <summary>Verifies architecture tests do not introduce production project references.</summary>
    [Fact]
    public void ArchitectureTestsRemainDependencyFree()
    {
        string project = ReadText("tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj");

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
        AssertUiRuntimeControlConstructionIsSerialized();
    }

    private static void AssertContainsAll(string source, params string[] expected)
    {
        foreach (string value in expected)
        {
            Assert.Contains(value, source, StringComparison.Ordinal);
        }
    }

    private static void AssertDoesNotContainAny(string source, params string[] forbidden)
    {
        foreach (string value in forbidden)
        {
            Assert.DoesNotContain(value, source, StringComparison.Ordinal);
        }
    }

}
