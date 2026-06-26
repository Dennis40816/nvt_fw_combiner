using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;

namespace NvtFwCombiner.Architecture.Tests.ExternalTools;

public sealed class ExternalCombinerToolManifestValidatorTests
{
    [Fact]
    public void ValidateAcceptsVersionOneTenAsStringToken()
    {
        ExternalCombinerToolManifest manifest = ValidManifest() with
        {
            ToolVersion = "1.10",
            ToolBindingId = "legacy-combiner-1-10"
        };

        IReadOnlyList<string> errors = new ExternalCombinerToolManifestValidator().Validate(manifest);

        Assert.Empty(errors);
        Assert.Equal("1.10", manifest.ToolVersion);
    }

    [Fact]
    public void ValidateRejectsExecutablePathTraversal()
    {
        ExternalCombinerToolManifest manifest = ValidManifest() with
        {
            ExecutableName = @"..\combiner.exe"
        };

        IReadOnlyList<string> errors = new ExternalCombinerToolManifestValidator().Validate(manifest);

        Assert.Contains(errors, error => error.Contains("ExecutableName", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsUppercaseSha256()
    {
        ExternalCombinerToolManifest manifest = ValidManifest() with
        {
            Sha256 = new string('A', 64)
        };

        IReadOnlyList<string> errors = new ExternalCombinerToolManifestValidator().Validate(manifest);

        Assert.Contains(errors, error => error.Contains("Sha256", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsUnsupportedArgumentToken()
    {
        ExternalCombinerToolManifest manifest = ValidManifest() with
        {
            ArgumentTemplate = ["{staging.workBin}", "{user.path}"]
        };

        IReadOnlyList<string> errors = new ExternalCombinerToolManifestValidator().Validate(manifest);

        Assert.Contains(errors, error => error.Contains("unsupported token", StringComparison.Ordinal));
    }

    private static ExternalCombinerToolManifest ValidManifest()
    {
        return new ExternalCombinerToolManifest(
            SchemaVersion: "1.0",
            ToolBindingId: "legacy-combiner-1-10",
            ToolId: "legacy-combiner",
            ToolVersion: "1.10",
            DisplayName: "Legacy Combiner 1.10",
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
