using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;
using ResolvedFirmwareImageMap = NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests map-bound admission for normalized composition-profile-v2 definitions.</summary>
public sealed class CompositionProfileV2MapAdmissionTests
{
    private const string FamilyHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    /// <summary>Verifies exact family identity, effective regions, and declared metadata admit a profile.</summary>
    [Fact]
    public void ValidateAdmitsExactMapIdentityEffectiveRegionAndResolvedMetadata()
    {
        CompositionProfileMapAdmissionResult result = Admit(Profile());

        Assert.True(result.IsAdmitted);
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Admission);
        Assert.Empty(result.Admission.RequiredCapabilities);
    }

    /// <summary>Verifies every family identity component is compared exactly and independently.</summary>
    [Theory]
    [InlineData("other-family", "1.0.0", FamilyHash, "profile.v2.map.profile-family-id-mismatch")]
    [InlineData("synthetic-family", "2.0.0", FamilyHash, "profile.v2.map.profile-family-version-mismatch")]
    [InlineData("synthetic-family", "1.0.0", "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", "profile.v2.map.profile-family-content-hash-mismatch")]
    public void ValidateRejectsMismatchedFamilyIdentity(
        string familyId,
        string familyVersion,
        string familyContentHash,
        string expectedIssueCode)
    {
        CompositionProfileMapAdmissionResult result = Admit(Profile(familyId, familyVersion, familyContentHash));

        Assert.False(result.IsAdmitted);
        Assert.Equal([expectedIssueCode], result.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies a map resolved by another normalized family cannot be mixed into admission by matching strings alone.</summary>
    [Fact]
    public void ValidateRejectsResolvedMapOutsideTheSuppliedFamilyOwnership()
    {
        ResolutionContext resolvedContext = ResolveContext();
        ResolutionContext foreignContext = ResolveContext(
            familyContentHash: "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd");

        CompositionProfileMapAdmissionResult result = Admit(
            Profile(),
            new ResolutionContext(foreignContext.Family, resolvedContext.ResolvedMap));

        Assert.False(result.IsAdmitted);
        Assert.Equal(
            [
                "profile.v2.map.profile-family-content-hash-mismatch",
                "profile.v2.map.resolved-family-content-hash-mismatch",
                "profile.v2.map.resolved-map-not-owned",
            ],
            result.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies a resolved map must be explicitly declared by the profile binding.</summary>
    [Fact]
    public void ValidateRejectsResolvedMapOutsideDeclaredMapIds()
    {
        CompositionProfileMapAdmissionResult result = Admit(Profile(mapIds: ["other-map"]));

        Assert.False(result.IsAdmitted);
        Assert.Equal(["profile.v2.map.map-not-allowed"], result.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies all required physical regions must be present in the selected map's effective region graph.</summary>
    [Fact]
    public void ValidateRejectsMissingRequiredRegion()
    {
        CompositionProfileMapAdmissionResult result = Admit(Profile(regionIds: ["dp-code", "missing-region"]));

        Assert.False(result.IsAdmitted);
        Assert.Equal(["profile.v2.map.required-region-missing"], result.Issues.Select(static issue => issue.Code));
        Assert.Contains("missing-region", result.Issues[0].Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies metadata requirements must be declared by the selected canonical map.</summary>
    [Fact]
    public void ValidateRejectsMissingRequiredMetadataStructure()
    {
        CompositionProfileMapAdmissionResult result = Admit(Profile(structureIds: ["firmware-config", "missing-structure"]));

        Assert.False(result.IsAdmitted);
        Assert.Equal(
            ["profile.v2.map.required-metadata-structure-missing"],
            result.Issues.Select(static issue => issue.Code));
        Assert.Contains("missing-structure", result.Issues[0].Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a required capability without one selected family binding blocks admission.</summary>
    [Fact]
    public void ValidateRejectsMissingRequiredCapabilities()
    {
        CompositionProfileMapAdmissionResult result = Admit(Profile(capabilityIds: ["ab-code", "crc32"]));

        Assert.False(result.IsAdmitted);
        Assert.Equal(
            [
                "profile.v2.map.required-capability-missing",
                "profile.v2.map.required-capability-missing",
            ],
            result.Issues.Select(static issue => issue.Code));
        Assert.Contains("ab-code", result.Issues[0].Message, StringComparison.Ordinal);
        Assert.Contains("crc32", result.Issues[1].Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies declaration order cannot change the ordered diagnostic output.</summary>
    [Fact]
    public void ValidateOrdersAdmissionIssuesIndependentlyOfRequirementDeclarationOrder()
    {
        CompositionProfileMapAdmissionResult first = Admit(
            Profile(
                regionIds: ["z-region", "dp-code", "a-region"],
                structureIds: ["z-structure", "firmware-config", "a-structure"],
                capabilityIds: ["z-capability", "a-capability"]));
        CompositionProfileMapAdmissionResult second = Admit(
            Profile(
                regionIds: ["a-region", "z-region", "dp-code"],
                structureIds: ["a-structure", "z-structure", "firmware-config"],
                capabilityIds: ["a-capability", "z-capability"]));

        Assert.Equal(
            first.Issues.Select(static issue => issue.Code),
            second.Issues.Select(static issue => issue.Code));
        Assert.Equal(
            first.Issues.Select(static issue => issue.Message),
            second.Issues.Select(static issue => issue.Message));
        Assert.Equal(
            [
                "profile.v2.map.required-capability-missing",
                "profile.v2.map.required-capability-missing",
                "profile.v2.map.required-metadata-structure-missing",
                "profile.v2.map.required-metadata-structure-missing",
                "profile.v2.map.required-region-missing",
                "profile.v2.map.required-region-missing",
            ],
            first.Issues.Select(static issue => issue.Code));
    }

    /// <summary>Verifies admission reads the selected map's canonical values and not direct-source map identifiers.</summary>
    [Fact]
    public void ValidateAcceptsAliasedEffectiveCanonicalFacts()
    {
        ResolutionContext context = ResolveContext(useAliasedFacts: true);

        CompositionProfileMapAdmissionResult result = Admit(Profile(), context);

        Assert.True(result.IsAdmitted);
        Assert.Contains(context.ResolvedMap.FactProvenance, static provenance => provenance.AliasChain.Count != 0);
    }

    private static CompositionProfileMapAdmissionResult Admit(CompositionProfileDefinition profile)
    {
        return Admit(profile, ResolveContext());
    }

    private static CompositionProfileMapAdmissionResult Admit(
        CompositionProfileDefinition profile,
        ResolutionContext context)
    {
        return CompositionProfileMapAdmissionValidator.Validate(profile, context.Family, context.ResolvedMap);
    }

    private static CompositionProfileDefinition Profile(
        string familyId = "synthetic-family",
        string familyVersion = "1.0.0",
        string familyContentHash = FamilyHash,
        IEnumerable<string>? mapIds = null,
        IEnumerable<string>? regionIds = null,
        IEnumerable<string>? structureIds = null,
        IEnumerable<string>? capabilityIds = null)
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        return CompositionProfileV2DefinitionTestData.Create(parts with
        {
            MapBinding = new CompositionProfileMapBinding(
                familyId,
                familyVersion,
                familyContentHash,
                mapIds ?? ["standard-map"],
                regionIds ?? ["dp-code"],
                structureIds ?? ["firmware-config"],
                capabilityIds ?? []),
        });
    }

    private static ResolutionContext ResolveContext(
        bool useAliasedFacts = false,
        string familyContentHash = FamilyHash)
    {
        FirmwareMetadataSet metadata = new(
            "metadata",
            [new FirmwareMetadataStructure(
                "firmware-config",
                "tp-firmware",
                1,
                new FirmwareAbsoluteRangeLocator(
                    new FirmwareAddressedRange("flash", new ByteRange(0, 1)),
                    "dp-code"),
                [new FirmwareMetadataField(
                    "pid",
                    0,
                    1,
                    FirmwareMetadataEncoding.UnsignedInteger,
                    FirmwareMetadataByteOrder.LittleEndian)],
                [])],
            ["metadata-evidence"]);
        FirmwareMapApplicability applicability = new(
            ["NT-SYNTHETIC"],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            16);
        FirmwareRegionSet regions = new(
            "physical",
            "flash",
            [
                new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 16),
                    FirmwareWriteConstraint.Forbidden),
                new FirmwareRegion(
                    "dp-code",
                    "root",
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Code,
                    new ByteRange(0, 1),
                    FirmwareWriteConstraint.Forbidden),
                new FirmwareRegion(
                    "unmapped",
                    "root",
                    FirmwareRegionOwner.Unknown,
                    FirmwareRegionKind.Unmapped,
                    new ByteRange(1, 15),
                    FirmwareWriteConstraint.Forbidden),
            ],
            ["region-evidence"]);
        FirmwareImageMap map = useAliasedFacts
            ? AliasedMap(applicability, regions, metadata)
            : FirmwareImageMapTestFactory.CreateDirect(
                "standard-map",
                "flash",
                applicability,
                FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
                [regions],
                [metadata],
                ["map-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            familyContentHash,
            [map],
            [metadata]);
        FirmwareMapResolutionResult result = definition.ResolveMap(new FirmwareMapResolutionInputs(
            "NT-SYNTHETIC",
            "standard",
            16,
            requestedTopology: null,
            [new FirmwareArtifactPayload("tp-firmware", [0x01])]));

        return new ResolutionContext(
            definition,
            Assert.IsType<ResolvedFirmwareImageMap>(result.ResolvedMap));
    }

    private static FirmwareImageMap AliasedMap(
        FirmwareMapApplicability applicability,
        FirmwareRegionSet regions,
        FirmwareMetadataSet metadata)
    {
        return new FirmwareImageMap(
            "standard-map",
            "flash",
            applicability,
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [AliasedBinding(regions, applicability)],
            [AliasedBinding(metadata, applicability)],
            ["map-evidence"]);
    }

    private static FirmwareMapFactBinding<TFact> AliasedBinding<TFact>(
        TFact value,
        FirmwareMapApplicability applicability)
        where TFact : class, IFirmwareMapFact
    {
        var target = new FirmwareMapFactKey(
            "NT-SYNTHETIC",
            "standard-map",
            value.FactKind,
            "effective-" + value.CanonicalFactId);
        var source = new FirmwareMapFactKey(
            "NT-SOURCE",
            "source-map",
            value.FactKind,
            value.CanonicalFactId);
        var factApplicability = FirmwareFactApplicability.FromMap(applicability);
        var hop = new FirmwareFactAliasHop(
            "alias-" + value.CanonicalFactId,
            target,
            source,
            factApplicability,
            "Synthetic canonical fact inheritance.",
            ["alias-evidence"]);
        var provenance = new FirmwareFactProvenance(target, source, [hop], value.EvidenceRefs);
        return new FirmwareMapFactBinding<TFact>(
            target,
            source,
            value.CanonicalFactId,
            value,
            factApplicability,
            provenance);
    }

    private sealed record ResolutionContext(
        FirmwareFamilyResolutionDefinition Family,
        ResolvedFirmwareImageMap ResolvedMap);
}
