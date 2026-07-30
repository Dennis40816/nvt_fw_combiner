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
        "3dd5c0adb73b7ee5b0c0762e79ab8ddfa800696a0646138d15fba8984d84d2eb",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51917",
        "nt51917-general-merge-logical-candidate")]
    [InlineData(
        "nt51917-nt51927-general-merge-logical-candidate",
        "3dd5c0adb73b7ee5b0c0762e79ab8ddfa800696a0646138d15fba8984d84d2eb",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51927",
        "nt51927-general-merge-logical-candidate")]
    [InlineData(
        "nt51923-nt51926-general-merge-logical-candidate",
        "26f12851f81d55bb88a0a0e18ab4f10f451747369e797efbc69fdbf05cdf5a96",
        "nt51923-standard-merge",
        "nt51923-nt51926.json",
        "nt51923-nt51926",
        "NT51923",
        "nt51923-general-merge-logical-candidate")]
    [InlineData(
        "nt51923-nt51926-general-merge-logical-candidate",
        "26f12851f81d55bb88a0a0e18ab4f10f451747369e797efbc69fdbf05cdf5a96",
        "nt51923-standard-merge",
        "nt51923-nt51926.json",
        "nt51923-nt51926",
        "NT51926",
        "nt51926-general-merge-logical-candidate")]
    [InlineData(
        "nt51928-general-merge-logical-candidate",
        "a774a7622aedfac94fc045b56e7fe04902359ebe59747acd5486ed336b6d5da2",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51928",
        "nt51928-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "387e70efd2bfb4591852f700b5f0b1b3763b0fb7c5edd2cefa10d998b73b29b6",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51950",
        "nt51950-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "387e70efd2bfb4591852f700b5f0b1b3763b0fb7c5edd2cefa10d998b73b29b6",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51951",
        "nt51951-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "5659a4095a6fce9ab3f46f9415759f7aeba321adfddb891e52871b2d6acff4f8",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51919",
        "nt51919-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "5659a4095a6fce9ab3f46f9415759f7aeba321adfddb891e52871b2d6acff4f8",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51929",
        "nt51929-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "5659a4095a6fce9ab3f46f9415759f7aeba321adfddb891e52871b2d6acff4f8",
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
        V2CompositionPlanCompileResult compile = TrustedV2CompositionCompiler.CompileLogicalOutput(
            catalog,
            profileId,
            "0.1.0",
            memberId,
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(16),
                [new V2LogicalOutputInputBinding("source-a", "source", 4)],
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
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(composition.Authority);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        LogicalOutputV2CompilationContext context = Assert.IsType<LogicalOutputV2CompilationContext>(
            details.Provenance.Context);
        Assert.Equal(familyId, context.FamilyId);
        Assert.Equal(memberId, context.MemberId);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, details.Provenance.Promotion.Stage);
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
