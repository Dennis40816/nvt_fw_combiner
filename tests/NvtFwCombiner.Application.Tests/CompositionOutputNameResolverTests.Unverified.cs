using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionOutputNameResolverTests
{
    private const string UnverifiedFileName =
        "NT51929_FlashCode_DxxxxTxxxx_20260728_UNVERIFIED.bin";

    /// <summary>The marker is added only for an executable candidate that still has a Golden blocker.</summary>
    [Theory]
    [InlineData(CompiledProfilePromotionStage.ExecutableCandidate, true, true, true)]
    [InlineData(CompiledProfilePromotionStage.ExecutableCandidate, true, false, true)]
    [InlineData(CompiledProfilePromotionStage.ExecutableCandidate, false, true, false)]
    [InlineData(CompiledProfilePromotionStage.Supported, false, false, false)]
    [InlineData(CompiledProfilePromotionStage.Compilable, true, true, false)]
    public void UnverifiedMarkerFollowsPromotionStageAndGoldenBlocker(
        CompiledProfilePromotionStage stage,
        bool golden,
        bool humanReview,
        bool expected)
    {
        CompiledComposition composition = CreateUnverifiedCandidate(
            stage, golden, humanReview, NormalFlashCodeOutput());

        Assert.Equal(expected, UnverifiedCandidateOutputName.Applies(composition));
    }

    /// <summary>TP-firmware naming is typed normal naming and receives the same marker.</summary>
    [Fact]
    public void UnverifiedMarkerCoversTpFirmwareRenderer()
    {
        CompiledComposition composition = CreateUnverifiedCandidate(
            CompiledProfilePromotionStage.ExecutableCandidate, golden: true, humanReview: false, TpFirmwareOutput());

        Assert.True(UnverifiedCandidateOutputName.Applies(composition));
    }

    /// <summary>Static and AB Code candidate names are not changed by this rule.</summary>
    [Fact]
    public void UnverifiedMarkerSkipsStaticAndAbCodeRenderers()
    {
        var staticOutput = new CompiledOutputNamingRequirement(
            "output.bin", allowOverride: true, CompiledOutputInvalidCharacterPolicy.Reject, requiredTokenIds: []);
        var abOutput = new CompiledOutputNamingRequirement(
            CompiledOutputNamingRequirement.AbCodeV1Template,
            allowOverride: true,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["date", "dp-a", "dp-b", "ic", "tp-a", "tp-b"]);

        foreach (CompiledOutputNamingRequirement output in new[] { staticOutput, abOutput })
        {
            CompiledComposition composition = CreateUnverifiedCandidate(
                CompiledProfilePromotionStage.ExecutableCandidate, golden: true, humanReview: true, output);
            Assert.False(UnverifiedCandidateOutputName.Applies(composition));
        }
    }

    /// <summary>The automatic name carries the marker and the token list says why.</summary>
    [Fact]
    public void UnverifiedCandidateAutomaticNameCarriesMarkerAndVerificationToken()
    {
        CompiledOutputNamingRequirement output = NormalFlashCodeOutput();
        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929", output, output.FileNameTemplate, isExplicitOverride: false,
            acceptedInspection: null, inputSummaries: [], RunTime, markUnverified: true);

        Assert.Equal(UnverifiedFileName, resolved.FileName);
        OutputNamingSummary summary = Assert.IsType<OutputNamingSummary>(resolved.Summary);
        Assert.Equal(UnverifiedFileName, summary.AutomaticFileName);
        OutputNamingTokenSummary token = Assert.Single(summary.Tokens, candidate => candidate.TokenId == "verification");
        Assert.Equal("unverified", token.Value);
        Assert.True(token.IsKnown);
        Assert.Equal(output.FileNameTemplate, summary.Template);
    }

    /// <summary>Without the flag, every existing automatic name stays byte-identical.</summary>
    [Fact]
    public void VerifiedRouteNameHasNoMarkerAndNoVerificationToken()
    {
        CompiledOutputNamingRequirement output = NormalFlashCodeOutput();
        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929", output, output.FileNameTemplate, isExplicitOverride: false,
            acceptedInspection: null, inputSummaries: [], RunTime);

        Assert.Equal("NT51929_FlashCode_DxxxxTxxxx_20260728.bin", resolved.FileName);
        OutputNamingSummary summary = Assert.IsType<OutputNamingSummary>(resolved.Summary);
        Assert.DoesNotContain(summary.Tokens, candidate => candidate.TokenId == "verification");
    }

    /// <summary>A custom file name cannot remove the marker of an unverified candidate.</summary>
    [Fact]
    public void UnverifiedCandidateRejectsExplicitOverrideWithoutThrowing()
    {
        CompiledOutputNamingRequirement output = NormalFlashCodeOutput();
        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929", output, "custom.bin", isExplicitOverride: true,
            acceptedInspection: null, inputSummaries: [], RunTime, markUnverified: true);

        Assert.Null(resolved.Summary);
        CompositionIssue issue = Assert.Single(resolved.Issues);
        Assert.Equal("output-naming.unverified-override-rejected", issue.Code);

        OutputNameResolution verified = CompiledOutputNameResolver.ResolveNormal(
            "NT51929", output, "custom.bin", isExplicitOverride: true,
            acceptedInspection: null, inputSummaries: [], RunTime);
        Assert.Equal("custom.bin", verified.FileName);
    }

    /// <summary>The marker goes before the extension and is never added twice.</summary>
    [Theory]
    [InlineData("a.bin", "a_UNVERIFIED.bin")]
    [InlineData("a_UNVERIFIED.bin", "a_UNVERIFIED.bin")]
    [InlineData("a.BIN", "a_UNVERIFIED.BIN")]
    [InlineData("a", "a_UNVERIFIED")]
    public void ApplyInsertsMarkerOnceBeforeExtension(string name, string expected)
    {
        Assert.Equal(expected, UnverifiedCandidateOutputName.Apply(name));
    }

    /// <summary>A request that overrides an unverified candidate name fails output-name validation.</summary>
    [Fact]
    public void RuntimeOutputNameValidationRejectsOverrideOfUnverifiedCandidate()
    {
        CompiledComposition composition = CreateUnverifiedCandidate(
            CompiledProfilePromotionStage.ExecutableCandidate, golden: true, humanReview: true, NormalFlashCodeOutput());

        _ = Assert.Throws<ArgumentException>(() =>
            CompositionRunRequest.ValidateRuntimeOutputName(composition, "custom.bin", outputFileNameIsOverride: true));
    }

    private static CompiledComposition CreateUnverifiedCandidate(
        CompiledProfilePromotionStage stage,
        bool golden,
        bool humanReview,
        CompiledOutputNamingRequirement output)
    {
        var blockers = new List<CompiledProfilePromotionBlocker>();
        if (stage == CompiledProfilePromotionStage.Supported)
        {
            golden = false;
            humanReview = false;
        }
        else
        {
            blockers.Add(new("map-blocker", CompiledProfilePromotionBlockerKind.Map,
                "Official map is not available.", []));
        }

        if (golden)
        {
            blockers.Add(new("golden-blocker", CompiledProfilePromotionBlockerKind.Golden,
                "Independent Golden result is not available.", []));
        }

        if (humanReview)
        {
            blockers.Add(new("review-blocker", CompiledProfilePromotionBlockerKind.HumanReview,
                "Owner approval is pending.", []));
        }

        var plan = new CompositionPlan(
            [ImageInitialization.Blank("output-image", 4, 0)],
            "output-image",
            [
                new AddressSpace("source-a", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-a", 10, "source-a", new ByteRange(0, 4), "output-image", new ByteRange(0, 4),
                    OverlapPolicy.Reject, "copy the logical input"),
            ]);
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity("naming-bundle-v2", "1.0.0", new string('a', 64), "release-binding"),
            new ProfileBundleEntryIdentity("naming-profile-entry", new string('b', 64)),
            new LogicalOutputV2CompilationContext("synthetic-family", "1.0.0", new string('c', 64), "NT-SYNTHETIC"),
            new CompiledProfilePromotion(stage, blockers),
            ["naming-profile-evidence"],
            [],
            []);
        var details = new V2CompiledCompositionDetails(
            "naming-candidate",
            "1.0.0",
            ExperienceIds.StandardMerge,
            CompositionKind.Merge,
            provenance,
            new CompiledInputContract(
                [CompiledInputSlotTestFactory.Create(
                        "source-slot",
                        "source",
                        CompiledInputArtifactClass.Auxiliary,
                        required: true,
                        CompiledInputSlotCardinality.OneOrMore,
                        [".bin"],
                        new CompiledBoundedInputLengthRequirement(1, int.MaxValue),
                        new CompiledNoInputNormalization())],
                [new CompiledInputSpaceBinding("source-a", "source-slot", CompiledInputInstancePolicy.PerBinding)]),
            new CompiledRegionAccessContract([], []),
            output);
        return CompiledComposition.CreateV2(plan, details);
    }
}
