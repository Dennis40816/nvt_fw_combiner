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
        "998d3f724868b50a381834b798dce140f7877cf0e69205567288d632672a680c",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51917",
        "nt51917-general-merge-logical-candidate")]
    [InlineData(
        "nt51917-nt51927-general-merge-logical-candidate",
        "998d3f724868b50a381834b798dce140f7877cf0e69205567288d632672a680c",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51927",
        "nt51927-general-merge-logical-candidate")]
    [InlineData(
        "nt51923-nt51926-general-merge-logical-candidate",
        "60bcf8b9abf814a302d80383270826586811ce0481a28673f776fb88e107b76b",
        "nt51923-standard-merge",
        "nt51923-nt51926.json",
        "nt51923-nt51926",
        "NT51923",
        "nt51923-general-merge-logical-candidate")]
    [InlineData(
        "nt51923-nt51926-general-merge-logical-candidate",
        "60bcf8b9abf814a302d80383270826586811ce0481a28673f776fb88e107b76b",
        "nt51923-standard-merge",
        "nt51923-nt51926.json",
        "nt51923-nt51926",
        "NT51926",
        "nt51926-general-merge-logical-candidate")]
    [InlineData(
        "nt51928-general-merge-logical-candidate",
        "e3a4443d3c845b57d3ba66723d659b26a9bd6602824d92571f77b74a053fef2a",
        "nt51927-standard-merge",
        "nt51927-nt51928.json",
        "nt51917-nt51927-nt51928-canonical-container",
        "NT51928",
        "nt51928-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "101a97d0101abae37e27568f21b7af9c491ef47af3d26b0a6d5d4f5cb0945a05",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51950",
        "nt51950-general-merge-logical-candidate")]
    [InlineData(
        "nt51950-nt51951-general-merge-logical-candidate",
        "101a97d0101abae37e27568f21b7af9c491ef47af3d26b0a6d5d4f5cb0945a05",
        "nt51950-nt51951-standard-merge",
        "nt51950-nt51951-dp-perspective.json",
        "nt51950-nt51951-dp-perspective",
        "NT51951",
        "nt51951-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "54f5c973b51b99acf3447d2909fbc22270df52129d5aadaecf464b92cdcf5bd8",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51919",
        "nt51919-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "54f5c973b51b99acf3447d2909fbc22270df52129d5aadaecf464b92cdcf5bd8",
        "nt51929-standard-merge",
        "nt51929-nt51932.json",
        "nt51929-nt51932",
        "NT51929",
        "nt51929-general-merge-logical-candidate")]
    [InlineData(
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "54f5c973b51b99acf3447d2909fbc22270df52129d5aadaecf464b92cdcf5bd8",
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
