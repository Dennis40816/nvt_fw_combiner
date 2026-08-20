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
        "349563de9aaf5ee6fffc38941cab09563e857ebd349bbd8ded0efe08be67a2ba",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51917",
        "nt51917-general-merge-logical-candidate")]
    [InlineData(
        "nt51917-nt51927-general-merge-logical-candidate",
        "349563de9aaf5ee6fffc38941cab09563e857ebd349bbd8ded0efe08be67a2ba",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51927",
        "nt51927-general-merge-logical-candidate")]
    [InlineData(
        "nt51923-nt51926-general-merge-logical-candidate",
        "074708d3ab91eabbef4c1344f02dca36c4803fd09cfdda61752dbe02aad3b748",
        "nt51923-standard-merge",
        "nt51923-nt51926.json",
        "nt51923-nt51926",
        "NT51923",
        "nt51923-general-merge-logical-candidate")]
    [InlineData(
        "nt51923-nt51926-general-merge-logical-candidate",
        "074708d3ab91eabbef4c1344f02dca36c4803fd09cfdda61752dbe02aad3b748",
        "nt51923-standard-merge",
        "nt51923-nt51926.json",
        "nt51923-nt51926",
        "NT51926",
        "nt51926-general-merge-logical-candidate")]
    [InlineData(
        "nt51928-general-merge-logical-candidate",
        "7410f193c85cbc9092bea46d5674649b9e8f91f7b347e06454a0f899765e3867",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51928",
        "nt51928-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "2891a94e01bf07d68bc751599038ae88eeb95c9d1f705722912fbd55bf8806ec",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51950",
        "nt51950-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "2891a94e01bf07d68bc751599038ae88eeb95c9d1f705722912fbd55bf8806ec",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51951",
        "nt51951-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "d6513b7026c6438f574c334476176bf3bd704aed9bd0279733706942aeb02910",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51919",
        "nt51919-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "d6513b7026c6438f574c334476176bf3bd704aed9bd0279733706942aeb02910",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51929",
        "nt51929-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "d6513b7026c6438f574c334476176bf3bd704aed9bd0279733706942aeb02910",
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
