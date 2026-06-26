using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;

namespace NvtFwCombiner.Architecture.Tests.ExternalTools;

public sealed class ExternalCombinerToolManifestValidatorTests
{
    [Fact]
    public void ValidateAcceptsVersionOneTenAsStringToken()
    {
        ExternalCombinerToolManifest manifest = ValidManifest(toolVersion: "1.10", toolBindingId: "legacy-combiner-1-10");

        IReadOnlyList<string> errors = new ExternalCombinerToolManifestValidator().Validate(manifest);

        Assert.Empty(errors);
        Assert.Equal("1.10", manifest.ToolVersion);
    }

    [Fact]
    public void ValidateRejectsExecutablePathTraversal()
    {
        ExternalCombinerToolManifest manifest = ValidManifest(executableName: @"..\combiner.exe");

        IReadOnlyList<string> errors = new ExternalCombinerToolManifestValidator().Validate(manifest);

        Assert.Contains(errors, error => error.Contains("ExecutableName", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsUppercaseSha256()
    {
        ExternalCombinerToolManifest manifest = ValidManifest(sha256: new string('A', 64));

        IReadOnlyList<string> errors = new ExternalCombinerToolManifestValidator().Validate(manifest);

        Assert.Contains(errors, error => error.Contains("Sha256", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRejectsUnsupportedArgumentToken()
    {
        ExternalCombinerToolManifest manifest = ValidManifest(argumentTemplate: ["{staging.workBin}", "{user.path}"]);

        IReadOnlyList<string> errors = new ExternalCombinerToolManifestValidator().Validate(manifest);

        Assert.Contains(errors, error => error.Contains("unsupported token", StringComparison.Ordinal));
    }

    private static ExternalCombinerToolManifest ValidManifest(
        string toolBindingId = "legacy-combiner-1-10",
        string toolVersion = "1.10",
        string executableName = "combiner.exe",
        string? sha256 = null,
        IReadOnlyList<string>? argumentTemplate = null)
    {
        return new ExternalCombinerToolManifest(
            schemaVersion: "1.0",
            toolBindingId: toolBindingId,
            toolId: "legacy-combiner",
            toolVersion: toolVersion,
            displayName: $"Legacy Combiner {toolVersion}",
            platform: "win-x64",
            executableName: executableName,
            sha256: sha256 ?? new string('a', 64),
            adapterId: "legacy-combiner-inplace-v1",
            inputMode: "in-place",
            argumentTemplate: argumentTemplate ?? ["{staging.workBin}"],
            workingDirectoryPolicy: "staging-directory",
            timeoutSeconds: 5,
            allowedExtraOutputFiles: []);
    }
}
