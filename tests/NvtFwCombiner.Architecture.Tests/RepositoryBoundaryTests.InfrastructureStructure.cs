namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
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
        Assert.Contains("TryDeleteDirectory", staging, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ExternalCombinerToolResolver", toolResolution, StringComparison.Ordinal);
        Assert.Contains("_toolResolver.TryResolve", root, StringComparison.Ordinal);
        Assert.Contains("_toolResolver.TryResolve", legacyRoot, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA256", legacyRoot, StringComparison.Ordinal);
        Assert.Contains("GetLowerSha256", toolResolution, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashData", toolResolution, StringComparison.Ordinal);
    }
}
