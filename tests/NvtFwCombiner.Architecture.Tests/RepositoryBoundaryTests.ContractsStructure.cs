namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class ApplicationBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Locks the manifest to one compact class transport without record value semantics.</summary>
    [Fact]
    public void ExternalCombinerManifestKeepsOnePrimaryClassConstructor()
    {
        string manifest = ReadText(
            "src/NvtFwCombiner.Contracts/ExternalTools/ExternalCombinerToolManifest.cs")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "public sealed class ExternalCombinerToolManifest(\n    string schemaVersion,",
            manifest,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public ExternalCombinerToolManifest(",
            manifest,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "record ExternalCombinerToolManifest",
            manifest,
            StringComparison.Ordinal);
    }
}
