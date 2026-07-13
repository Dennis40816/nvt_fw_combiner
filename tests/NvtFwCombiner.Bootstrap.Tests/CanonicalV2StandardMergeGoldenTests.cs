using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Golden migration evidence for canonical V2 Standard Merge families.</summary>
public sealed class CanonicalV2StandardMergeGoldenTests
{
    /// <summary>Verifies every family member retains legacy plan geometry and the owner-approved reference bytes.</summary>
    [Theory]
    [InlineData("nt51929-standard-merge", "3c8ace0d7b0360573847d4b2c5f052313af9d2ff680cebe6288cf1611edb8f09", "NT51919", "nt51919-standard-merge-gen-flash-alias", "51929", "nt51919-standard-merge-gen-flash-alias.bin", 0x6000, 0x40000, true)]
    [InlineData("nt51929-standard-merge", "3c8ace0d7b0360573847d4b2c5f052313af9d2ff680cebe6288cf1611edb8f09", "NT51929", "nt51929-standard-merge-gen-flash", "51929", "nt51929-standard-merge-gen-flash.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51929-standard-merge", "3c8ace0d7b0360573847d4b2c5f052313af9d2ff680cebe6288cf1611edb8f09", "NT51932", "nt51932-standard-merge-gen-flash", "51932", "nt51932-standard-merge-gen-flash.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51923-standard-merge", "6bac75eb386ff08c3fa6970e54b3c1dca35722ddaeaf52b67068a127c4e85a96", "NT51923", "nt51923-standard-merge-gen-flash", "51923", "nt51923-standard-merge-gen-flash.bin", 0x40000, 0x40000, false)]
    [InlineData("nt51923-standard-merge", "6bac75eb386ff08c3fa6970e54b3c1dca35722ddaeaf52b67068a127c4e85a96", "NT51926", "nt51926-standard-merge-gen-flash", "51926", "nt51926-standard-merge-gen-flash.bin", 0x40000, 0x40000, false)]
    [InlineData("nt51930-standard-merge", "b9ca3d66d8674d080b4e0c8563110dfd305b3df18746f5164e7ed45514e0714e", "NT51930", "nt51930-standard-merge-flashmap", "51930", "nt51930-standard-merge-flashmap.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51931-standard-merge", "a7b3534afce6d2fe107363e41554668a71832f203168c81fa09e9f98a1a5815f", "NT51931", "nt51931-standard-merge-gen-flash", "51931", "nt51931-standard-merge-gen-flash.bin", 0x40000, 0x80000, false)]
    [InlineData("nt51927-standard-merge", "751f44c7dd790a826e9ab17747b933542c691125bdee8b975c9c764e4f2ef4b1", "NT51917", "nt51917-standard-merge-gen-flash-alias", "51927", "nt51917-standard-merge-gen-flash-alias.bin", 0x40000, 0x200000, true)]
    [InlineData("nt51927-standard-merge", "751f44c7dd790a826e9ab17747b933542c691125bdee8b975c9c764e4f2ef4b1", "NT51927", "nt51927-standard-merge-gen-flash", "51927", "nt51927-standard-merge-gen-flash.bin", 0x40000, 0x200000, false)]
    [InlineData("nt51928-standard-merge", "27de29151abd1305a8ebf6ba25118acbf59392efd362d362699310a5564ad5af", "NT51928", "nt51928-standard-merge-gen-flash", "51928", "nt51928-standard-merge-gen-flash.bin", 0x40000, 0x80000, false)]
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
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(bundleDirectory, bundleContentHash),
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
            (string EffectiveMapId, string EffectiveFactId, string SourceMemberId, string SourceMapId, string SourceFactId, string AliasId) = composition.IcId switch
            {
                "NT51917" => (
                    "nt51917-standard-merge-256k",
                    "nt51917-standard-merge-flash",
                    "NT51927",
                    "nt51927-standard-merge-256k",
                    "nt51927-standard-merge-flash",
                    "nt51917-standard-merge-region-set-alias"),
                "NT51919" => (
                    "nt51919-standard-merge-256k",
                    "nt51919-standard-merge-flash",
                    "NT51929",
                    "nt51929-standard-merge-256k",
                    "nt51929-nt51932-standard-merge-flash",
                    "nt51919-standard-merge-region-set-alias"),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected region-set alias for {composition.IcId}."),
            };

            Assert.Equal(composition.IcId, provenance.EffectiveKey.MemberId);
            Assert.Equal(EffectiveMapId, provenance.EffectiveKey.MapId);
            Assert.Equal(EffectiveFactId, provenance.EffectiveKey.FactId);
            Assert.Equal(SourceMemberId, provenance.DirectSourceKey.MemberId);
            Assert.Equal(SourceMapId, provenance.DirectSourceKey.MapId);
            Assert.Equal(SourceFactId, provenance.DirectSourceKey.FactId);
            Assert.Equal(AliasId, Assert.Single(provenance.AliasChain).AliasId);
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
