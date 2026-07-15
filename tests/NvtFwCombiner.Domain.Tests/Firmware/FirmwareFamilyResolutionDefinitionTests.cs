using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests the normalized family boundary used before firmware-map resolution.</summary>
public sealed class FirmwareFamilyResolutionDefinitionTests
{
    private const string FamilyHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>Verifies family facts, candidate structures, and bindings are immutable and canonical.</summary>
    [Fact]
    public void ConstructorCreatesCanonicalImmutableDefinition()
    {
        FirmwareMetadataSet setZ = MetadataSet(
            "metadata-z",
            Structure("version", artifactBindingId: "tp-firmware"));
        FirmwareMetadataSet setA = MetadataSet(
            "metadata-a",
            Structure("config", artifactBindingId: "display-firmware"));
        FirmwareImageMap[] maps =
        [
            Map("map-z", metadataSets: [setZ]),
            Map("map-a", metadataSets: [setA]),
        ];
        FirmwareMetadataSet[] sets = [setZ, setA];

        FirmwareFamilyResolutionDefinition definition = Definition(maps, sets);
        maps[0] = Map("changed");
        sets[0] = MetadataSet("changed", Structure("changed"));

        Assert.Equal("synthetic-family", definition.FamilyId);
        Assert.Equal("1.0.0", definition.FamilyVersion);
        Assert.Equal(FamilyHash, definition.FamilyContentHash);
        Assert.Equal(["map-a", "map-z"], definition.ImageMaps.Select(static map => map.MapId));
        Assert.Equal(
            ["metadata-a", "metadata-z"],
            definition.MetadataSets.Select(static set => set.MetadataSetId));
        Assert.Equal(["display-firmware", "tp-firmware"], definition.RequiredArtifactBindingIds);
        Assert.Equal(["config"],
            definition.GetStructuresForMap("map-a").Select(static structure => structure.StructureId));

        IList<FirmwareImageMap> mapView = Assert.IsType<IList<FirmwareImageMap>>(
            definition.ImageMaps,
            exactMatch: false);
        IList<FirmwareMetadataSet> setView = Assert.IsType<IList<FirmwareMetadataSet>>(
            definition.MetadataSets,
            exactMatch: false);
        IList<string> bindingView = Assert.IsType<IList<string>>(
            definition.RequiredArtifactBindingIds,
            exactMatch: false);
        IList<FirmwareMetadataStructure> structureView =
            Assert.IsType<IList<FirmwareMetadataStructure>>(
                definition.GetStructuresForMap("map-a"),
                exactMatch: false);
        Assert.True(mapView.IsReadOnly);
        Assert.True(setView.IsReadOnly);
        Assert.True(bindingView.IsReadOnly);
        Assert.True(structureView.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => mapView[0] = Map("changed"));
        _ = Assert.Throws<NotSupportedException>(() =>
            setView[0] = MetadataSet("changed", Structure("changed")));
        _ = Assert.Throws<NotSupportedException>(() => bindingView[0] = "changed");
        _ = Assert.Throws<NotSupportedException>(() => structureView[0] = Structure("changed"));
    }

    /// <summary>Verifies structure and field lookup never bypasses candidate metadata selection.</summary>
    [Fact]
    public void CandidateLookupsRemainMapScoped()
    {
        FirmwareMetadataSet setA = MetadataSet("metadata-a", Structure("config-a"));
        FirmwareMetadataSet setB = MetadataSet("metadata-b", Structure("config-b"));
        FirmwareFamilyResolutionDefinition definition = Definition(
            [
                Map("map-a", metadataSets: [setA]),
                Map("map-b", metadataSets: [setB]),
            ],
            [setA, setB]);

        Assert.True(definition.TryResolveStructure("map-a", "config-a", out FirmwareMetadataStructure? structure));
        Assert.Equal("config-a", structure?.StructureId);
        Assert.False(definition.TryResolveStructure("map-a", "config-b", out _));
        Assert.False(definition.TryResolveStructure("missing-map", "config-a", out _));
        Assert.True(definition.TryResolveField("map-a", "config-a", "value", out FirmwareMetadataField? field));
        Assert.Equal("value", field?.FieldId);
        Assert.False(definition.TryResolveField("map-a", "config-a", "missing", out _));
        Assert.False(definition.TryResolveField("map-a", "config-b", "value", out _));
        _ = Assert.Throws<KeyNotFoundException>(() => definition.GetStructuresForMap("missing-map"));
        _ = Assert.Throws<ArgumentException>(() => definition.GetStructuresForMap(" "));
        _ = Assert.Throws<ArgumentException>(() => definition.TryResolveStructure("map-a", " ", out _));
        _ = Assert.Throws<ArgumentException>(() =>
            definition.TryResolveField("map-a", "config-a", " ", out _));
    }

