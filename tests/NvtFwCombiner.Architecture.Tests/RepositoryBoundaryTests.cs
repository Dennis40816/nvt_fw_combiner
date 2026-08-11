namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Repository-level architecture boundary checks that do not depend on production assemblies.</summary>
public sealed partial class RepositoryGovernanceBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Verifies architecture tests do not introduce production project references.</summary>
    [Fact]
    public void ArchitectureTestsRemainDependencyFree()
    {
        string project = ReadText("tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj");

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
        AssertArchitectureBoundaryTestsUseReviewedSerialTopology();
        AssertUiRuntimeControlConstructionIsSerialized();
    }

}
