using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-routing evidence for hash-anchored built-in V2 Standard Merge bundles.</summary>
public sealed class BuiltInV2StandardMergeRoutingTests
{
    /// <summary>Verifies every registered IC selects one deployed V2 artifact without legacy fallback.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-standard-merge-gen-flash-alias", "nt51929-standard-merge", "01b84018c975ee4c7c52b36d594e2919a346294b713e060c496e597237ae3de3", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51920", "nt51920-standard-merge-gen-flash", "nt51920-standard-merge", "2acde361b0537210c4707f2a77a112d659ac885254ef863df2a2d75baa12ff53", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51923", "nt51923-standard-merge-gen-flash", "nt51923-standard-merge", "6c1d0336b4c2e4df61a47258937b75c598e06daa189f50d1b5457381434df7ec", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51926", "nt51926-standard-merge-gen-flash", "nt51923-standard-merge", "6c1d0336b4c2e4df61a47258937b75c598e06daa189f50d1b5457381434df7ec", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51928", "nt51928-standard-merge-gen-flash", "nt51928-standard-merge", "c55c07f8a84389804d96ca6a2caa57b3ce87840e94256f76f4710dde68997010", "dp-input,ld-input,tp-input", "DpFirmware,Auxiliary,TpFirmware")]
    [InlineData("NT51929", "nt51929-standard-merge-gen-flash", "nt51929-standard-merge", "01b84018c975ee4c7c52b36d594e2919a346294b713e060c496e597237ae3de3", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51930", "nt51930-standard-merge-flashmap", "nt51930-standard-merge", "f1c9d60f024ad4aae17c5e16f285d88acbd38977f048daf264184c2f6d75855b", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51932", "nt51932-standard-merge-gen-flash", "nt51929-standard-merge", "01b84018c975ee4c7c52b36d594e2919a346294b713e060c496e597237ae3de3", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    public void RegisteredStandardMergeUsesDeployedTrustedV2Artifact(
        string icId,
        string profileId,
        string bundleDirectory,
        string bundleContentHash,
        string expectedInputAddressSpaceIds,
        string expectedArtifactClasses)
    {
        ArgumentNullException.ThrowIfNull(expectedInputAddressSpaceIds);
        ArgumentNullException.ThrowIfNull(expectedArtifactClasses);
        AssertDeployedBundleMatchesRepository(bundleDirectory);

        bool compiled = WorkbenchCompositionService.TryCompileStandardMerge(
            icId,
            dpInputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, artifact.Eligibility);
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(artifact.Authority);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(artifact.V2Details);
        Assert.Equal(bundleContentHash, details.Provenance.Bundle.ContentHash);
        Assert.Equal(profileId, artifact.ProfileId);
        Assert.Equal(icId, artifact.IcId);
        Assert.Equal(
            expectedInputAddressSpaceIds.Split(','),
            artifact.Plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal));
        Assert.Equal(
            expectedArtifactClasses.Split(','),
            details.InputContract.Slots
                .OrderBy(static slot => slot.SlotId, StringComparer.Ordinal)
                .Select(static slot => slot.ArtifactClass.ToString()));
    }

    /// <summary>Verifies the second bundle reaches the shared engine with original input trace names.</summary>
    [Fact]
    public async Task Nt51929WorkbenchPreviewUsesTrustedV2InputBindings()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x40000];
        dp[0] = 0x11;
        tp[0x7000] = 0x22;
        string dpPath = workspace.Write("nt51929-dp.bin", dp);
        string tpPath = workspace.Write("nt51929-tp.bin", tp);

        WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51929",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dp-input"] = dpPath,
                ["tp-input"] = tpPath,
            },
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(0x40000, result.OutputSize);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal("nt51929-standard-merge-gen-flash", report.RootElement.GetProperty("ProfileId").GetString());
        Assert.Equal(
            ["nt51929-dp.bin", "nt51929-tp.bin"],
            report.RootElement.GetProperty("Inputs")
                .EnumerateArray()
                .Select(static input => input.GetProperty("OriginalFileName").GetString())
                .Order(StringComparer.Ordinal));
    }

    private static void AssertDeployedBundleMatchesRepository(string bundleDirectory)
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string sourceRoot = Path.Combine(repositoryRoot, "profiles", "built-in", bundleDirectory);
        string deployedRoot = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", bundleDirectory);
        string[] sourcePaths = [.. Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .Order(StringComparer.Ordinal)];
        string[] deployedPaths = [.. Directory.GetFiles(deployedRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(deployedRoot, path))
            .Order(StringComparer.Ordinal)];

        Assert.Equal(sourcePaths, deployedPaths);
        foreach (string relativePath in sourcePaths)
        {
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(sourceRoot, relativePath)),
                File.ReadAllBytes(Path.Combine(deployedRoot, relativePath)));
        }
    }
}
