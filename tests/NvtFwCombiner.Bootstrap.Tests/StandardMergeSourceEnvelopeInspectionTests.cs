using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Accepted-input evidence from the real NT51950/951 Standard bundle.</summary>
public sealed class StandardMergeSourceEnvelopeInspectionTests
{
    /// <summary>The reviewed TP artifact must expose its unique FWConfig Backup inside the compiled source view.</summary>
    [Fact]
    public void Nt51950GoldenTpBackupRemainsInsideAcceptedSourceView()
    {
        using JsonDocument manifest = CanonicalGoldenTestData.LoadDirectWorkflowManifest(
            ExperienceIds.StandardMerge);
        JsonElement goldenCase = manifest.RootElement.GetProperty("cases")
            .EnumerateArray().Single(static entry => entry.GetProperty("ic").GetString() == "51950");
        JsonElement inputs = goldenCase.GetProperty("inputs");
        byte[] dp = File.ReadAllBytes(RepositoryPaths.ManifestPath(
            CanonicalGoldenTestData.Root, inputs.GetProperty("dp-input")));
        byte[] tp = File.ReadAllBytes(RepositoryPaths.ManifestPath(
            CanonicalGoldenTestData.Root, inputs.GetProperty("tp-input")));
        Assert.True(BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(
            "NT51950", dp, ["dp-input", "tp-input"],
            out CompiledComposition? composition, out _, out IReadOnlyList<CompositionIssue> issues),
            string.Join(" | ", issues.Select(static issue => issue.Message)));

        CompiledInputArtifactInspectionResult inspection =
            CompiledInputArtifactInspectionService.Inspect(
                Assert.IsType<CompiledComposition>(composition), "tp-input", tp);
        Assert.False(inspection.BlocksBuild,
            $"issue={inspection.IssueCode}; source={tp.Length}; accepted={inspection.AcceptedSnapshotRange}");
        Assert.Equal(tp.Length, inspection.AcceptedSnapshotRange?.Length);
    }

    /// <summary>A captured nonstandard DP is complete and advisory before Build.</summary>
    [Theory]
    [InlineData("NT51950", 0x40001)]
    [InlineData("NT51951", 0x80001)]
    public void NonstandardCapturedDpIsAcceptedWithVisibleAdvisory(string icId, int length)
    {
        byte[] dp = new byte[length];
        dp[0] = 0x13;
        dp[^1] = 0x92;
        bool compiled = BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(
            icId, dp, ["dp-input", "tp-input"],
            out CompiledComposition? composition,
            out ResolvedCapability? capability,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.NotNull(capability);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(length, artifact.Plan.OutputInitialization.Capacity);
        CompiledInputArtifactInspectionResult inspection =
            CompiledInputArtifactInspectionService.Inspect(artifact, "dp-input", dp);
        Assert.False(inspection.BlocksBuild);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Warning, inspection.Severity);
        Assert.Equal("DP_NONSTANDARD_SIZE_WARNING", inspection.IssueCode);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.ReviewUnexpectedOuterLength,
            inspection.NextAction);
        Assert.Equal(length, inspection.AcceptedSnapshotRange?.Length);
        Assert.Null(inspection.IgnoredTrailingRange);
        Assert.Equal([0x40000, 0x80000, 0x100000], inspection.ExpectedOuterLengths);
    }

    /// <summary>Existing exact map routes retain their original ready input projection.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void ExactCapturedDpKeepsTheOriginalMapAndReadyInspection(string icId)
    {
        ArgumentNullException.ThrowIfNull(icId);
        byte[] dp = new byte[0x40000];
        dp[0] = 0x13;
        bool compiled = BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(
            icId, dp, ["dp-input", "tp-input"],
            out CompiledComposition? composition,
            out _, out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal($"{icId.ToLowerInvariant()}-standard-merge-256k",
            artifact.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Null(Assert.IsType<ResolvedMapV2CompilationContext>(
            artifact.V2Details.Provenance.Context).SourceEnvelope);
        CompiledInputArtifactInspectionResult inspection =
            CompiledInputArtifactInspectionService.Inspect(artifact, "dp-input", dp);
        Assert.Equal(InputArtifactInspectionIssueCodes.Ready, inspection.IssueCode);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Valid, inspection.Severity);
    }
}
