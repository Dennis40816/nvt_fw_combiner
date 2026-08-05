using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Golden migration evidence for canonical V2 Standard Merge families.</summary>
public sealed class CanonicalV2StandardMergeGoldenTests
{
    /// <summary>Verifies every family member declares the V2 copy plan and produces owner-approved reference bytes.</summary>
    [Theory]
    [InlineData("nt51929-standard-merge", "c67e8ee68cd06f4e1a169abab7c900dc457bbd03f29da770fb7feefb848be380", "NT51919", "nt51919-standard-merge-gen-flash-alias", "51929", "nt51919-standard-merge-gen-flash-alias.bin", 0x6000, true, false)]
    [InlineData("nt51929-standard-merge", "c67e8ee68cd06f4e1a169abab7c900dc457bbd03f29da770fb7feefb848be380", "NT51929", "nt51929-standard-merge-gen-flash", "51929", "nt51929-standard-merge-gen-flash.bin", 0x6000, false, false)]
    [InlineData("nt51929-standard-merge", "c67e8ee68cd06f4e1a169abab7c900dc457bbd03f29da770fb7feefb848be380", "NT51932", "nt51932-standard-merge-gen-flash", "51932", "nt51932-standard-merge-gen-flash.bin", 0x6000, false, false)]
    [InlineData("nt51923-standard-merge", "a0a7ad684887b4071dceb66b9ca28b11d97cd9108c8d518e6846773892cc02c2", "NT51923", "nt51923-standard-merge-gen-flash", "51923", "nt51923-standard-merge-gen-flash.bin", 0x40000, false, false)]
    [InlineData("nt51923-standard-merge", "a0a7ad684887b4071dceb66b9ca28b11d97cd9108c8d518e6846773892cc02c2", "NT51926", "nt51926-standard-merge-gen-flash", "51926", "nt51926-standard-merge-gen-flash.bin", 0x40000, false, false)]
    [InlineData("nt51927-standard-merge", "48511d6e386f295c75bb7bd05a69ce60a4d20f3954d750959e7e31a018c6c6d8", "NT51917", "nt51917-standard-merge-gen-flash-alias", "51927", "nt51917-standard-merge-gen-flash-alias.bin", 0x40000, false, false)]
    [InlineData("nt51927-standard-merge", "48511d6e386f295c75bb7bd05a69ce60a4d20f3954d750959e7e31a018c6c6d8", "NT51927", "nt51927-standard-merge-gen-flash", "51927", "nt51927-standard-merge-gen-flash.bin", 0x40000, false, false)]
    [InlineData("nt51928-standard-merge", "895ccc579907874af31e5a9f132e0ffb4c10e150f1ca8aad23a0f4f8bac317ca", "NT51928", "nt51928-standard-merge-gen-flash", "51927", "nt51928-standard-merge-gen-flash.bin", 0x40000, false, false)]
    [InlineData("nt51928-standard-merge", "895ccc579907874af31e5a9f132e0ffb4c10e150f1ca8aad23a0f4f8bac317ca", "NT51928", "nt51928-standard-merge-gen-flash", "51928", "nt51928-standard-merge-gen-flash.bin", 0x40000, false, true)]
    public async Task TrustedV2BundleMatchesDeclaredPlanAndOwnerApprovedGoldenBytes(
        string bundleDirectory,
        string bundleContentHash,
        string icId,
        string profileId,
        string referenceIc,
        string expectedOutputFileName,
        int expectedDpSourceLength,
        bool expectsRegionSetAlias,
        bool expectsLdc)
    {
        CompiledComposition v2 = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(bundleDirectory, bundleContentHash),
            profileId,
            icId switch
            {
                "NT51917" or "NT51919" or "NT51929" or "NT51932" => "0.6.0",
                "NT51927" => "0.7.0",
                "NT51928" => "0.8.0",
                _ => "0.5.0",
            },
            icId,
            requestedMapCapacity: icId == "NT51928" ? expectsLdc ? 0x80000 : 0x40000 : null,
            selectedInputSlotIds: expectsLdc
                ? [CompositionAddressSpaceIds.LdcInput]
                : null);

        Assert.Equal(profileId, v2.ProfileId);
        Assert.Equal(icId, v2.IcId);
        Assert.Equal(expectedOutputFileName, v2.DefaultOutputFileName);
        AssertNormalDpInputContract(v2, expectedDpSourceLength);
        if (expectsLdc)
        {
            AssertNt51928LdcInputContract(v2);
        }
        AssertRegionSetProvenance(v2, expectsRegionSetAlias);

        System.Text.Json.JsonElement goldenCase = V2StandardMergeGoldenTestSupport.ReadGoldenCase(referenceIc);
        Dictionary<string, byte[]> inputs = V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs"));
        byte[] expectedOutput = V2StandardMergeGoldenTestSupport.ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        AssertDeclaredStandardMergePlan(v2.Plan, expectedOutput.LongLength, expectsLdc);
        CompositionRunResult result = await V2StandardMergeGoldenTestSupport.PreviewAsync(v2, inputs);

        V2StandardMergeGoldenTestSupport.AssertSuccessfulGoldenOutput(
            result,
            v2,
            expectedOutput);
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
        CompiledComposition composition,
        int expectedSourceLength)
    {
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        CompiledInputSlotRequirement dpSlot = Assert.Single(
            details.InputContract.Slots,
            slot => slot.SlotId == CompositionAddressSpaceIds.DpInput);
        CompiledSourceViewCoverageInputLengthRequirement lengthRequirement =
            Assert.IsType<CompiledSourceViewCoverageInputLengthRequirement>(dpSlot.LengthRequirement);
        Assert.Empty(lengthRequirement.ExpectedOuterLengths);
        Assert.Null(lengthRequirement.UnexpectedOuterLengthIssueCode);

        AddressSpace dpInput = composition.Plan.AddressSpaces.Single(
            space => space.AddressSpaceId == CompositionAddressSpaceIds.DpInput);
        Assert.Equal(expectedSourceLength, dpInput.Length);
        Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, dpInput.InputOversizePolicy);
        Assert.Empty(dpInput.AllowedInputLengths);
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
            ? ["copy-tp", "copy-dp", "copy-ldc"]
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
        CompiledInputSlotRequirement ldcSlot = Assert.Single(details.InputContract.Slots,
            slot => slot.SlotId == CompositionAddressSpaceIds.LdcInput);
        Assert.Equal("ldc", ldcSlot.Role);
        Assert.Equal(CompiledInputArtifactClass.Auxiliary, ldcSlot.ArtifactClass);
        Assert.True(ldcSlot.Required);
        Assert.Equal(CompiledInputSlotCardinality.ExactlyOne, ldcSlot.Cardinality);
        CompiledSourceViewCoverageInputLengthRequirement lengthRequirement =
            Assert.IsType<CompiledSourceViewCoverageInputLengthRequirement>(ldcSlot.LengthRequirement);
        Assert.Empty(lengthRequirement.ExpectedOuterLengths);
        Assert.Null(lengthRequirement.UnexpectedOuterLengthIssueCode);
        Assert.Equal(0x62000, Assert.Single(composition.Plan.AddressSpaces,
            space => space.AddressSpaceId == CompositionAddressSpaceIds.LdcInput).Length);
    }
}
