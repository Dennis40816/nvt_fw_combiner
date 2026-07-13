using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-routing evidence for hash-anchored built-in V2 Standard Merge bundles.</summary>
public sealed class BuiltInV2StandardMergeRoutingTests
{
    /// <summary>Verifies every registered IC selects one deployed V2 artifact without legacy fallback.</summary>
    [Theory]
    [InlineData("NT51917", "nt51917-standard-merge-gen-flash-alias", "nt51927-standard-merge", "67a314a3763b81e348960bafb5e743e5fc1df553d8590544a6d8d52706038afe", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51919", "nt51919-standard-merge-gen-flash-alias", "nt51929-standard-merge", "eb30675d297323914fb0e587165ecd124ee2f89a10fa9a7e55a19309b8784de8", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51920", "nt51920-standard-merge-gen-flash", "nt51920-standard-merge", "c58c9b68678bd314fa82c5563602001b6fa55d7176142c07067ef08f1b8d720a", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51923", "nt51923-standard-merge-gen-flash", "nt51923-standard-merge", "56bc8a3d68b0015461bc903fa1a17fdb172715b61e1fa879506ddcc3a71c9038", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51926", "nt51926-standard-merge-gen-flash", "nt51923-standard-merge", "56bc8a3d68b0015461bc903fa1a17fdb172715b61e1fa879506ddcc3a71c9038", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51927", "nt51927-standard-merge-gen-flash", "nt51927-standard-merge", "67a314a3763b81e348960bafb5e743e5fc1df553d8590544a6d8d52706038afe", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51928", "nt51928-standard-merge-gen-flash", "nt51928-standard-merge", "961224d53b236e851039d65765654674ff65ba75a7cedc7ee9e5d6c9a6165bb5", "dp-input,ld-input,tp-input", "DpFirmware,Auxiliary,TpFirmware")]
    [InlineData("NT51929", "nt51929-standard-merge-gen-flash", "nt51929-standard-merge", "eb30675d297323914fb0e587165ecd124ee2f89a10fa9a7e55a19309b8784de8", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51930", "nt51930-standard-merge-flashmap", "nt51930-standard-merge", "3803b473fd0f133d33c66299199f6202a72e1c83eb8c9e6e910f191d1fadd00d", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51931", "nt51931-standard-merge-gen-flash", "nt51931-standard-merge", "94c36258a6d981a5fa7133811d38bae175b1ff82b67a2df3abcaf090e03ec0d4", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51932", "nt51932-standard-merge-gen-flash", "nt51929-standard-merge", "eb30675d297323914fb0e587165ecd124ee2f89a10fa9a7e55a19309b8784de8", "dp-input,tp-input", "DpFirmware,TpFirmware")]
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

    /// <summary>Verifies DP Perspective routing selects the trusted map that exactly matches the supplied DP container length.</summary>
    [Theory]
    [InlineData("NT51950", "nt51950-standard-merge-dp-perspective", 0x40000)]
    [InlineData("NT51951", "nt51951-standard-merge-dp-perspective", 0x80000)]
    public void DpPerspectiveStandardMergeUsesCapacitySelectedTrustedV2Artifact(
        string icId,
        string profileId,
        long dpInputLength)
    {
        AssertDeployedBundleMatchesRepository("nt51950-nt51951-standard-merge");

        bool compiled = WorkbenchCompositionService.TryCompileStandardMerge(
            icId,
            dpInputLength,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, artifact.Eligibility);
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(artifact.Authority);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(artifact.V2Details);
        Assert.Equal("a51258be9024c8366821bee9610f7c7326bce9e9ea046747da7361c72a75c76b", details.Provenance.Bundle.ContentHash);
        Assert.Equal(profileId, artifact.ProfileId);
        Assert.Equal(icId, artifact.IcId);
        Assert.Equal(dpInputLength, artifact.Plan.OutputInitialization.Capacity);
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
