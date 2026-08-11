namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class InfrastructureBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Locks the one private bundle snapshot fast path without weakening general caller isolation.</summary>
    [Fact]
    public void ProfileBundleJsonParsingKeepsExplicitBufferOwnership()
    {
        string reader = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/StrictJsonDocumentReader.cs");
        string bundleSnapshot = ReadText(
            "src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleFileSnapshot.cs");
        string infrastructureSource = string.Concat(
            Directory.EnumerateFiles(
                    Path.Combine(Root.FullName, "src", "NvtFwCombiner.Infrastructure"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.Contains("byte[] snapshot = utf8Json.ToArray();", reader, StringComparison.Ordinal);
        Assert.Contains("The caller must keep the memory unchanged", reader, StringComparison.Ordinal);
        Assert.Contains(
            "StrictJsonDocumentReader.ParseOwnedSnapshot(",
            bundleSnapshot,
            StringComparison.Ordinal);
        Assert.Equal(
            2,
            CountOccurrences(infrastructureSource, "StrictJsonDocumentReader.ParseOwnedSnapshot("));
    }

    /// <summary>Verifies the external combiner adapter root stays focused on staged execution flow.</summary>
    [Fact]
    public void ExternalCombinerProcessorsShareConstrainedToolResolution()
    {
        string root = ReadText("src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerProcessor.cs");
        string staging = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerProcessor.Staging.cs");
        string toolResolution = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalCombinerToolResolver.cs");
        string legacyRoot = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.cs");
        string legacyStaging = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/LegacyCombinerPostbuildProcessor.Staging.cs");
        string stagedArtifactVerifier = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/StagedArtifactFileVerifier.cs");
        string normalizedLegacyRoot = legacyRoot.ReplaceLineEndings("\n");
        int legacyCommandLoopStart = legacyRoot.IndexOf(
            "foreach (ExternalProcessorProtocolCommand command in commandPlan.Commands)",
            StringComparison.Ordinal);
        int legacyCommandLoopEnd = legacyRoot.IndexOf(
            "// Plans are nonempty, and the last per-command check follows every staging mutation.",
            legacyCommandLoopStart,
            StringComparison.Ordinal);
        int legacyStagingValidation = legacyRoot.IndexOf(
            "CompositionIssue? perCommandUnexpectedFileIssue = ValidateStagingTree(runDirectory, stagingTreePolicy);",
            StringComparison.Ordinal);
        int legacyArtifactVerification = legacyRoot.IndexOf(
            "CompositionIssue? artifactMutationIssue = await VerifyStagedArtifactsUnchangedAsync(",
            legacyCommandLoopStart,
            StringComparison.Ordinal);
        int legacyShortOutputNormalization = legacyRoot.IndexOf(
            "CompositionIssue? lengthIssue = await NormalizeShortenedFirmwareAsync(",
            legacyCommandLoopStart,
            StringComparison.Ordinal);

        Assert.Contains("public sealed partial class ExternalCombinerProcessor", root, StringComparison.Ordinal);
        Assert.Contains("TransformAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool TryResolveManifest", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool TryResolveExecutable", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static IReadOnlyList<string> ExpandArguments", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static CompositionIssue? FindUnexpectedStagingFileIssue", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string GetLowerSha256", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA256", root, StringComparison.Ordinal);
        Assert.Contains("ExpandArguments", staging, StringComparison.Ordinal);
        Assert.Contains("FindUnexpectedStagingFileIssue", staging, StringComparison.Ordinal);
        Assert.Contains("ExternalStagingDirectory.TryDelete", root, StringComparison.Ordinal);
        Assert.DoesNotContain("TryDeleteDirectory", staging, StringComparison.Ordinal);
        Assert.Contains("File.WriteAllBytesAsync(workBin, request.InputBytes,", root, StringComparison.Ordinal);
        Assert.Contains("File.WriteAllBytesAsync(path, artifact.Bytes,", staging, StringComparison.Ordinal);
        Assert.DoesNotContain("InputBytes.ToArray()", root, StringComparison.Ordinal);
        Assert.DoesNotContain("artifact.Bytes.ToArray()", staging, StringComparison.Ordinal);
        Assert.Contains("StagedArtifactFileVerifier", staging, StringComparison.Ordinal);
        Assert.Contains("StagedArtifactFileVerifier", legacyStaging, StringComparison.Ordinal);
        Assert.Contains(".MatchesAsync", staging, StringComparison.Ordinal);
        Assert.Contains(".MatchesAsync", legacyStaging, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytesAsync(path", staging, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytesAsync(artifactPath", legacyStaging, StringComparison.Ordinal);
        Assert.Contains("ArrayPool<byte>.Shared.Rent", stagedArtifactVerifier, StringComparison.Ordinal);
        Assert.Contains("ArrayPool<byte>.Shared.Return(buffer, clearArray: true)", stagedArtifactVerifier, StringComparison.Ordinal);
        Assert.Contains("stream.Length != expectedBytes.Length", stagedArtifactVerifier, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ExternalCombinerToolResolver", toolResolution, StringComparison.Ordinal);
        Assert.Contains("_toolResolver.TryResolve", root, StringComparison.Ordinal);
        Assert.Contains("_toolResolver.TryResolve", legacyRoot, StringComparison.Ordinal);
        Assert.Contains("ReadOnlyMemory<byte> inputBytes = request.InputBytes;", legacyRoot, StringComparison.Ordinal);
        Assert.DoesNotContain("request.InputBytes.ToArray()", legacyRoot, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(legacyRoot, "CreateStagingTreePolicy("));
        Assert.Equal(1, CountOccurrences(legacyRoot, "ValidateStagingTree("));
        Assert.Contains(
            "StagingTreePolicy stagingTreePolicy = CreateStagingTreePolicy(resolvedManifest, commandPlan);\n"
                + "            foreach (ExternalProcessorProtocolCommand command in commandPlan.Commands)",
            normalizedLegacyRoot,
            StringComparison.Ordinal);
        Assert.True(legacyCommandLoopStart >= 0);
        Assert.True(legacyCommandLoopEnd > legacyCommandLoopStart);
        Assert.InRange(legacyArtifactVerification, legacyCommandLoopStart, legacyCommandLoopEnd);
        Assert.InRange(legacyShortOutputNormalization, legacyArtifactVerification, legacyCommandLoopEnd);
        Assert.InRange(legacyStagingValidation, legacyCommandLoopStart, legacyCommandLoopEnd);
        Assert.True(legacyStagingValidation > legacyShortOutputNormalization);
        Assert.Contains(
            "StagingTreePolicy stagingTreePolicy = CreateStagingTreePolicy(resolvedManifest, commandPlan);",
            legacyRoot,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompositionIssue? perCommandUnexpectedFileIssue = ValidateStagingTree(runDirectory, stagingTreePolicy);",
            legacyRoot,
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(legacyStaging, "private static StagingTreePolicy CreateStagingTreePolicy("));
        Assert.DoesNotContain("SHA256", legacyRoot, StringComparison.Ordinal);
        Assert.Contains("GetLowerSha256", toolResolution, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashData", toolResolution, StringComparison.Ordinal);
    }
}
