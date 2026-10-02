namespace NvtFwCombiner.Architecture.Tests;

/// <summary>Feature-scoped ProfileBoundaryTests repository checks.</summary>
[Collection(nameof(RepositoryBoundarySerialGroup))]
public sealed partial class ProfileBoundaryTests
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
