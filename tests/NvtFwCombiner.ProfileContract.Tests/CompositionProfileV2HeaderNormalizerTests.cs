using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DTO normalization for v2 promotion, experience, and map binding headers.</summary>
public sealed class CompositionProfileV2HeaderNormalizerTests
{
    private const string FamilyHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    /// <summary>Verifies every closed promotion stage maps without fallback.</summary>
    [Fact]
    public void PromotionMapsEveryStage()
    {
        (string Token, CompositionProfilePromotionStage Stage)[] cases =
        [
            ("known", CompositionProfilePromotionStage.Known),
            ("map-resolvable", CompositionProfilePromotionStage.MapResolvable),
            ("inspectable", CompositionProfilePromotionStage.Inspectable),
            ("authorable", CompositionProfilePromotionStage.Authorable),
            ("compilable", CompositionProfilePromotionStage.Compilable),
            ("executable-candidate", CompositionProfilePromotionStage.ExecutableCandidate),
            ("supported", CompositionProfilePromotionStage.Supported),
        ];

        foreach ((string token, CompositionProfilePromotionStage expected) in cases)
        {
            CompositionProfilePromotion promotion = CompositionProfileNormalizer.NormalizePromotion(
                new CompositionProfilePromotionDocument(token, []));
            Assert.Equal(expected, promotion.Stage);
        }
    }

    /// <summary>Verifies every blocker kind maps and evidence is normalized deterministically.</summary>
    [Fact]
    public void PromotionMapsEveryBlockerKind()
    {
        string[] tokens =
        [
            "map", "metadata", "operation", "processor", "integrity",
            "golden", "human-review", "ui", "release",
        ];
        CompositionProfilePromotionBlockerDocument[] documents =
        [
            .. tokens.Select((token, index) => new CompositionProfilePromotionBlockerDocument(
                $"blocker-{index}",
                token,
                "Pending evidence.",
                ["z-evidence", "a-evidence"])),
        ];

        CompositionProfilePromotion promotion = CompositionProfileNormalizer.NormalizePromotion(
            new CompositionProfilePromotionDocument("known", documents));

        Assert.Equal(9, promotion.Blockers.Count);
        Assert.Equal(["a-evidence", "z-evidence"], promotion.Blockers[0].EvidenceRefs);
        Assert.Equal(
            Enum.GetValues<CompositionProfileBlockerKind>().Order(),
            promotion.Blockers.Select(static blocker => blocker.Kind).Order());
    }

    /// <summary>Verifies experience tokens validate while compiler-consumed policy dimensions remain typed.</summary>
    [Fact]
    public void ExperienceMapsClosedPolicyTokens()
    {
        var document = new CompositionProfileExperienceDocument(
            "general-replace",
            "advanced",
            "user-defined",
            "extensible",
            "exact-count",
            "experience.general-replace");

        CompositionProfileExperience experience = CompositionProfileNormalizer.NormalizeExperience(document);

        Assert.Equal(LayoutPolicy.UserDefined, experience.LayoutPolicy);
        Assert.Equal(InputPolicy.Extensible, experience.InputPolicy);
    }

    /// <summary>Verifies trusted family/map references normalize into immutable canonical sets.</summary>
    [Fact]
    public void MapBindingNormalizesTrustedReferences()
    {
        var document = new CompositionProfileMapBindingDocument(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            ["map-z", "map-a"],
            ["region-z", "region-a"],
            ["structure-z", "structure-a"],
            ["register-replace", "ab-code"]);

        CompositionProfileMapBinding binding = CompositionProfileNormalizer.NormalizeMapBinding(document);

        Assert.Equal(["map-a", "map-z"], binding.MapIds);
        Assert.Equal(["region-a", "region-z"], binding.RequiredRegionIds);
        Assert.Equal(["structure-a", "structure-z"], binding.RequiredMetadataStructureIds);
        Assert.Equal(["ab-code", "register-replace"], binding.RequiredCapabilityIds);
    }

    /// <summary>Verifies unknown tokens and constructor invariants preserve exact source paths.</summary>
    [Fact]
    public void HeaderNormalizationRejectsUnknownAndInvalidValuesWithPaths()
    {
        CompositionProfileNormalizationException stage = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizePromotion(
                new CompositionProfilePromotionDocument("future", [])));
        CompositionProfileNormalizationException blocker = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizePromotion(
                new CompositionProfilePromotionDocument(
                    "known",
                    [new CompositionProfilePromotionBlockerDocument(
                        "blocker", "future", "reason", [])])));
        CompositionProfileNormalizationException experience = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeExperience(
                new CompositionProfileExperienceDocument(
                    "Experience", "system", "fixed", "fixed", "hidden", "name")));
        CompositionProfileNormalizationException audience = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeExperience(
                new CompositionProfileExperienceDocument(
                    "experience", "future", "fixed", "fixed", "hidden", "name")));
        CompositionProfileNormalizationException topology = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeExperience(
                new CompositionProfileExperienceDocument(
                    "experience", "system", "fixed", "fixed", "future", "name")));
        CompositionProfileNormalizationException map = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeMapBinding(
                new CompositionProfileMapBindingDocument(
                    "family", "1.0", FamilyHash, ["map"], ["region"], [], [])));

        Assert.Equal("promotion.stage", stage.Path);
        Assert.Equal("promotion.blockers[0].kind", blocker.Path);
        Assert.Equal("experience", experience.Path);
        Assert.Equal("experience.audience", audience.Path);
        Assert.Equal("experience.topologyAuthoring", topology.Path);
        Assert.Equal("mapBinding", map.Path);
    }

    /// <summary>Verifies supported promotion cannot silently retain blockers.</summary>
    [Fact]
    public void SupportedPromotionRejectsBlockers()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizePromotion(
                new CompositionProfilePromotionDocument(
                    "supported",
                    [new CompositionProfilePromotionBlockerDocument(
                        "human-review", "human-review", "Pending review.", [])])));

        Assert.Equal("promotion", exception.Path);
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }
}
