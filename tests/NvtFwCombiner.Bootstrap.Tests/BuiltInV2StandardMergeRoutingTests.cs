using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-routing evidence for hash-anchored built-in V2 Standard Merge bundles.</summary>
public sealed class BuiltInV2StandardMergeRoutingTests
{
    /// <summary>Verifies every registered IC selects one deployed V2 artifact without legacy fallback.</summary>
    [Theory]
    [InlineData("NT51917", "nt51917-standard-merge-gen-flash-alias", "nt51927-standard-merge", "b1ee7a6ba5aa4d2ddcea2cb94a4aef23839e6e4353687df0115049ec15c019ef", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51919", "nt51919-standard-merge-gen-flash-alias", "nt51929-standard-merge", "456697118dbf707a060228a5f124341c9c9f32957153ff7dfd1a5f752887236a", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51920", "nt51920-standard-merge-gen-flash", "nt51920-standard-merge", "596fa2f4b8a8043d1892b07f9c4b5bb1cd749b7c7fe20ed194a176c5293c399a", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51923", "nt51923-standard-merge-gen-flash", "nt51923-standard-merge", "2fa763cce4d9bbaa623821905683cb7ebc832174d916fb338aa8a3cde31b2f59", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51926", "nt51926-standard-merge-gen-flash", "nt51923-standard-merge", "2fa763cce4d9bbaa623821905683cb7ebc832174d916fb338aa8a3cde31b2f59", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51927", "nt51927-standard-merge-gen-flash", "nt51927-standard-merge", "b1ee7a6ba5aa4d2ddcea2cb94a4aef23839e6e4353687df0115049ec15c019ef", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51928", "nt51928-standard-merge-gen-flash", "nt51928-standard-merge", "4c0574d52d78bcdca8461fb0660d58f781221a27bfa93e541edf076a5432574d", "dp-input,ld-input,tp-input", "DpFirmware,Auxiliary,TpFirmware")]
    [InlineData("NT51929", "nt51929-standard-merge-gen-flash", "nt51929-standard-merge", "456697118dbf707a060228a5f124341c9c9f32957153ff7dfd1a5f752887236a", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51930", "nt51930-standard-merge-flashmap", "nt51930-standard-merge", "046409a16d3b7bdfd942407e8702f08ddb40f20fd94ff297e449f141d4b13cbb", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51931", "nt51931-standard-merge-gen-flash", "nt51931-standard-merge", "ff3ac6d142ffdbef52c9b088b692e25fe36b38f9cbcf2b43c06894b00ee97d4f", "dp-input,tp-input", "DpFirmware,TpFirmware")]
    [InlineData("NT51932", "nt51932-standard-merge-gen-flash", "nt51929-standard-merge", "456697118dbf707a060228a5f124341c9c9f32957153ff7dfd1a5f752887236a", "dp-input,tp-input", "DpFirmware,TpFirmware")]
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
