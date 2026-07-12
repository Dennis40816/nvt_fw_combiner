using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Golden migration evidence for the NT51919/NT51929/NT51932 canonical V2 Standard Merge family.</summary>
public sealed class Nt51929FamilyV2StandardMergeGoldenTests
{
    private const string BundleContentHash = "01b84018c975ee4c7c52b36d594e2919a346294b713e060c496e597237ae3de3";

    /// <summary>Verifies every family member retains legacy plan geometry and the owner-approved reference bytes.</summary>
    [Theory]
    [InlineData("NT51919", "nt51919-standard-merge-gen-flash-alias", "51929", "nt51919-standard-merge-gen-flash-alias.bin", true)]
    [InlineData("NT51929", "nt51929-standard-merge-gen-flash", "51929", "nt51929-standard-merge-gen-flash.bin", false)]
    [InlineData("NT51932", "nt51932-standard-merge-gen-flash", "51932", "nt51932-standard-merge-gen-flash.bin", false)]
    public async Task TrustedV2BundleMatchesLegacyPlanAndOwnerApprovedGoldenBytes(
        string icId,
        string profileId,
        string referenceIc,
        string expectedOutputFileName,
        bool expectsRegionSetAlias)
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string bundleRoot = Path.Combine(repositoryRoot, "profiles", "built-in", "nt51929-standard-merge");
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadCatalog(bundleRoot, BundleContentHash),
            profileId,
            "0.5.0",
            icId);

        Assert.Equal(profileId, v2.ProfileId);
        Assert.Equal(icId, v2.IcId);
        Assert.Equal(expectedOutputFileName, v2.DefaultOutputFileName);
        V2StandardMergeGoldenTestSupport.AssertPlanGeometryAndOperationParity(
            V2StandardMergeGoldenTestSupport.CompileLegacy(icId).Plan,
            v2.Plan);
        AssertNormalDpExtractionContract(v2.Plan);
        AssertRegionSetProvenance(v2, expectsRegionSetAlias);

        System.Text.Json.JsonElement goldenCase = V2StandardMergeGoldenTestSupport.ReadGoldenCase(referenceIc);
        Dictionary<string, byte[]> inputs = V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs"));
        byte[] expectedOutput = V2StandardMergeGoldenTestSupport.ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        V2StandardMergeGoldenTestSupport.AssertSuccessfulGoldenOutput(result, v2, expectedOutput);
    }

    private static void AssertRegionSetProvenance(CompiledComposition composition, bool expectsAlias)
    {
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        FirmwareFactProvenance provenance = Assert.Single(details.Provenance.ResolvedMap.FactProvenance);
        if (expectsAlias)
        {
            Assert.Equal("nt51919-standard-merge-flash", provenance.EffectiveKey.FactId);
            Assert.Equal("NT51929", provenance.DirectSourceKey.MemberId);
            Assert.Equal("nt51929-standard-merge-256k", provenance.DirectSourceKey.MapId);
            Assert.Equal("nt51929-nt51932-standard-merge-flash", provenance.DirectSourceKey.FactId);
            Assert.Equal("nt51919-standard-merge-region-set-alias", Assert.Single(provenance.AliasChain).AliasId);
            return;
        }

        Assert.Empty(provenance.AliasChain);
        Assert.Equal(provenance.EffectiveKey, provenance.DirectSourceKey);
    }

    private static void AssertNormalDpExtractionContract(CompositionPlan plan)
    {
        AddressSpace dpInput = plan.AddressSpaces.Single(space => space.AddressSpaceId == "dp-input");
        Assert.Equal(0x6000, dpInput.Length);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, dpInput.InputOversizePolicy);
        Assert.Empty(dpInput.AllowedInputLengths);
        Assert.Equal([0x40000], dpInput.ExpectedInputLengths);
        Assert.Equal("DP_SIZE_WARNING", dpInput.UnexpectedInputLengthIssueCode);
    }
}
