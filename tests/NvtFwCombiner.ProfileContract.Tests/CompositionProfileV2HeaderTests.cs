using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable map-independent v2 profile header values.</summary>
public sealed class CompositionProfileV2HeaderTests
{
    private const string FamilyHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    /// <summary>Verifies experience policy retains only compiler-consumed dimensions.</summary>
    [Fact]
    public void ExperienceRetainsCompilerConsumedPolicyValues()
    {
        (string ExperienceId, LayoutPolicy LayoutPolicy, InputPolicy InputPolicy) experience =
            CompositionProfileNormalizer.NormalizeExperience(new CompositionProfileExperienceDocument(
                "general-replace",
                "advanced",
                "user-defined",
                "extensible",
                "exact-count",
                "experience.general-replace"));

        Assert.Equal("general-replace", experience.ExperienceId);
        Assert.Equal(LayoutPolicy.UserDefined, experience.LayoutPolicy);
        Assert.Equal(InputPolicy.Extensible, experience.InputPolicy);
    }

    /// <summary>Verifies unknown policy tokens cannot enter normalized experience values.</summary>
    [Fact]
    public void HeaderValuesRejectUnknownEnums()
    {
        _ = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeExperience(new CompositionProfileExperienceDocument(
                ExperienceIds.StandardMerge,
                "system",
                "future",
                "fixed",
                "hidden",
                "experience.standard-merge")));
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
        _ = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeExperience(new CompositionProfileExperienceDocument(
                "general_replace",
                "advanced",
                "user-defined",
                "extensible",
                "exact-count",
                "experience.general-replace")));

        CompositionProfileMapBinding binding = Binding(familyVersion: "1.2.3-rc.1+build.5");
        Assert.Equal("1.2.3-rc.1+build.5", binding.FamilyVersion);
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
