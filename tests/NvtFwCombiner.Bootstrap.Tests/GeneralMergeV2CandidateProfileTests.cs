using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Direct bundle evidence for routed logical-output General Merge profiles.</summary>
public sealed class GeneralMergeV2CandidateProfileTests
{
    /// <summary>Verifies each candidate binds one exact member of its snapshot family without a physical map claim.</summary>
    [Theory]
    [InlineData(
        "nt51917-nt51927-general-merge-logical-candidate",
        "649f0c646e612b41e092439c0ed1502d04b9dcee58a62de45902917612a60af6",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51917",
        "nt51917-general-merge-logical-candidate")]
    [InlineData(
        "nt51917-nt51927-general-merge-logical-candidate",
        "649f0c646e612b41e092439c0ed1502d04b9dcee58a62de45902917612a60af6",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51927",
        "nt51927-general-merge-logical-candidate")]
    [InlineData(
        "nt51923-nt51926-general-merge-logical-candidate",
        "b78e85de0846cca51790762a2c1bc0510a1d34728d40420129a98fc0a0c4ed08",
        "nt51923-standard-merge",
        "nt51923-nt51926.json",
        "nt51923-nt51926",
        "NT51923",
        "nt51923-general-merge-logical-candidate")]
    [InlineData(
        "nt51923-nt51926-general-merge-logical-candidate",
        "b78e85de0846cca51790762a2c1bc0510a1d34728d40420129a98fc0a0c4ed08",
        "nt51923-standard-merge",
        "nt51923-nt51926.json",
        "nt51923-nt51926",
        "NT51926",
        "nt51926-general-merge-logical-candidate")]
    [InlineData(
        "nt51928-general-merge-logical-candidate",
        "6d733b20ac46fc0affe626ca43a6e06bdea640f3bc179850a9831f9e812e871e",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51928",
        "nt51928-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "973966b55ab64092166b45dc000d8be60a49d4a93068611cddb835ce5e3496eb",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51950",
        "nt51950-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "973966b55ab64092166b45dc000d8be60a49d4a93068611cddb835ce5e3496eb",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51951",
        "nt51951-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "4f70de328604c8fe56fd407abb66ca87abbb46785c4c68dfdc988e73d0996d2b",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51919",
        "nt51919-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "4f70de328604c8fe56fd407abb66ca87abbb46785c4c68dfdc988e73d0996d2b",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51929",
        "nt51929-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "4f70de328604c8fe56fd407abb66ca87abbb46785c4c68dfdc988e73d0996d2b",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51932",
        "nt51932-general-merge-logical-candidate")]
    public void CandidateBundleCompilesOnlyItsExactLogicalFamilyMember(
        string bundleDirectory,
        string bundleContentHash,
        string sourceBundleDirectory,
        string familyFileName,
        string familyId,
        string memberId,
        string profileId)
    {
        ArgumentNullException.ThrowIfNull(bundleDirectory);
        ArgumentNullException.ThrowIfNull(bundleContentHash);
        ArgumentNullException.ThrowIfNull(sourceBundleDirectory);
        ArgumentNullException.ThrowIfNull(familyFileName);
        ArgumentNullException.ThrowIfNull(familyId);
        ArgumentNullException.ThrowIfNull(memberId);
        ArgumentNullException.ThrowIfNull(profileId);

        using var workspace = TempWorkspace.Create();
        TrustedProfileBundleCatalog catalog = BuiltInProfileMaterializationTestSupport.LoadSourceCandidateCatalog(
            workspace,
            bundleDirectory,
            bundleContentHash);
        V2CompositionPlanCompileResult compile = catalog.CompileLogicalOutput(
            profileId,
            "0.1.0",
            memberId,
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(16),
                [new V2ExplicitMappingInputBinding("source-a", "source", 4)],
                [new ExplicitMapping(
                    "copy-source",
                    1,
                    ExplicitMappingOperationKind.CopyRange,
                    "source-a",
                    new ByteRange(1, 3),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, 3),
                    OverlapPolicy.Reject,
                    1,
                    "candidate contract test")]));

        CompiledComposition composition = Assert.IsType<CompiledComposition>(compile.CompiledComposition);
        Assert.True(compile.IsCompiled);
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        LogicalOutputV2CompilationContext context = Assert.IsType<LogicalOutputV2CompilationContext>(
            details.Provenance.Context);
        Assert.Equal(familyId, context.FamilyId);
        Assert.Equal(memberId, context.MemberId);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, details.Provenance.Promotion.Stage);
        Assert.Equal(CompiledOutputNameRendererKind.Static, details.OutputNamingRequirement.RendererKind);
        Assert.Null(details.OutputNamingRequirement.RuleId);
        Assert.Equal(CompiledOutputArtifactType.Unspecified, details.OutputNamingRequirement.OutputArtifactType);
        Assert.Empty(details.OutputNamingRequirement.TokenRequirements);
        Assert.Empty(details.RegionAccessContract.Requirements);
        Assert.Empty(details.RegionAccessContract.ResolvedViews);
        byte[] sourceFamily = File.ReadAllBytes(
            RepositoryPaths.FromRepositoryRoot(
                "profiles",
                "built-in",
                sourceBundleDirectory,
                "families",
                familyFileName));
        byte[] candidateFamily = File.ReadAllBytes(
            Path.Combine(workspace.Root, "families", familyFileName));
        if (StringComparer.Ordinal.Equals(
                bundleDirectory,
                "nt51919-nt51929-nt51932-general-merge-logical-candidate"))
        {
            Assert.False(sourceFamily.SequenceEqual(candidateFamily));
        }
        else
        {
            Assert.Equal(sourceFamily, candidateFamily);
        }
    }
}
