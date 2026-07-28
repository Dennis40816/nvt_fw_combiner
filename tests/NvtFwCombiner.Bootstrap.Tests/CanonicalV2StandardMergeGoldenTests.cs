using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Golden migration evidence for canonical V2 Standard Merge families.</summary>
public sealed class CanonicalV2StandardMergeGoldenTests
{
    /// <summary>Verifies every family member declares the V2 copy plan and produces owner-approved reference bytes.</summary>
    [Theory]
    [InlineData("nt51929-standard-merge", "14a3b2808a5377af39b683fe44f60f152e9c7f4a15c18e5c9e264ad6ea2b0827", "NT51919", "nt51919-standard-merge-gen-flash-alias", "51929", "nt51919-standard-merge-gen-flash-alias.bin", 0x6000, 0x40000, true, true)]
    [InlineData("nt51929-standard-merge", "14a3b2808a5377af39b683fe44f60f152e9c7f4a15c18e5c9e264ad6ea2b0827", "NT51929", "nt51929-standard-merge-gen-flash", "51929", "nt51929-standard-merge-gen-flash.bin", 0x6000, 0x40000, false, true)]
    [InlineData("nt51929-standard-merge", "14a3b2808a5377af39b683fe44f60f152e9c7f4a15c18e5c9e264ad6ea2b0827", "NT51932", "nt51932-standard-merge-gen-flash", "51932", "nt51932-standard-merge-gen-flash.bin", 0x6000, 0x40000, false, true)]
    [InlineData("nt51923-standard-merge", "cf1113069ac44ddd4a5fcf074555f3ac5ff0a1b0f43b9302218311fbf530739d", "NT51923", "nt51923-standard-merge-gen-flash", "51923", "nt51923-standard-merge-gen-flash.bin", 0x40000, 0x40000, false, true)]
    [InlineData("nt51923-standard-merge", "cf1113069ac44ddd4a5fcf074555f3ac5ff0a1b0f43b9302218311fbf530739d", "NT51926", "nt51926-standard-merge-gen-flash", "51926", "nt51926-standard-merge-gen-flash.bin", 0x40000, 0x40000, false, true)]
    [InlineData("nt51927-standard-merge", "37132b1658e69c0b59db3f7261a123b654dfc31c1cfad2c2a856ed31c79a0a19", "NT51917", "nt51917-standard-merge-gen-flash-alias", "51927", "nt51917-standard-merge-gen-flash-alias.bin", 0x40000, 0x200000, false, true)]
    [InlineData("nt51927-standard-merge", "37132b1658e69c0b59db3f7261a123b654dfc31c1cfad2c2a856ed31c79a0a19", "NT51927", "nt51927-standard-merge-gen-flash", "51927", "nt51927-standard-merge-gen-flash.bin", 0x40000, 0x200000, false, true)]
    [InlineData("nt51928-standard-merge", "034e3b8e4bbad7d487578ef517093c008b1121ab0cdceaa8bb1879a39cc72bfe", "NT51928", "nt51928-standard-merge-gen-flash", "51928", "nt51928-standard-merge-gen-flash.bin", 0x40000, 0x80000, false, true)]
    public async Task TrustedV2BundleMatchesDeclaredPlanAndOwnerApprovedGoldenBytes(
        string bundleDirectory,
        string bundleContentHash,
        string icId,
        string profileId,
        string referenceIc,
        string expectedOutputFileName,
        int expectedDpSourceLength,
        int expectedDpInputLength,
        bool expectsRegionSetAlias,
        bool expectsDpExtraction)
    {
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(bundleDirectory, bundleContentHash),
            profileId,
            icId switch
            {
                "NT51917" or "NT51919" or "NT51929" or "NT51932" => "0.6.0",
                "NT51927" or "NT51928" => "0.7.0",
                _ => "0.5.0",
            },
            icId);

        Assert.Equal(profileId, v2.ProfileId);
        Assert.Equal(icId, v2.IcId);
        Assert.Equal(expectedOutputFileName, v2.DefaultOutputFileName);
        AssertNormalDpInputContract(v2.Plan, expectedDpSourceLength, expectedDpInputLength, expectsDpExtraction);
        if (StringComparer.Ordinal.Equals(icId, "NT51928"))
        {
            AssertNt51928LdcInputContract(v2);
        }
        AssertRegionSetProvenance(v2, expectsRegionSetAlias);

