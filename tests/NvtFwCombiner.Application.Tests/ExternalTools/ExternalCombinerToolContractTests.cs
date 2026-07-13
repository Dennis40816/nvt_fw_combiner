using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

/// <summary>Executable tests for external combiner tool manifest and registry contracts.</summary>
public sealed class ExternalCombinerToolContractTests
{
    /// <summary>Rejects executable names that would escape the approved tool package.</summary>
    [Fact]
    public void ManifestValidatorRejectsPathTraversalExecutableNames()
    {
        ExternalCombinerToolManifest manifest = ValidManifest(executableName: @"..\combiner.exe");

        IReadOnlyList<string> errors = ExternalCombinerToolManifestValidator.Validate(manifest);

        Assert.Contains(errors, error => error.Contains("ExecutableName", StringComparison.Ordinal));
    }

    /// <summary>Rejects non-canonical SHA casing and unsupported host expansion tokens.</summary>
    [Fact]
    public void ManifestValidatorRejectsUppercaseShaAndUnsupportedTokens()
    {
        ExternalCombinerToolManifest manifest = ValidManifest(
            sha256: new string('A', 64),
            argumentTemplate: ["--input", "{staging.hostPath}", "--output", "{staging.outputBin}"]);

        IReadOnlyList<string> errors = ExternalCombinerToolManifestValidator.Validate(manifest);

        Assert.Contains(errors, error => error.Contains("Sha256", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("{staging.hostPath}", StringComparison.Ordinal));
    }

    /// <summary>Accepts only closed lower-case named artifact tokens in approved manifests.</summary>
    [Fact]
    public void ManifestValidatorAcceptsNamedArtifactTokenAndRejectsMalformedId()
    {
        ExternalCombinerToolManifest valid = ValidManifest(
            argumentTemplate: ["--a", "{staging.artifact.a-bank}", "--output", "{staging.outputBin}"]);
        ExternalCombinerToolManifest malformed = ValidManifest(
            argumentTemplate: ["--a", "{staging.artifact.A-Bank}", "--output", "{staging.outputBin}"]);

        Assert.Empty(ExternalCombinerToolManifestValidator.Validate(valid));
        Assert.Contains(
            ExternalCombinerToolManifestValidator.Validate(malformed),
            error => error.Contains("{staging.artifact.A-Bank}", StringComparison.Ordinal));
    }

    /// <summary>Rejects malformed named-artifact collections before their items are sorted for deterministic staging.</summary>
    [Fact]
    public void ExternalProcessorRequestRejectsNullArtifactEntry()
    {
        _ = Assert.Throws<ArgumentException>(() => new ExternalProcessorRequest(
            "run-id",
            "processor-v1",
            "tool-v1",
            new byte[] { 0 },
            [new ByteRange(0, 1)],
            stagedArtifacts: [null!]));
    }

    /// <summary>Rejects duplicate profile binding ids before runtime selection is possible.</summary>
    [Fact]
    public void RegistryRejectsDuplicateBindingIds()
    {
        ExternalCombinerToolManifest first = ValidManifest(toolBindingId: "legacy-combiner");
        ExternalCombinerToolManifest second = ValidManifest(toolBindingId: "legacy-combiner", toolId: "legacy-combiner-alt");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new ExternalCombinerToolRegistry([first, second]));

        Assert.Contains("declared more than once", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Preserves exact external tool version strings such as 1.10 during registry resolution.</summary>
    [Fact]
    public void RegistryPreservesExactToolVersionStrings()
    {
        ExternalCombinerToolManifest manifest = ValidManifest(toolVersion: "1.10");
        ExternalCombinerToolRegistry registry = new([manifest]);

        ExternalCombinerToolManifest resolved = registry.Resolve("legacy-combiner");

        Assert.Equal("1.10", resolved.ToolVersion);
    }

    private static ExternalCombinerToolManifest ValidManifest(
        string schemaVersion = "1.0",
        string toolBindingId = "legacy-combiner",
        string toolId = "legacy-combiner",
        string toolVersion = "1.10",
        string displayName = "Legacy Combiner",
        string platform = "win-x64",
        string executableName = "combiner.exe",
        string sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        string adapterId = "legacy-combiner-v1",
        string inputMode = "input-output-file",
        IReadOnlyList<string>? argumentTemplate = null,
        string workingDirectoryPolicy = "staging-directory",
        int timeoutSeconds = 30,
        IReadOnlyList<string>? allowedExtraOutputFiles = null)
    {
        return new ExternalCombinerToolManifest(
            schemaVersion,
            toolBindingId,
            toolId,
            toolVersion,
            displayName,
            platform,
            executableName,
            sha256,
            adapterId,
            inputMode,
            argumentTemplate ?? ["--input", "{staging.workBin}", "--output", "{staging.outputBin}"],
            workingDirectoryPolicy,
            timeoutSeconds,
            allowedExtraOutputFiles ?? []);
    }
}