    /// <summary>Verifies category applicability does not imply an undocumented metadata dependency.</summary>
    [Fact]
    public void ConstructorAcceptsMetadataIndependentMaps()
    {
        FirmwareFamilyResolutionDefinition definition = Definition(
            [Map("plain-map", commonFirmwareCategoryIds: ["common"])],
            []);

        Assert.Empty(definition.MetadataSets);
        Assert.Empty(definition.RequiredArtifactBindingIds);
        Assert.Empty(definition.GetStructuresForMap("plain-map"));
    }

    /// <summary>Verifies map-bound capability evidence remains immutable and separate from map selection facts.</summary>
    [Fact]
    public void ConstructorStoresCapabilityBindingsWithoutChangingMapFacts()
    {
        FirmwareImageMap map = Map("plain-map");
        FirmwareCapabilityFact value = new(
            "ab-code-evidence",
            "ab-code",
            FirmwareCapabilityState.ConfirmedPresent,
            "synthetic evidence",
            ["capability-evidence"]);
        FirmwareMapFactKey key = new("NT00001", "plain-map", FirmwareFactKind.Capability, "ab-code-evidence");
        var binding = new FirmwareMapFactBinding<FirmwareCapabilityFact>(
            key,
            key,
            value.CanonicalFactId,
            value,
            new FirmwareFactApplicability(
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                16),
            new FirmwareFactProvenance(key, key, [], value.EvidenceRefs));

        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            [map],
            [],
            [binding]);

