using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;

namespace NvtFwCombiner.Architecture.Tests.ExternalTools;

public sealed class ExternalCombinerToolRegistryTests
{
    [Fact]
    public void ResolveReturnsExactBindingWithoutVersionNormalization()
    {
        ExternalCombinerToolManifest oneNine = Manifest("legacy-combiner-1-9", "1.9");
        ExternalCombinerToolManifest oneTen = Manifest("legacy-combiner-1-10", "1.10");
        ExternalCombinerToolRegistry registry = new([oneNine, oneTen]);

        ExternalCombinerToolManifest resolved = registry.Resolve("legacy-combiner-1-10");

        Assert.Equal("1.10", resolved.ToolVersion);
        Assert.NotEqual(registry.Resolve("legacy-combiner-1-9").ToolVersion, resolved.ToolVersion);
    }

    [Fact]
    public void ConstructorRejectsDuplicateBindingId()
    {
        ExternalCombinerToolManifest first = Manifest("legacy-combiner-1-10", "1.10");
        ExternalCombinerToolManifest duplicate = Manifest("legacy-combiner-1-10", "1.10");

        Assert.Throws<ArgumentException>(() => new ExternalCombinerToolRegistry([first, duplicate]));
    }

    [Fact]
    public void ResolveRejectsUnknownBinding()
    {
        ExternalCombinerToolRegistry registry = new([Manifest("legacy-combiner-1-10", "1.10")]);

        Assert.Throws<KeyNotFoundException>(() => registry.Resolve("legacy-combiner-1-9"));
    }

    private static ExternalCombinerToolManifest Manifest(string bindingId, string version)
    {
        return new ExternalCombinerToolManifest(
            SchemaVersion: "1.0",
            ToolBindingId: bindingId,
            ToolId: "legacy-combiner",
            ToolVersion: version,
            DisplayName: $"Legacy Combiner {version}",
            Platform: "win-x64",
            ExecutableName: "combiner.exe",
            Sha256: new string('a', 64),
            AdapterId: "legacy-combiner-inplace-v1",
            InputMode: "in-place",
            ArgumentTemplate: ["{staging.workBin}"],
            WorkingDirectoryPolicy: "staging-directory",
            TimeoutSeconds: 5,
            AllowedExtraOutputFiles: []);
    }
}
