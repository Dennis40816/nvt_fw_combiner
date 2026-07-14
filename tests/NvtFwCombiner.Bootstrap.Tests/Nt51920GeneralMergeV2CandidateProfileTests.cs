using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Direct bundle evidence for the non-routed NT51920 logical-output General Merge candidate.</summary>
public sealed class Nt51920GeneralMergeV2CandidateProfileTests
{
    private const string BundleDirectory = "nt51920-general-merge-logical-candidate";
    private const string BundleContentHash = "d2f87973576f54b80439f30ef1790f47df2994a6811673f0ceb8ecd5cacdbdc7";

    /// <summary>Verifies the candidate preserves the exact NT51920 family snapshot while making no physical map claim.</summary>
    [Fact]
    public void CandidateBundleCompilesOnlyItsExactLogicalFamilyMember()
    {
        using var workspace = TempWorkspace.Create();
        TrustedProfileBundleCatalog catalog = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(
            workspace,
            BundleDirectory,
            BundleContentHash);
        V2CompositionPlanCompileResult compile = TrustedV2CompositionCompiler.CompileLogicalOutput(
            catalog,
            "nt51920-general-merge-logical-candidate",
            "0.1.0",
            "NT51920",
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
        Assert.Equal("nt51920", context.FamilyId);
        Assert.Equal("NT51920", context.MemberId);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, details.Provenance.Promotion.Stage);
        Assert.Empty(details.RegionAccessContract.Requirements);
        Assert.Empty(details.RegionAccessContract.ResolvedViews);
        Assert.Equal(
            File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
                "profiles",
                "built-in",
                "nt51920-standard-merge",
                "families",
                "nt51920.json")),
            File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
                "profiles",
                "built-in",
                BundleDirectory,
                "families",
                "nt51920.json")));
    }
}