        FirmwareMapFactBinding<FirmwareCapabilityFact> stored = Assert.Single(definition.CapabilityBindings);
        Assert.Same(value, stored.Value);
        Assert.Equal(["plain-map"], definition.ImageMaps.Select(static item => item.MapId));
        Assert.Empty(definition.RequiredArtifactBindingIds);
        Assert.Equal(
            [FirmwareFactKind.RegionSet, FirmwareFactKind.Capability],
            definition.EnumerateFactProvenance().Select(static provenance => provenance.EffectiveKey.FactKind));
        IList<FirmwareMapFactBinding<FirmwareCapabilityFact>> view = Assert.IsType<IList<FirmwareMapFactBinding<FirmwareCapabilityFact>>>(
            definition.CapabilityBindings,
            exactMatch: false);
        Assert.True(view.IsReadOnly);
        Assert.DoesNotContain(
            typeof(FirmwareFamilyResolutionDefinition).GetConstructors(),
            constructor => constructor.GetParameters().Length == 6);
    }

    /// <summary>Verifies one map canonically orders structures and deduplicates shared bindings.</summary>
    [Fact]
    public void ConstructorCanonicalizesSelectedStructuresAndSharedBindings()
    {
        FirmwareMetadataSet setZ = MetadataSet(
            "metadata-z",
            Structure("zeta", artifactBindingId: "shared-firmware"));
        FirmwareMetadataSet setA = MetadataSet(
            "metadata-a",
            Structure("alpha", artifactBindingId: "shared-firmware"));

        FirmwareFamilyResolutionDefinition definition = Definition(
            [Map("map", metadataSets: [setZ, setA])],
            [setZ, setA]);

        Assert.Equal(
            ["alpha", "zeta"],
            definition.GetStructuresForMap("map").Select(static structure => structure.StructureId));
        Assert.Equal(["shared-firmware"], definition.RequiredArtifactBindingIds);
    }

    /// <summary>Verifies both relative locator forms retain valid boundary declarations.</summary>
    [Fact]
    public void ConstructorAcceptsValidRelativeLocators()
    {
        FirmwareMetadataSet regionRelative = MetadataSet(
            "region-relative-metadata",
            Structure(
                "region-relative",
                locator: new FirmwareRegionRelativeLocator("metadata", 4, "metadata")));
        FirmwareMetadataSet markerRelative = MetadataSet(
            "marker-relative-metadata",
            Structure(
                "marker-relative",
                locator: Marker("flash", 4, 4, -4, "metadata")));

        FirmwareFamilyResolutionDefinition definition = Definition(
            [
                Map("region-map", metadataSets: [regionRelative]),
                Map("marker-map", metadataSets: [markerRelative]),
            ],
            [regionRelative, markerRelative]);

        _ = Assert.IsType<FirmwareRegionRelativeLocator>(
            Assert.Single(definition.GetStructuresForMap("region-map")).Locator);
        FirmwareMarkerRelativeLocator marker = Assert.IsType<FirmwareMarkerRelativeLocator>(
            Assert.Single(definition.GetStructuresForMap("marker-map")).Locator);
        Assert.Equal(-4, marker.ResultOffset);
    }

    /// <summary>Verifies one canonical metadata set may be selected by multiple compatible maps.</summary>
    [Fact]
    public void ConstructorAcceptsSharedCompatibleMetadataSet()
    {
        FirmwareMetadataSet metadataSet = MetadataSet("metadata", Structure("config"));

        FirmwareFamilyResolutionDefinition definition = Definition(
            [
                Map("map-a", metadataSets: [metadataSet]),
                Map("map-b", metadataSets: [metadataSet]),
            ],
            [metadataSet]);

        Assert.Equal("config", Assert.Single(definition.GetStructuresForMap("map-a")).StructureId);
        Assert.Equal("config", Assert.Single(definition.GetStructuresForMap("map-b")).StructureId);
    }

    /// <summary>Verifies family identity and collection boundaries fail closed.</summary>
    [Fact]
    public void ConstructorRejectsInvalidIdentityAndCollections()
    {
        FirmwareMetadataSet metadataSet = MetadataSet("metadata", Structure("config"));
        FirmwareImageMap map = Map("map", metadataSets: [metadataSet]);

        _ = Assert.Throws<ArgumentException>(() => Definition([map], [metadataSet], familyId: " "));
        _ = Assert.Throws<ArgumentException>(() => Definition([map], [metadataSet], familyVersion: " "));
        _ = Assert.Throws<ArgumentException>(() => Definition(
            [map],
            [metadataSet],
            familyContentHash: "A" + FamilyHash[1..]));
        _ = Assert.Throws<ArgumentException>(() => Definition(
            [map],
            [metadataSet],
            familyContentHash: FamilyHash[..^1]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            null!,
            [metadataSet]));
        _ = Assert.Throws<ArgumentException>(() => Definition([], []));
        _ = Assert.Throws<ArgumentException>(() => Definition([null!], []));
        _ = Assert.Throws<ArgumentException>(() => Definition([Map("same"), Map("same")], []));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            [Map("plain-map")],
            null!));
        _ = Assert.Throws<ArgumentException>(() => Definition([Map("plain-map")], [null!]));
        FirmwareMetadataSet sameA = MetadataSet("same", Structure("a"));
        FirmwareMetadataSet sameB = MetadataSet("same", Structure("b"));
        _ = Assert.Throws<ArgumentException>(() => Definition(
            [Map("map", metadataSets: [sameA])],
            [sameA, sameB]));
    }

    /// <summary>Verifies map/set and family-global structure cross-references are exact.</summary>
    [Fact]
    public void ConstructorRejectsInvalidMetadataCrossReferences()
    {
        FirmwareMetadataSet missing = MetadataSet("missing", Structure("missing"));
        _ = Assert.Throws<ArgumentException>(() => Definition([Map("map", metadataSets: [missing])], []));
        _ = Assert.Throws<ArgumentException>(() => Definition(
            [Map("plain-map")],
            [MetadataSet("orphan", Structure("config"))]));
        FirmwareMetadataSet metadataA = MetadataSet("metadata-a", Structure("same"));
        FirmwareMetadataSet metadataB = MetadataSet("metadata-b", Structure("same"));
        _ = Assert.Throws<ArgumentException>(() => Definition(
            [Map("map", metadataSets: [metadataA, metadataB])],
            [metadataA, metadataB]));
    }

    /// <summary>Verifies predicates resolve only through the map-selected structure and field.</summary>
    [Fact]
    public void ConstructorRejectsUnselectedPredicateStructureAndUnknownField()
    {
        FirmwareMetadataSet setA = MetadataSet("metadata-a", Structure("config-a"));
        FirmwareMetadataSet setB = MetadataSet("metadata-b", Structure("config-b"));
        FirmwareMetadataPredicate unselected = Predicate("config-b", "value", Unsigned(1));

        _ = Assert.Throws<ArgumentException>(() => Definition(
            [
                Map("map-a", metadataSets: [setA], predicates: [unselected]),
                Map("map-b", metadataSets: [setB]),
            ],
            [setA, setB]));

        FirmwareMetadataPredicate missingField = Predicate("config-a", "missing", Unsigned(1));
        _ = Assert.Throws<ArgumentException>(() => Definition(
            [Map("map-a", metadataSets: [setA], predicates: [missingField])],
            [setA]));
    }

    /// <summary>Verifies every predicate value is exactly representable by its declared field.</summary>
    [Fact]
    public void ConstructorRejectsUnrepresentablePredicateValues()
    {
        (FirmwareMetadataField Field, FirmwareMetadataValue Value)[] invalidCases =
        [
            (Field(FirmwareMetadataEncoding.Bytes, widthBytes: 2), FirmwareMetadataValue.FromBytes([1])),
            (Field(FirmwareMetadataEncoding.PrintableAscii, widthBytes: 2), FirmwareMetadataValue.FromText("A")),
            (Field(FirmwareMetadataEncoding.PrintableAscii, widthBytes: 2), FirmwareMetadataValue.FromText("A\n")),
            (UnsignedField(), FirmwareMetadataValue.FromSignedInteger(1)),
            (UnsignedField(), Unsigned(256)),
            (UnsignedField(new FirmwareMetadataBitSlice(0, 4)), Unsigned(16)),
            (SignedField(), FirmwareMetadataValue.FromSignedInteger(128)),
        ];

        foreach ((FirmwareMetadataField field, FirmwareMetadataValue value) in invalidCases)
        {
            FirmwareMetadataStructure structure = Structure("config", fields: [field]);
            FirmwareMetadataSet metadataSet = MetadataSet("metadata", structure);
            FirmwareMetadataPredicate predicate = Predicate("config", "value", value);

            _ = Assert.Throws<ArgumentException>(() => Definition(
                [Map("map", metadataSets: [metadataSet], predicates: [predicate])],
                [metadataSet]));
        }

        FirmwareMetadataStructure oneOfStructure = Structure("one-of", fields: [UnsignedField()]);
        FirmwareMetadataSet oneOfSet = MetadataSet("one-of-metadata", oneOfStructure);
        var oneOf = new FirmwareMetadataPredicate(
            "one-of",
            "value",
            FirmwareMetadataPredicateOperator.OneOf,
            [Unsigned(1), Unsigned(256)]);
        _ = Assert.Throws<ArgumentException>(() => Definition(
            [Map("one-of-map", metadataSets: [oneOfSet], predicates: [oneOf])],
            [oneOfSet]));
    }

    /// <summary>Verifies absolute locator address space, capacity, and allowed-region boundaries.</summary>
    [Fact]
    public void ConstructorRejectsInvalidAbsoluteLocators()
    {
        FirmwareMetadataLocator[] invalidLocators =
        [
            Absolute("other", 0, 4, "metadata"),
            Absolute("flash", 14, 4, "root"),
            Absolute("flash", 0, 4, "missing"),
            Absolute("flash", 8, 4, "metadata"),
        ];

        foreach (FirmwareMetadataLocator locator in invalidLocators)
        {
            AssertInvalidLocator(locator);
        }
    }

    /// <summary>Verifies region-relative locators remain inside the base, allowed region, and map.</summary>
    [Fact]
    public void ConstructorRejectsInvalidRegionRelativeLocators()
    {
        FirmwareMetadataLocator[] invalidLocators =
        [
            new FirmwareRegionRelativeLocator("missing", 0, "root"),
            new FirmwareRegionRelativeLocator("metadata", 6, "root"),
            new FirmwareRegionRelativeLocator("root", 8, "metadata"),
            new FirmwareRegionRelativeLocator("root", 0, "missing"),
        ];

        foreach (FirmwareMetadataLocator locator in invalidLocators)
        {
            AssertInvalidLocator(locator);
        }

        FirmwareMetadataLocator overflow = new FirmwareRegionRelativeLocator(
            "other",
            long.MaxValue - 4,
            "root");
        _ = Assert.Throws<OverflowException>(() => CreateDefinitionForLocator(overflow));
    }

    /// <summary>Verifies marker locators keep search ranges map-bounded and arithmetic checked.</summary>
    [Fact]
    public void ConstructorRejectsInvalidMarkerRelativeLocators()
    {
        FirmwareMetadataLocator[] invalidLocators =
        [
            Marker("other", 0, 4, 0, "root"),
            Marker("flash", 15, 2, 0, "root"),
            Marker("flash", 0, 4, 0, "missing"),
        ];

        foreach (FirmwareMetadataLocator locator in invalidLocators)
        {
            AssertInvalidLocator(locator);
        }

        FirmwareMetadataLocator overflow = Marker(
            "flash",
            8,
            4,
            long.MaxValue - 4,
            "root");
        _ = Assert.Throws<OverflowException>(() => CreateDefinitionForLocator(overflow));
    }

    /// <summary>Verifies shared metadata geometry is checked against every referencing map.</summary>
    [Fact]
    public void ConstructorRejectsSharedStructureInvalidForOneMap()
    {
        FirmwareMetadataStructure structure = Structure(
            "config",
            locator: Absolute("flash", 4, 4, "metadata"));
        FirmwareMetadataSet metadataSet = MetadataSet("metadata", structure);

        _ = Assert.Throws<ArgumentException>(() => Definition(
            [
                Map("wide-map", metadataSets: [metadataSet]),
                Map(
                    "narrow-map",
                    metadataSets: [metadataSet],
                    regionSets: [RegionSet(metadataLength: 4)]),
            ],
            [metadataSet]));
    }

    private static FirmwareFamilyResolutionDefinition Definition(
        IEnumerable<FirmwareImageMap> maps,
        IEnumerable<FirmwareMetadataSet> metadataSets,
        string familyId = "synthetic-family",
        string familyVersion = "1.0.0",
        string familyContentHash = FamilyHash)
    {
        return new FirmwareFamilyResolutionDefinition(
            familyId,
            familyVersion,
            familyContentHash,
            maps,
            metadataSets);
    }

    private static FirmwareImageMap Map(
        string mapId,
        IEnumerable<FirmwareMetadataSet>? metadataSets = null,
        IEnumerable<FirmwareMetadataPredicate>? predicates = null,
        IEnumerable<string>? commonFirmwareCategoryIds = null,
        IEnumerable<FirmwareRegionSet>? regionSets = null)
    {
        return FirmwareImageMap.CreateDirect(
            mapId,
            "flash",
            new FirmwareMapApplicability(
                ["NT00001"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                16,
                commonFirmwareCategoryIds,
                predicates),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            regionSets ?? [RegionSet()],
            metadataSets ?? [],
            ["map-evidence"]);
    }

    private static FirmwareRegionSet RegionSet(int metadataLength = 8)
    {
        return new FirmwareRegionSet(
            "physical",
            "flash",
            [
                Region("root", 0, 16, FirmwareRegionOwner.System, FirmwareRegionKind.Image),
                Region("metadata", 0, metadataLength),
                Region(
                    "other",
                    metadataLength,
                    16 - metadataLength,
                    FirmwareRegionOwner.Reserved,
                    FirmwareRegionKind.Reserved),
            ],
            ["region-evidence"]);
    }

    private static FirmwareRegion Region(
        string regionId,
        long start,
        long length,
        FirmwareRegionOwner owner = FirmwareRegionOwner.System,
        FirmwareRegionKind kind = FirmwareRegionKind.Data)
    {
        return new FirmwareRegion(
            regionId,
            regionId == "root" ? null : "root",
            owner,
            kind,
            new ByteRange(start, length),
            FirmwareWriteConstraint.Forbidden);
    }

    private static FirmwareMetadataSet MetadataSet(
        string metadataSetId,
        params FirmwareMetadataStructure[] structures)
    {
        return new FirmwareMetadataSet(metadataSetId, structures, ["metadata-evidence"]);
    }

    private static FirmwareMetadataStructure Structure(
        string structureId,
        string artifactBindingId = "tp-firmware",
        FirmwareMetadataLocator? locator = null,
        IEnumerable<FirmwareMetadataField>? fields = null)
    {
        FirmwareMetadataLocator selectedLocator = locator ?? Absolute("flash", 0, 4, "metadata");
        IEnumerable<FirmwareMetadataByteAssertion> assertions = selectedLocator is FirmwareMarkerRelativeLocator
            ? [FirmwareMetadataByteAssertion.Exact(0, [0])]
            : [];
        return new FirmwareMetadataStructure(
            structureId,
            artifactBindingId,
            4,
            selectedLocator,
            fields ?? [UnsignedField()],
            assertions);
    }

    private static FirmwareMetadataPredicate Predicate(
        string structureId,
        string fieldId,
        FirmwareMetadataValue expected)
    {
        return new FirmwareMetadataPredicate(
            structureId,
            fieldId,
            FirmwareMetadataPredicateOperator.Equal,
            [expected]);
    }

    private static FirmwareMetadataField Field(
        FirmwareMetadataEncoding encoding,
        int widthBytes)
    {
        return new FirmwareMetadataField("value", 0, widthBytes, encoding);
    }

    private static FirmwareMetadataField UnsignedField(FirmwareMetadataBitSlice? bitSlice = null)
    {
        return new FirmwareMetadataField(
            "value",
            0,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            bitSlice);
    }

    private static FirmwareMetadataField SignedField()
    {
        return new FirmwareMetadataField(
            "value",
            0,
            1,
            FirmwareMetadataEncoding.SignedInteger,
            FirmwareMetadataByteOrder.LittleEndian);
    }

    private static FirmwareMetadataValue Unsigned(ulong value)
    {
        return FirmwareMetadataValue.FromUnsignedInteger(value);
    }

    private static FirmwareAbsoluteRangeLocator Absolute(
        string addressSpaceId,
        long start,
        long length,
        string allowedResultRegionId)
    {
        return new FirmwareAbsoluteRangeLocator(
            new FirmwareAddressedRange(addressSpaceId, new ByteRange(start, length)),
            allowedResultRegionId);
    }

    private static FirmwareMarkerRelativeLocator Marker(
        string addressSpaceId,
        long start,
        long length,
        long resultOffset,
        string allowedResultRegionId)
    {
        return new FirmwareMarkerRelativeLocator(
            new FirmwareAddressedRange(addressSpaceId, new ByteRange(start, length)),
            [0xAA],
            new FirmwareUniqueMarkerSelection(),
            resultOffset,
            allowedResultRegionId);
    }

    private static void AssertInvalidLocator(FirmwareMetadataLocator locator)
    {
        _ = Assert.Throws<ArgumentException>(() => CreateDefinitionForLocator(locator));
    }

    private static FirmwareFamilyResolutionDefinition CreateDefinitionForLocator(
        FirmwareMetadataLocator locator)
    {
        FirmwareMetadataSet metadataSet = MetadataSet("metadata", Structure("config", locator: locator));
        return Definition(
            [Map("map", metadataSets: [metadataSet])],
            [metadataSet]);
    }
}
