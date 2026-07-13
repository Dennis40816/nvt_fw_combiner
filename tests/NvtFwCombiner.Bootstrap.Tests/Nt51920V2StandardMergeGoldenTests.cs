using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Golden migration evidence for the first canonical V2 Standard Merge bundle.</summary>
public sealed class Nt51920V2StandardMergeGoldenTests
{
    private const string BundleContentHash = "3bb76d56656642af553ff012a619ca8fc38fb7cdabf8ac674e5433998357f9f2";

    /// <summary>Verifies the trusted NT51920 V2 bundle preserves the legacy plan and owner-approved output bytes.</summary>
    [Fact]
    public async Task TrustedV2BundleMatchesLegacyPlanAndOwnerApprovedGoldenBytes()
    {
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog("nt51920-standard-merge", BundleContentHash),
            "nt51920-standard-merge-gen-flash",
            "0.5.0",
            "NT51920");
        Assert.Equal("nt51920-standard-merge-gen-flash.bin", v2.DefaultOutputFileName);
        V2StandardMergeGoldenTestSupport.AssertPlanGeometryAndOperationParity(
            V2StandardMergeGoldenTestSupport.CompileLegacy("NT51920").Plan,
            v2.Plan);

        System.Text.Json.JsonElement goldenCase = V2StandardMergeGoldenTestSupport.ReadGoldenCase("51920");
        Dictionary<string, byte[]> inputs = V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs"));
        byte[] expectedOutput = V2StandardMergeGoldenTestSupport.ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        V2StandardMergeGoldenTestSupport.AssertSuccessfulGoldenOutput(result, v2, expectedOutput);
        Assert.Equal(
            ["dp.bin", "tp.bin"],
            result.Report.Inputs.Select(static input => input.OriginalFileName).Order(StringComparer.Ordinal));
    }
}
