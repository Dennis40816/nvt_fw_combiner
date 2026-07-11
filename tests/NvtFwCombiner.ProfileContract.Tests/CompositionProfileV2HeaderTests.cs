using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable map-independent v2 profile header values.</summary>
public sealed class CompositionProfileV2HeaderTests
{
    private const string FamilyHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    /// <summary>Verifies promotion blockers snapshot, sort, and retain evidence without caller mutation.</summary>
    [Fact]
    public void PromotionSnapshotsAndOrdersBlockers()
    {
        List<string> evidence = ["z-evidence", "a-evidence"];
        var second = new CompositionProfilePromotionBlocker(
            "second",
            CompositionProfileBlockerKind.Golden,
            "Golden evidence is pending.",
            evidence);
        var first = new CompositionProfilePromotionBlocker(
            "first",
            CompositionProfileBlockerKind.HumanReview,
            "Owner review is pending.",
            []);
        var blockers = new List<CompositionProfilePromotionBlocker> { second, first };

        var promotion = new CompositionProfilePromotion(
            CompositionProfilePromotionStage.ExecutableCandidate,
            blockers);
        evidence.Add("late-evidence");
        blockers.Clear();

        Assert.Equal(["first", "second"], promotion.Blockers.Select(static blocker => blocker.BlockerId));
        Assert.Equal(["a-evidence", "z-evidence"], promotion.Blockers[1].EvidenceRefs);
        Assert.Equal(CompositionProfilePromotionStage.ExecutableCandidate, promotion.Stage);
    }

    /// <summary>Verifies supported profiles are blocker-free and blocker ids are unambiguous.</summary>
    [Fact]
    public void PromotionRejectsSupportedBlockersAndDuplicateIds()
    {
        CompositionProfilePromotionBlocker blocker = Blocker("pending");

        _ = Assert.Throws<ArgumentException>(() => new CompositionProfilePromotion(
            CompositionProfilePromotionStage.Supported,
            [blocker]));
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfilePromotion(
            CompositionProfilePromotionStage.Known,
            [blocker, Blocker("pending")]));
    }

    /// <summary>Verifies experience policy keeps orthogonal authoring dimensions typed.</summary>
    [Fact]
    public void ExperienceKeepsOrthogonalPolicyValues()
    {
        var experience = new CompositionProfileExperience(
            "general-replace",
            AudienceKind.Advanced,
            LayoutPolicy.UserDefined,
            InputPolicy.Extensible,
            CompositionProfileTopologyAuthoring.ExactCount,
            "experience.general-replace");

        Assert.Equal("general-replace", experience.ExperienceId);
        Assert.Equal(AudienceKind.Advanced, experience.Audience);
        Assert.Equal(LayoutPolicy.UserDefined, experience.LayoutPolicy);
        Assert.Equal(InputPolicy.Extensible, experience.InputPolicy);
        Assert.Equal(CompositionProfileTopologyAuthoring.ExactCount, experience.TopologyAuthoring);
        Assert.Equal("experience.general-replace", experience.DisplayNameKey);
    }

    /// <summary>Verifies invalid enum carriers cannot enter normalized experience or promotion values.</summary>
    [Fact]
    public void HeaderValuesRejectUnknownEnums()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompositionProfilePromotion(
            (CompositionProfilePromotionStage)99,
            []));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompositionProfilePromotionBlocker(
            "blocker",
            (CompositionProfileBlockerKind)99,
            "reason",
            []));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompositionProfileExperience(
            "experience",
            (AudienceKind)99,
            LayoutPolicy.Fixed,
            InputPolicy.Fixed,
            CompositionProfileTopologyAuthoring.Hidden,
            "experience.name"));
    }

    /// <summary>Verifies map binding snapshots every unordered identity set deterministically.</summary>
    [Fact]
    public void MapBindingSnapshotsAndOrdersCanonicalReferences()
    {
        var mapIds = new List<string> { "map-z", "map-a" };
        var regionIds = new List<string> { "region-z", "region-a" };
        var metadataIds = new List<string> { "structure-z", "structure-a" };
        var capabilityIds = new List<string> { "register-replace", "ab-code" };

        var binding = new CompositionProfileMapBinding(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            mapIds,
            regionIds,
            metadataIds,
            capabilityIds);
        mapIds.Clear();
        regionIds.Clear();
        metadataIds.Clear();
        capabilityIds.Clear();

        Assert.Equal(["map-a", "map-z"], binding.MapIds);
        Assert.Equal(["region-a", "region-z"], binding.RequiredRegionIds);
        Assert.Equal(["structure-a", "structure-z"], binding.RequiredMetadataStructureIds);
        Assert.Equal(["ab-code", "register-replace"], binding.RequiredCapabilityIds);
        Assert.Equal(FamilyHash, binding.FamilyContentHash);
    }

    /// <summary>Verifies map binding rejects malformed trust anchors and ambiguous required ids.</summary>
    [Fact]
    public void MapBindingRejectsInvalidTrustAndReferenceSets()
    {
        _ = Assert.Throws<ArgumentException>(() => Binding(familyHash: new string('C', 64)));
        _ = Assert.Throws<ArgumentException>(() => Binding(familyId: "Family"));
        _ = Assert.Throws<ArgumentException>(() => Binding(familyVersion: "1.0"));
        _ = Assert.Throws<ArgumentException>(() => Binding(mapIds: []));
        _ = Assert.Throws<ArgumentException>(() => Binding(regionIds: []));
        _ = Assert.Throws<ArgumentException>(() => Binding(mapIds: ["map", "map"]));
        _ = Assert.Throws<ArgumentException>(() => Binding(mapIds: ["map_A"]));
        _ = Assert.Throws<ArgumentException>(() => Binding(regionIds: ["region/path"]));
        _ = Assert.Throws<ArgumentException>(() => Binding(metadataIds: ["config", "config"]));
        _ = Assert.Throws<ArgumentException>(() => Binding(capabilityIds: ["ab-code", ""]));
    }

    /// <summary>Verifies singular canonical ids and valid extended semantic versions.</summary>
    [Fact]
    public void HeaderValuesEnforceCanonicalSingularIdentity()
    {
        _ = Assert.Throws<ArgumentException>(() => Blocker("Human-Review"));
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileExperience(
            "general_replace",
            AudienceKind.Advanced,
            LayoutPolicy.UserDefined,
            InputPolicy.Extensible,
            CompositionProfileTopologyAuthoring.Hidden,
            "experience.general-replace"));

        CompositionProfileMapBinding binding = Binding(familyVersion: "1.2.3-rc.1+build.5");
        Assert.Equal("1.2.3-rc.1+build.5", binding.FamilyVersion);
    }

    private static CompositionProfilePromotionBlocker Blocker(string blockerId)
    {
        return new CompositionProfilePromotionBlocker(
            blockerId,
            CompositionProfileBlockerKind.Map,
            "Map evidence is pending.",
            []);
    }

    private static CompositionProfileMapBinding Binding(
        string familyId = "synthetic-family",
        string familyVersion = "1.0.0",
        string familyHash = FamilyHash,
        IEnumerable<string>? mapIds = null,
        IEnumerable<string>? regionIds = null,
        IEnumerable<string>? metadataIds = null,
        IEnumerable<string>? capabilityIds = null)
    {
        return new CompositionProfileMapBinding(
            familyId,
            familyVersion,
            familyHash,
            mapIds ?? ["map"],
            regionIds ?? ["root"],
            metadataIds ?? [],
            capabilityIds ?? []);
    }
}
