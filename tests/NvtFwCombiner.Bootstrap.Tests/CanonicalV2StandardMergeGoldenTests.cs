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
    [InlineData("nt51929-standard-merge", "456697118dbf707a060228a5f124341c9c9f32957153ff7dfd1a5f752887236a", "NT51919", "nt51919-standard-merge-gen-flash-alias", "51929", "nt51919-standard-merge-gen-flash-alias.bin", 0x6000, 0x40000, true)]
    [InlineData("nt51929-standard-merge", "456697118dbf707a060228a5f124341c9c9f32957153ff7dfd1a5f752887236a", "NT51929", "nt51929-standard-merge-gen-flash", "51929", "nt51929-standard-merge-gen-flash.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51929-standard-merge", "456697118dbf707a060228a5f124341c9c9f32957153ff7dfd1a5f752887236a", "NT51932", "nt51932-standard-merge-gen-flash", "51932", "nt51932-standard-merge-gen-flash.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51923-standard-merge", "2fa763cce4d9bbaa623821905683cb7ebc832174d916fb338aa8a3cde31b2f59", "NT51923", "nt51923-standard-merge-gen-flash", "51923", "nt51923-standard-merge-gen-flash.bin", 0x40000, 0x40000, false)]
    [InlineData("nt51923-standard-merge", "2fa763cce4d9bbaa623821905683cb7ebc832174d916fb338aa8a3cde31b2f59", "NT51926", "nt51926-standard-merge-gen-flash", "51926", "nt51926-standard-merge-gen-flash.bin", 0x40000, 0x40000, false)]
    [InlineData("nt51930-standard-merge", "046409a16d3b7bdfd942407e8702f08ddb40f20fd94ff297e449f141d4b13cbb", "NT51930", "nt51930-standard-merge-flashmap", "51930", "nt51930-standard-merge-flashmap.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51931-standard-merge", "ff3ac6d142ffdbef52c9b088b692e25fe36b38f9cbcf2b43c06894b00ee97d4f", "NT51931", "nt51931-standard-merge-gen-flash", "51931", "nt51931-standard-merge-gen-flash.bin", 0x40000, 0x80000, false)]
    [InlineData("nt51928-standard-merge", "4c0574d52d78bcdca8461fb0660d58f781221a27bfa93e541edf076a5432574d", "NT51928", "nt51928-standard-merge-gen-flash", "51928", "nt51928-standard-merge-gen-flash.bin", 0x40000, 0x80000, false)]
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
