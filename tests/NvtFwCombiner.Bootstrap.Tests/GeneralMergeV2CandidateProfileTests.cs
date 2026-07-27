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
        "nt51920-general-merge-logical-candidate",
        "d2f87973576f54b80439f30ef1790f47df2994a6811673f0ceb8ecd5cacdbdc7",
        "nt51920-standard-merge",
        "nt51920.json",
        "nt51920",
        "NT51920",
        "nt51920-general-merge-logical-candidate")]
    [InlineData(
        "nt51917-nt51927-general-merge-logical-candidate",
        "6e0ecbad0b1205f860c62d2a251ad54193dc53d49f88d0fbecfdb86ddfa951ea",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51917",
        "nt51917-general-merge-logical-candidate")]
    [InlineData(
        "nt51917-nt51927-general-merge-logical-candidate",
        "6e0ecbad0b1205f860c62d2a251ad54193dc53d49f88d0fbecfdb86ddfa951ea",
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
        "207b1cf26169f62d7702f7daa1844272cb666ff368b405b8ebe5283b7986f861",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51928",
        "nt51928-general-merge-logical-candidate")]
    [InlineData(
        "nt51930-general-merge-logical-candidate",
        "72fce0d8405eaf969d781ad684aea9c654e0185bc625864b642e302d6f1698a4",
        "nt51930-standard-merge",
        "nt51930.json",
        "nt51930",
        "NT51930",
        "nt51930-general-merge-logical-candidate")]
    [InlineData(
        "nt51931-general-merge-logical-candidate",
        "ce3b18aede5c884b074b6f9253d45a255e82a2147ec76bd300e7548d6fdc52fe",
        "nt51931-standard-merge",
        "nt51931.json",
        "nt51931",
        "NT51931",
        "nt51931-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "0d6754abc0f60e7fd967023759893df67e834971ec568027fe861efc7d7d21f0",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51950",
        "nt51950-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "0d6754abc0f60e7fd967023759893df67e834971ec568027fe861efc7d7d21f0",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51951",
        "nt51951-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "fabc02474120adb7659d9e069b9c60395cad4620282afdf8ff9e9b915acc4283",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51919",
        "nt51919-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "fabc02474120adb7659d9e069b9c60395cad4620282afdf8ff9e9b915acc4283",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51929",
        "nt51929-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "fabc02474120adb7659d9e069b9c60395cad4620282afdf8ff9e9b915acc4283",
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
                16,
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
        Assert.Equal(
            File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
                "profiles",
                "built-in",
                sourceBundleDirectory,
                "families",
                familyFileName)),
            File.ReadAllBytes(Path.Combine(workspace.Root, "families", familyFileName)));
    }
}
