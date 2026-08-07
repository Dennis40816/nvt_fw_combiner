using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Locks deleted compatibility projections and raw admission vocabulary out of the public Domain API.</summary>
    [Fact]
    public void RemovedCompatibilitySurfaceStaysOutsidePublicDomainApi()
    {
        (Type Owner, string Property)[] removedProperties =
        [
            (typeof(CompiledComposition), "IntegrityFingerprint"),
            (typeof(ChangedRangeVerdict), "ObservedRanges"),
            (typeof(CompiledOutputNamingRequirement), "RequiredTokenIds"),
            (typeof(FirmwareImageMap), "MetadataSetIds"),
            (typeof(FirmwareArtifactPayload), "Sha256"),
            (typeof(FirmwareMetadataByteAssertion), "IsExact"),
            (typeof(FirmwareMetadataStructure), "Assertions"),
        ];
        Assert.All(
            removedProperties,
            entry => Assert.Null(entry.Owner.GetProperty(entry.Property)));

        string[] removedTypes =
        [
            "NvtFwCombiner.Domain.Composition.RangeSet",
            "NvtFwCombiner.Domain.Firmware.FirmwareRelativeRegion",
        ];
        Assert.All(
            removedTypes,
            typeName => Assert.Null(typeof(CompiledComposition).Assembly.GetType(typeName)));

        Assert.True(typeof(FirmwareFactApplicability).IsSealed);
        Assert.True(typeof(FirmwareMapApplicability).IsSealed);
        Assert.False(typeof(FirmwareFactApplicability).IsAssignableFrom(typeof(FirmwareMapApplicability)));
        Assert.Null(typeof(FirmwareMapApplicability).GetMethod("Evaluate"));
        Assert.Null(typeof(FirmwareMetadataPredicate).GetMethod("Evaluate"));

        Type[] internalTypes =
        [
            typeof(CompiledCapabilityAdmission),
            typeof(FirmwareCapabilityFact),
            typeof(FirmwareCapabilityState),
            typeof(FirmwareFamilyRelationship),
            typeof(FirmwareSharedFactReference),
            typeof(FirmwareMapApplicabilityEvaluation),
            typeof(FirmwareMapResolutionPendingKind),
            typeof(FirmwareMapResolutionPendingRequirement),
            typeof(FirmwareMetadataPredicateOutcome),
            typeof(FirmwareRegionTemplate),
            typeof(FirmwareRegionInstance),
        ];
        Assert.All(internalTypes, type => Assert.True(type.IsNotPublic, type.FullName));
    }

    /// <summary>Verifies equal detached region values cannot impersonate the selected map's canonical instances.</summary>
    [Fact]
    public void V2RegionAccessContractRejectsDetachedEquivalentRegions()
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap(
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
        FirmwareRegion canonicalRoot = resolvedMap.ImageMap.Regions.Single(static region => region.RegionId == "root");
        var detachedEquivalent = new FirmwareRegion(
            canonicalRoot.RegionId,
            canonicalRoot.ParentRegionId,
            canonicalRoot.Owner,
            canonicalRoot.Kind,
            canonicalRoot.Range,
            canonicalRoot.WriteConstraint,
            canonicalRoot.Alignment);

        _ = Assert.Throws<ArgumentException>(() => CreateV2(
            resolvedMap: resolvedMap,
            regionAccessContract: CreateRegionAccessContract(
                RegionAccessKind.ReadOnly,
                "Detached values are not canonical references.",
                detachedEquivalent)));
    }
}