        System.Text.Json.JsonElement goldenCase = V2StandardMergeGoldenTestSupport.ReadGoldenCase(referenceIc);
        Dictionary<string, byte[]> inputs = V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs"));
        byte[] expectedOutput = V2StandardMergeGoldenTestSupport.ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        AssertDeclaredStandardMergePlan(v2.Plan, expectedOutput.LongLength, icId == "NT51928");
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        V2StandardMergeGoldenTestSupport.AssertSuccessfulGoldenOutput(result, v2, expectedOutput);
    }

    private static void AssertRegionSetProvenance(CompiledComposition composition, bool expectsAlias)
    {
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        FirmwareFactProvenance[] regionSetProvenance =
        [
            .. details.Provenance.ResolvedMap.FactProvenance.Where(static candidate =>
                candidate.EffectiveKey.FactKind == FirmwareFactKind.RegionSet),
        ];
        Assert.NotEmpty(regionSetProvenance);
        if (expectsAlias)
        {
            FirmwareFactProvenance provenance = Assert.Single(
                regionSetProvenance,
                static candidate => candidate.AliasChain.Count != 0);
            Assert.All(
                regionSetProvenance.Where(static candidate =>
                    candidate.AliasChain.Count == 0),
                AssertDirectProvenance);
            (string EffectiveMapId, string EffectiveFactId, string SourceMemberId, string SourceMapId, string SourceFactId, string AliasId) = composition.IcId switch
            {
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

        Assert.All(regionSetProvenance, AssertDirectProvenance);
    }

    private static void AssertDirectProvenance(FirmwareFactProvenance provenance)
    {
        Assert.Empty(provenance.AliasChain);
        Assert.Equal(provenance.EffectiveKey, provenance.DirectSourceKey);
    }

    private static void AssertNormalDpInputContract(
        CompositionPlan plan,
        int expectedSourceLength,
        int expectedInputLength,
        bool expectsExtraction)
    {
        AddressSpace dpInput = plan.AddressSpaces.Single(space => space.AddressSpaceId == "dp-input");
        Assert.Equal(expectedSourceLength, dpInput.Length);
        if (expectsExtraction)
        {
            Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, dpInput.InputOversizePolicy);
            Assert.Empty(dpInput.AllowedInputLengths);
            Assert.Equal([expectedInputLength], dpInput.ExpectedInputLengths);
            Assert.Equal("DP_SIZE_WARNING", dpInput.UnexpectedInputLengthIssueCode);
            return;
        }

        Assert.Equal(InputOversizePolicy.Reject, dpInput.InputOversizePolicy);
        Assert.Equal([expectedInputLength], dpInput.AllowedInputLengths);
        Assert.Empty(dpInput.ExpectedInputLengths);
        Assert.Null(dpInput.UnexpectedInputLengthIssueCode);
    }

    private static void AssertDeclaredStandardMergePlan(
        CompositionPlan plan,
        long expectedCapacity,
        bool expectsLdc)
    {
        Assert.Equal(CompositionAddressSpaceIds.OutputImage, plan.OutputSpaceId);
        Assert.Equal(ImageInitializationKind.Blank, plan.OutputInitialization.Kind);
        Assert.Equal(expectedCapacity, plan.OutputInitialization.Capacity);
        Assert.Equal(0, plan.OutputInitialization.FillByte);

        string[] expectedOperationIds = expectsLdc
            ? ["copy-tp", "copy-dp", "copy-ld"]
            : ["copy-tp", "copy-dp"];
        int[] expectedSequences = expectsLdc ? [100, 200, 300] : [100, 200];
        Assert.Equal(expectedOperationIds, plan.OrderedOperations.Select(static operation => operation.OperationId));
        Assert.Equal(expectedSequences, plan.OrderedOperations.Select(static operation => operation.Sequence));
        Assert.All(plan.OrderedOperations, operation =>
        {
            Assert.Equal(CompositionOperationKind.CopyRange, operation.Kind);
            Assert.Equal(CompositionAddressSpaceIds.OutputImage, operation.TargetSpaceId);
            Assert.Equal(operation.SourceRange, operation.TargetRange);
            Assert.Equal(OverlapPolicy.Reject, operation.OverlapPolicy);
        });
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
