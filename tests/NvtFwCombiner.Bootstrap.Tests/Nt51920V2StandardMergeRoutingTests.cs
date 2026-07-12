using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-routing evidence for the NT51920 canonical V2 Standard Merge bundle.</summary>
public sealed class Nt51920V2StandardMergeRoutingTests
{
    private const string BundleContentHash = "2acde361b0537210c4707f2a77a112d659ac885254ef863df2a2d75baa12ff53";

    /// <summary>Verifies deployed bundle bytes compile the selected NT51920 path as a V2 runtime artifact without legacy fallback.</summary>
    [Fact]
    public void Nt51920StandardMergeUsesDeployedTrustedV2Artifact()
    {
        AssertDeployedBundleMatchesRepository();

        bool compiled = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT51920",
            dpInputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, artifact.Eligibility);
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(artifact.Authority);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(artifact.V2Details);
        Assert.Equal(BundleContentHash, details.Provenance.Bundle.ContentHash);
        Assert.Equal("nt51920-standard-merge-gen-flash", artifact.ProfileId);
        Assert.Equal(["dp-input", "tp-input"], artifact.Plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal));
        Assert.Equal(
            [CompiledInputArtifactClass.DpFirmware, CompiledInputArtifactClass.TpFirmware],
            details.InputContract.Slots
                .OrderBy(static slot => slot.SlotId, StringComparer.Ordinal)
                .Select(static slot => slot.ArtifactClass));
    }

    /// <summary>Verifies the Workbench path creates V2 provenance bindings and reaches the shared engine.</summary>
    [Fact]
    public async Task Nt51920WorkbenchPreviewUsesTrustedV2InputBindings()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x30000];
        dp[0x3E000] = 0x11;
        tp[0] = 0x22;
        string dpPath = workspace.Write("nt51920-dp.bin", dp);
        string tpPath = workspace.Write("nt51920-tp.bin", tp);

        WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51920",
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
        Assert.Equal("nt51920-standard-merge-gen-flash", report.RootElement.GetProperty("ProfileId").GetString());
        Assert.Equal(
            ["nt51920-dp.bin", "nt51920-tp.bin"],
            report.RootElement.GetProperty("Inputs")
                .EnumerateArray()
                .Select(static input => input.GetProperty("OriginalFileName").GetString())
                .Order(StringComparer.Ordinal));
    }

    private static void AssertDeployedBundleMatchesRepository()
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string sourceRoot = Path.Combine(repositoryRoot, "profiles", "built-in", "nt51920-standard-merge");
        string deployedRoot = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", "nt51920-standard-merge");
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
