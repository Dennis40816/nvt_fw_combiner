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
    [InlineData("nt51929-standard-merge", "eb30675d297323914fb0e587165ecd124ee2f89a10fa9a7e55a19309b8784de8", "NT51919", "nt51919-standard-merge-gen-flash-alias", "51929", "nt51919-standard-merge-gen-flash-alias.bin", 0x6000, 0x40000, true)]
    [InlineData("nt51929-standard-merge", "eb30675d297323914fb0e587165ecd124ee2f89a10fa9a7e55a19309b8784de8", "NT51929", "nt51929-standard-merge-gen-flash", "51929", "nt51929-standard-merge-gen-flash.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51929-standard-merge", "eb30675d297323914fb0e587165ecd124ee2f89a10fa9a7e55a19309b8784de8", "NT51932", "nt51932-standard-merge-gen-flash", "51932", "nt51932-standard-merge-gen-flash.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51923-standard-merge", "56bc8a3d68b0015461bc903fa1a17fdb172715b61e1fa879506ddcc3a71c9038", "NT51923", "nt51923-standard-merge-gen-flash", "51923", "nt51923-standard-merge-gen-flash.bin", 0x40000, 0x40000, false)]
    [InlineData("nt51923-standard-merge", "56bc8a3d68b0015461bc903fa1a17fdb172715b61e1fa879506ddcc3a71c9038", "NT51926", "nt51926-standard-merge-gen-flash", "51926", "nt51926-standard-merge-gen-flash.bin", 0x40000, 0x40000, false)]
    [InlineData("nt51930-standard-merge", "3803b473fd0f133d33c66299199f6202a72e1c83eb8c9e6e910f191d1fadd00d", "NT51930", "nt51930-standard-merge-flashmap", "51930", "nt51930-standard-merge-flashmap.bin", 0x6000, 0x40000, false)]
    [InlineData("nt51931-standard-merge", "94c36258a6d981a5fa7133811d38bae175b1ff82b67a2df3abcaf090e03ec0d4", "NT51931", "nt51931-standard-merge-gen-flash", "51931", "nt51931-standard-merge-gen-flash.bin", 0x40000, 0x80000, false)]
    [InlineData("nt51927-standard-merge", "67a314a3763b81e348960bafb5e743e5fc1df553d8590544a6d8d52706038afe", "NT51917", "nt51917-standard-merge-gen-flash-alias", "51927", "nt51917-standard-merge-gen-flash-alias.bin", 0x40000, 0x200000, true)]
    [InlineData("nt51927-standard-merge", "67a314a3763b81e348960bafb5e743e5fc1df553d8590544a6d8d52706038afe", "NT51927", "nt51927-standard-merge-gen-flash", "51927", "nt51927-standard-merge-gen-flash.bin", 0x40000, 0x200000, false)]
    [InlineData("nt51928-standard-merge", "961224d53b236e851039d65765654674ff65ba75a7cedc7ee9e5d6c9a6165bb5", "NT51928", "nt51928-standard-merge-gen-flash", "51928", "nt51928-standard-merge-gen-flash.bin", 0x40000, 0x80000, false)]
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
