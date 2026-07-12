using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Golden migration evidence for canonical V2 Standard Merge families.</summary>
public sealed class CanonicalV2StandardMergeGoldenTests
{
    /// <summary>Verifies every family member retains legacy plan geometry and the owner-approved reference bytes.</summary>
    [Theory]
    [InlineData("nt51929-standard-merge", "01b84018c975ee4c7c52b36d594e2919a346294b713e060c496e597237ae3de3", "NT51919", "nt51919-standard-merge-gen-flash-alias", "51929", "nt51919-standard-merge-gen-flash-alias.bin", 0x6000, 0x40000, true)]
    [InlineData("nt51929-standard-merge", "01b84018c975ee4c7c52b36d594e2919a346294b713e060c496e597237ae3de3", "NT51929", "nt51929-standard-merge-gen-flash", "51929", "nt51929-standard-merge-gen-flash.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51929-standard-merge", "01b84018c975ee4c7c52b36d594e2919a346294b713e060c496e597237ae3de3", "NT51932", "nt51932-standard-merge-gen-flash", "51932", "nt51932-standard-merge-gen-flash.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51923-standard-merge", "6c1d0336b4c2e4df61a47258937b75c598e06daa189f50d1b5457381434df7ec", "NT51923", "nt51923-standard-merge-gen-flash", "51923", "nt51923-standard-merge-gen-flash.bin", 0x40000, 0x40000, false)]
    [InlineData("nt51923-standard-merge", "6c1d0336b4c2e4df61a47258937b75c598e06daa189f50d1b5457381434df7ec", "NT51926", "nt51926-standard-merge-gen-flash", "51926", "nt51926-standard-merge-gen-flash.bin", 0x40000, 0x40000, false)]
    [InlineData("nt51930-standard-merge", "f1c9d60f024ad4aae17c5e16f285d88acbd38977f048daf264184c2f6d75855b", "NT51930", "nt51930-standard-merge-flashmap", "51930", "nt51930-standard-merge-flashmap.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51928-standard-merge", "c55c07f8a84389804d96ca6a2caa57b3ce87840e94256f76f4710dde68997010", "NT51928", "nt51928-standard-merge-gen-flash", "51928", "nt51928-standard-merge-gen-flash.bin", 0x40000, 0x80000, false)]
    public async Task TrustedV2BundleMatchesLegacyPlanAndOwnerApprovedGoldenBytes(
        string bundleDirectory,
        string bundleContentHash,
        string icId,
        string profileId,
        string referenceIc,
        string expectedOutputFileName,
        int expectedDpSourceLength,
        int expectedDpInputLength,
        bool expectsRegionSetAlias)
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string bundleRoot = Path.Combine(repositoryRoot, "profiles", "built-in", bundleDirectory);
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadCatalog(bundleRoot, bundleContentHash),
            profileId,
            "0.5.0",
            icId);

        Assert.Equal(profileId, v2.ProfileId);
        Assert.Equal(icId, v2.IcId);
        Assert.Equal(expectedOutputFileName, v2.DefaultOutputFileName);
        V2StandardMergeGoldenTestSupport.AssertPlanGeometryAndOperationParity(
            V2StandardMergeGoldenTestSupport.CompileLegacy(icId).Plan,
            v2.Plan);
        AssertNormalDpExtractionContract(v2.Plan, expectedDpSourceLength, expectedDpInputLength);
        if (StringComparer.Ordinal.Equals(icId, "NT51928"))
        {
            AssertNt51928LdcInputContract(v2);
        }
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

    private static void AssertNormalDpExtractionContract(
        CompositionPlan plan,
        int expectedSourceLength,
        int expectedInputLength)
    {
        AddressSpace dpInput = plan.AddressSpaces.Single(space => space.AddressSpaceId == "dp-input");
        Assert.Equal(expectedSourceLength, dpInput.Length);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, dpInput.InputOversizePolicy);
        Assert.Empty(dpInput.AllowedInputLengths);
        Assert.Equal([expectedInputLength], dpInput.ExpectedInputLengths);
        Assert.Equal("DP_SIZE_WARNING", dpInput.UnexpectedInputLengthIssueCode);
    }

    private static void AssertNt51928LdcInputContract(CompiledComposition composition)
    {
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        CompiledInputSlotRequirement ldSlot = Assert.Single(details.InputContract.Slots,
            slot => slot.SlotId == "ld-input");
        Assert.Equal("ldc", ldSlot.Role);
        Assert.Equal(CompiledInputArtifactClass.Auxiliary, ldSlot.ArtifactClass);
        Assert.True(ldSlot.Required);
        Assert.Equal(CompiledInputSlotCardinality.ExactlyOne, ldSlot.Cardinality);
        Assert.Equal(0x80000, Assert.IsType<CompiledExactResolvedMapCapacityInputLengthRequirement>(
            ldSlot.LengthRequirement).Bytes);
        Assert.Equal(0x80000, Assert.Single(composition.Plan.AddressSpaces,
            space => space.AddressSpaceId == "ld-input").Length);
    }
}
