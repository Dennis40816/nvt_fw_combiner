namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Repository-level architecture boundary checks that do not depend on production assemblies.</summary>
public sealed partial class RepositoryBoundaryTests
{

    /// <summary>Verifies production acquisition references never become runtime dependencies.</summary>
    [Fact]
    public void ArchitectureTestsRemainDependencyFree()
    {
        string project = ReadText("tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj");

        EvaluatedProjectGraphTests.AssertArchitectureReferences(System.Xml.Linq.XDocument.Parse(project));
        AssertUiRuntimeControlConstructionIsSerialized();
    }



}
