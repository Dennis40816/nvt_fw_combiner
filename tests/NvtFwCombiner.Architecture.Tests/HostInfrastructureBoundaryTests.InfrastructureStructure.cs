namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class HostInfrastructureBoundaryTests
{
    /// <summary>Locks the one private bundle snapshot fast path without weakening general caller isolation.</summary>
    [Fact]
    public void ProfileBundleJsonParsingKeepsExplicitBufferOwnership()
    {
        string reader = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/StrictJsonDocumentReader.cs");
        string bundleSnapshot = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleFileSnapshot.cs");
        string infrastructureRoot = Path.Combine(Root.FullName, "src", "NvtFwCombiner.Infrastructure");
        string[] snapshotCallers = [.. Directory.EnumerateFiles(
                    Path.Combine(Root.FullName, "src", "NvtFwCombiner.Infrastructure"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(
                    "StrictJsonDocumentReader.ParseOwnedSnapshot(", StringComparison.Ordinal))];

        Assert.Contains("byte[] snapshot = utf8Json.ToArray();", reader, StringComparison.Ordinal);
        Assert.Contains("The caller must keep the memory unchanged", reader, StringComparison.Ordinal);
        Assert.Contains(
            "StrictJsonDocumentReader.ParseOwnedSnapshot(",
            bundleSnapshot,
            StringComparison.Ordinal);
        string[] expectedCallers = ["Bundles/ProfileBundleFileSnapshot.cs", "Bundles/ProfileBundlePackageTrustIndex.cs",
            "Configuration/EventBufferFormatConfigurationStorage.cs",
            "Configuration/ToolchainRuntimeConfigurationStorage.cs"];
        Assert.Equal(expectedCallers.Order(StringComparer.Ordinal), snapshotCallers
            .Select(path => Path.GetRelativePath(infrastructureRoot, path).Replace('\\', '/')).Order(StringComparer.Ordinal));
        Assert.All(snapshotCallers, path => Assert.Equal(1,
            CountOccurrences(File.ReadAllText(path), "StrictJsonDocumentReader.ParseOwnedSnapshot(")));
    }

    /// <summary>Protocol adapters share host custody, constrained resolution and bounded immutable-artifact verification.</summary>
    [Fact]
    public void ExternalCombinerProcessorsShareConstrainedToolResolution()
    {
        string host = ReadExternalToolSources("StagedExternalProcessorHost*.cs");
        string adapters = ReadExternalToolSources("LegacyCombinerProcessorAdapter*.cs");
        string manifest = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerProcessor.cs");
        string postbuild = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.cs");
        string manifestStaging = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerProcessorAdapter.ManifestStaging.cs");
        string postbuildStaging = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerProcessorAdapter.PostbuildStaging.cs");
        string toolResolution = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerToolResolver.cs");
        string artifactVerifier = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/StagedArtifactFileVerifier.cs");
        string processors = host + adapters + manifest + postbuild;

        Assert.Contains("public sealed partial class StagedExternalProcessorHost", host, StringComparison.Ordinal);
        Assert.Contains("public sealed class ExternalCombinerProcessor", manifest, StringComparison.Ordinal);
        Assert.Contains("TransformAsync", manifest, StringComparison.Ordinal);
        Assert.Contains("_host.TransformAsync(request, cancellationToken)", manifest, StringComparison.Ordinal);
        Assert.Contains("_host.TransformAsync(request, cancellationToken)", postbuild, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool TryResolveManifest", processors, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool TryResolveExecutable", processors, StringComparison.Ordinal);
        Assert.DoesNotContain("private static IReadOnlyList<string> ExpandArguments", host, StringComparison.Ordinal);
        Assert.DoesNotContain("private static CompositionIssue? FindUnexpectedStagingFileIssue", host, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string GetLowerSha256", processors, StringComparison.Ordinal);
        Assert.DoesNotContain("_toolResolver", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("_toolResolver", postbuild, StringComparison.Ordinal);
        Assert.Contains("_toolResolver.TryResolve", host, StringComparison.Ordinal);
        Assert.Contains("ExpandArguments", manifestStaging, StringComparison.Ordinal);
        Assert.Contains("FindUnexpectedStagingFileIssue", manifestStaging, StringComparison.Ordinal);
        Assert.Contains("using ExternalStagingDirectory? staging = ExternalStagingDirectory.TryAcquire(session.RunDirectory);", host, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalStagingDirectory.TryDelete", processors, StringComparison.Ordinal);
        Assert.DoesNotContain("TryDeleteDirectory", processors, StringComparison.Ordinal);
        Assert.Contains("File.WriteAllBytesAsync(session.InputPath, session.Request.InputBytes,", host, StringComparison.Ordinal);
        Assert.Contains("File.WriteAllBytesAsync(path, artifact.Bytes,", manifestStaging, StringComparison.Ordinal);
        Assert.DoesNotContain("InputBytes.ToArray()", processors, StringComparison.Ordinal);
        Assert.DoesNotContain("artifact.Bytes.ToArray()", processors, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA256", processors, StringComparison.Ordinal);
        Assert.Contains("StagedArtifactFileVerifier", manifestStaging, StringComparison.Ordinal);
        Assert.Contains("StagedArtifactFileVerifier", postbuildStaging, StringComparison.Ordinal);
        Assert.Contains(".MatchesAsync", manifestStaging, StringComparison.Ordinal);
        Assert.Contains(".MatchesAsync", postbuildStaging, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytesAsync(path", manifestStaging, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytesAsync(artifactPath", postbuildStaging, StringComparison.Ordinal);
        Assert.Contains("ArrayPool<byte>.Shared.Rent", artifactVerifier, StringComparison.Ordinal);
        Assert.Contains("ArrayPool<byte>.Shared.Return(buffer, clearArray: true)", artifactVerifier, StringComparison.Ordinal);
        Assert.Contains("stream.Length != expectedBytes.Length", artifactVerifier, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ExternalCombinerToolResolver", toolResolution, StringComparison.Ordinal);
        Assert.Contains("GetLowerSha256", toolResolution, StringComparison.Ordinal);
        Assert.Contains("RegularFileGuard.RequireOpenHandle", toolResolution, StringComparison.Ordinal);
        Assert.Contains("IncrementalHash.CreateHash(HashAlgorithmName.SHA256)", toolResolution, StringComparison.Ordinal);
        Assert.Contains("cancellationToken.ThrowIfCancellationRequested()", toolResolution, StringComparison.Ordinal);
        Assert.Contains("stream.Length == length", toolResolution, StringComparison.Ordinal);
    }

    /// <summary>Each compiled command verifies artifacts, normalizes short output, then validates the one staging policy.</summary>
    [Fact]
    public void LegacyPostbuildRetainsOnePolicyAndPerCommandValidationOrder()
    {
        string postbuildExecution = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerProcessorAdapter.Postbuild.cs");
        string postbuildStaging = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerProcessorAdapter.PostbuildStaging.cs");
        string processors = ReadExternalToolSources("StagedExternalProcessorHost*.cs") +
            ReadExternalToolSources("LegacyCombinerProcessorAdapter*.cs");
        string normalizedPostbuild = postbuildExecution.ReplaceLineEndings("\n");
        int commandLoopStart = postbuildExecution.IndexOf(
            "foreach (ExternalProcessorProtocolCommand command in commandPlan.Commands)", StringComparison.Ordinal);
        int commandLoopEnd = postbuildExecution.IndexOf(
            "// The last per-command check follows every protocol staging mutation; the host now audits the final image.",
            StringComparison.Ordinal);
        int commandExecution = postbuildExecution.IndexOf(
            "CompositionIssue? commandIssue = await ExecutePostbuildCommandAsync(", StringComparison.Ordinal);
        int commandMethodStart = postbuildExecution.IndexOf(
            "private static async ValueTask<CompositionIssue?> ExecutePostbuildCommandAsync(", StringComparison.Ordinal);
        int commandMethodEnd = postbuildExecution.IndexOf(
            "private static string[] ResolveProtocolArguments(", StringComparison.Ordinal);
        int artifactVerification = postbuildExecution.IndexOf(
            "CompositionIssue? artifactMutationIssue = await VerifyStagedArtifactsUnchangedAsync(", StringComparison.Ordinal);
        int shortOutputNormalization = postbuildExecution.IndexOf(
            "CompositionIssue? lengthIssue = await NormalizeShortenedFirmwareAsync(", StringComparison.Ordinal);
        int stagingValidation = postbuildExecution.IndexOf(
            "ValidateStagingTree(session.RunDirectory, stagingTreePolicy)", StringComparison.Ordinal);

        Assert.Contains("ReadOnlyMemory<byte> inputBytes = request.InputBytes;", postbuildExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("request.InputBytes.ToArray()", postbuildExecution, StringComparison.Ordinal);
        // Base 434920c already copies inputBytes here to apply BIN source overrides independently of the image.
        Assert.Contains("byte[] stagedSourceBytes = inputBytes.ToArray();", postbuildExecution, StringComparison.Ordinal);
        // Its exact-artifact BIN projection also owned a mutable copy, separate from artifact verification.
        Assert.Contains("byte[] exactFileBytes = fileArtifact.Bytes.ToArray();", postbuildStaging, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(postbuildExecution, "CreateStagingTreePolicy("));
        Assert.Equal(1, CountOccurrences(postbuildExecution, "ValidateStagingTree("));
        Assert.Equal(2, CountOccurrences(processors, "CreateStagingTreePolicy("));
        Assert.Equal(2, CountOccurrences(processors, "ValidateStagingTree("));
        Assert.Contains(
            "StagingTreePolicy stagingTreePolicy = CreateStagingTreePolicy(session.Manifest, commandPlan);\n"
                + "        foreach (ExternalProcessorProtocolCommand command in commandPlan.Commands)",
            normalizedPostbuild,
            StringComparison.Ordinal);
        Assert.True(commandLoopStart >= 0);
        Assert.True(commandLoopEnd > commandLoopStart);
        Assert.InRange(commandExecution, commandLoopStart, commandLoopEnd);
        Assert.Contains(
            "session, command, stagedSourceBytes, stagingTreePolicy, cancellationToken)",
            postbuildExecution,
            StringComparison.Ordinal);
        Assert.True(commandMethodStart > commandLoopEnd);
        Assert.True(commandMethodEnd > commandMethodStart);
        Assert.InRange(artifactVerification, commandMethodStart, commandMethodEnd);
        Assert.InRange(shortOutputNormalization, artifactVerification, commandMethodEnd);
        Assert.InRange(stagingValidation, commandMethodStart, commandMethodEnd);
        Assert.True(stagingValidation > shortOutputNormalization);
        Assert.Contains(
            "StagingTreePolicy stagingTreePolicy = CreateStagingTreePolicy(session.Manifest, commandPlan);",
            postbuildExecution,
            StringComparison.Ordinal);
        Assert.Contains(
            "return lengthIssue ?? ValidateStagingTree(session.RunDirectory, stagingTreePolicy);",
            postbuildExecution,
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(postbuildStaging, "private static StagingTreePolicy CreateStagingTreePolicy("));
        Assert.Equal(1, CountOccurrences(postbuildStaging, "private static CompositionIssue? ValidateStagingTree("));
    }

    private static string ReadExternalToolSources(string pattern)
    {
        return string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(Root.FullName, "src", "NvtFwCombiner.Infrastructure", "ExternalTools"), pattern)
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText));
    }
}
