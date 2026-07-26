using System.Reflection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests candidate-scoped locator evaluation against immutable artifact payloads.</summary>
public sealed class FirmwareMetadataStructureResolutionTests
{
    private const string FamilyHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    /// <summary>Verifies an absolute locator produces one typed, payload-free resolved outcome.</summary>
    [Fact]
    public void AbsoluteLocatorResolvesTypedStructure()
    {
        FirmwareMetadataStructure structure = Structure(
            Absolute(4, 2, "allowed"),
            fields: [BytesField("value", 0, 2)]);
        byte[] source = new byte[32];
        source[4] = 0x00;
        source[5] = 0x01;
        var artifact = new FirmwareArtifactPayload("tp-firmware", source);
        FirmwareFamilyResolutionDefinition definition = Definition(structure);

        FirmwareMetadataStructureResolution result = definition.ResolveMetadataStructure(
            "map",
            "config",
            Inputs(artifact));
        source[4] = 0xFF;

        Assert.Equal(FirmwareMetadataStructureResolutionStatus.Resolved, result.Status);
        Assert.Null(result.Failure);
        FirmwareResolvedMetadataStructure resolved = Assert.IsType<FirmwareResolvedMetadataStructure>(
            result.Resolved);
        Assert.Equal("map", resolved.MapId);
        Assert.Equal(artifact.Identity, resolved.ArtifactIdentity);
        Assert.Equal(FirmwareMetadataLocatorKind.AbsoluteRange, resolved.LocatorOutcome.LocatorKind);
        Assert.Equal(new ByteRange(4, 2), resolved.LocatorOutcome.ResolvedRange.Range);
        Assert.Null(resolved.LocatorOutcome.MarkerMatchCount);
        Assert.Null(resolved.LocatorOutcome.SelectedMarkerStart);
        Assert.Equal("0001", Assert.Single(resolved.DecodedStructure.Facts).Value.BytesValue?.Hex);
    }

    /// <summary>Verifies a region-relative result may end at its base-region boundary.</summary>
    [Fact]
    public void RegionRelativeLocatorResolvesAtEndExclusiveBoundary()
    {
        FirmwareMetadataStructure structure = Structure(
            new FirmwareRegionRelativeLocator("other", 14, "other"),
            fields: [BytesField("tail", 0, 2)]);
        byte[] source = new byte[32];
        source[30] = 0x12;
        source[31] = 0x34;
        FirmwareFamilyResolutionDefinition definition = Definition(structure);

        FirmwareMetadataStructureResolution result = definition.ResolveMetadataStructure(
            "map",
            "config",
            Inputs(new FirmwareArtifactPayload("tp-firmware", source)));

        FirmwareResolvedMetadataStructure resolved = Assert.IsType<FirmwareResolvedMetadataStructure>(
            result.Resolved);
        Assert.Equal(FirmwareMetadataLocatorKind.RegionRelative, resolved.LocatorOutcome.LocatorKind);
        Assert.Equal(new ByteRange(30, 2), resolved.LocatorOutcome.ResolvedRange.Range);
        Assert.Equal("1234", Assert.Single(resolved.DecodedStructure.Facts).Value.BytesValue?.Hex);
    }

    /// <summary>Verifies a unique marker may locate a structure through a negative result offset.</summary>
    [Fact]
    public void UniqueMarkerResolvesNegativeOffsetWithEvidence()
    {
        FirmwareMetadataStructure structure = Structure(
            Marker(
                searchStart: 8,
                searchLength: 8,
                markerBytes: [0x00, 0x4E, 0x56, 0x54],
                selection: new FirmwareUniqueMarkerSelection(),
                resultOffset: -2,
                allowedResultRegionId: "allowed"),
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [0xAA, 0xBB])]);
        byte[] source = new byte[32];
        source[10] = 0xAA;
        source[11] = 0xBB;
        source[12] = 0x00;
        source[13] = 0x4E;
        source[14] = 0x56;
        source[15] = 0x54;
        FirmwareFamilyResolutionDefinition definition = Definition(structure);

        FirmwareMetadataStructureResolution result = definition.ResolveMetadataStructure(
            "map",
            "config",
            Inputs(new FirmwareArtifactPayload("tp-firmware", source)));

        FirmwareMetadataLocatorOutcome outcome = Assert.IsType<FirmwareMetadataLocatorOutcome>(
            result.Resolved?.LocatorOutcome);
        Assert.Equal(new ByteRange(10, 2), outcome.ResolvedRange.Range);
        Assert.Equal(1, outcome.MarkerMatchCount);
        Assert.Equal(12, outcome.SelectedMarkerStart);
    }

    /// <summary>Verifies terminal selection counts overlapping matches and chooses the declared end.</summary>
    [Theory]
    [InlineData(FirmwareMarkerTerminal.LowestAddress, 4L, 8L)]
    [InlineData(FirmwareMarkerTerminal.HighestAddress, 5L, 9L)]
    public void TerminalMarkerCountsOverlappingMatches(
        FirmwareMarkerTerminal terminal,
        long expectedMarkerStart,
        long expectedResultStart)
    {
        FirmwareMetadataStructure structure = Structure(
            Marker(
                searchStart: 4,
                searchLength: 3,
                markerBytes: [0xAA, 0xAA],
                selection: new FirmwareTerminalMarkerSelection(terminal, 2),
                resultOffset: 4,
                allowedResultRegionId: "allowed"),
            lengthBytes: 1,
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [0x55])]);
        byte[] source = new byte[32];
        source[4] = 0xAA;
        source[5] = 0xAA;
        source[6] = 0xAA;
        source[8] = 0x55;
        source[9] = 0x55;
        FirmwareFamilyResolutionDefinition definition = Definition(structure);

        FirmwareMetadataStructureResolution result = definition.ResolveMetadataStructure(
            "map",
            "config",
            Inputs(new FirmwareArtifactPayload("tp-firmware", source)));

        FirmwareMetadataLocatorOutcome outcome = Assert.IsType<FirmwareMetadataLocatorOutcome>(
            result.Resolved?.LocatorOutcome);
        Assert.Equal(2, outcome.MarkerMatchCount);
        Assert.Equal(expectedMarkerStart, outcome.SelectedMarkerStart);
        Assert.Equal(expectedResultStart, outcome.ResolvedRange.Range.Start);
    }

    /// <summary>Verifies only the exact artifact binding can satisfy a selected structure.</summary>
    [Fact]
    public void MissingExactArtifactBindingRemainsPending()
    {
        FirmwareMetadataStructure structure = Structure(Absolute(0, 2, "allowed"));
        FirmwareFamilyResolutionDefinition definition = Definition(structure);

        FirmwareMetadataStructureResolution result = definition.ResolveMetadataStructure(
            "map",
            "config",
            Inputs(new FirmwareArtifactPayload("dp-firmware", new byte[32])));

        Assert.Equal(FirmwareMetadataStructureResolutionStatus.Pending, result.Status);
        Assert.Equal(FirmwareMetadataStructureResolutionFailure.MissingArtifact, result.Failure);
        Assert.Equal("map", result.MapId);
        Assert.Equal("tp-firmware", result.ArtifactBindingId);
        Assert.Equal("config", result.MetadataStructureId);
        Assert.Null(result.Resolved);
    }

    /// <summary>Verifies absolute, relative, and marker search ranges must fit the bound artifact.</summary>
    [Fact]
    public void StaticLocatorRangesRejectShortArtifacts()
    {
        FirmwareMetadataLocator[] locators =
        [
            Absolute(30, 2, "other"),
            new FirmwareRegionRelativeLocator("other", 14, "other"),
            Marker(
                24,
                8,
                [0xAA],
                new FirmwareUniqueMarkerSelection(),
                0,
                "root"),
        ];

        foreach (FirmwareMetadataLocator locator in locators)
        {
            FirmwareMetadataStructure structure = Structure(
                locator,
                assertions: locator is FirmwareMarkerRelativeLocator
                    ? [FirmwareMetadataByteAssertion.Exact(0, [0xAA, 0xBB])]
                    : []);
            FirmwareMetadataStructureResolution result = Definition(structure).ResolveMetadataStructure(
                "map",
                "config",
                Inputs(new FirmwareArtifactPayload("tp-firmware", new byte[31])));

            Assert.Equal(FirmwareMetadataStructureResolutionStatus.Rejected, result.Status);
            Assert.Equal(FirmwareMetadataStructureResolutionFailure.ArtifactRangeOutOfBounds, result.Failure);
            Assert.Null(result.Resolved);
        }
    }

    /// <summary>Verifies marker selection never guesses on missing, ambiguous, or wrong-count matches.</summary>
    [Fact]
    public void MarkerCardinalityMismatchRejectsCandidateStructure()
    {
        (FirmwareMarkerSelection Selection, byte[] SearchBytes)[] cases =
        [
            (new FirmwareUniqueMarkerSelection(), [0x00, 0x00, 0x00, 0x00]),
            (new FirmwareUniqueMarkerSelection(), [0xAA, 0x00, 0xAA, 0x00]),
            (new FirmwareTerminalMarkerSelection(FirmwareMarkerTerminal.LowestAddress, 2),
                [0xAA, 0x00, 0x00, 0x00]),
        ];

        foreach ((FirmwareMarkerSelection selection, byte[] searchBytes) in cases)
        {
            FirmwareMetadataStructure structure = Structure(
                Marker(4, 4, [0xAA], selection, 8, "root"),
                lengthBytes: 1,
                assertions: [FirmwareMetadataByteAssertion.Exact(0, [0x55])]);
            byte[] source = new byte[32];
            searchBytes.CopyTo(source, 4);
            source[12] = 0x55;
            FirmwareMetadataStructureResolution result = Definition(structure).ResolveMetadataStructure(
                "map",
                "config",
                Inputs(new FirmwareArtifactPayload("tp-firmware", source)));

            Assert.Equal(FirmwareMetadataStructureResolutionStatus.Rejected, result.Status);
            Assert.Equal(FirmwareMetadataStructureResolutionFailure.MarkerCardinalityMismatch, result.Failure);
            Assert.Equal(
                selection is FirmwareTerminalMarkerSelection ? 1 : searchBytes.Count(value => value == 0xAA),
                result.ObservedMarkerMatchCount);
            Assert.Null(result.Resolved);
        }
    }

    /// <summary>Marker-cardinality count is required exactly for that rejection kind.</summary>
    [Fact]
    public void MarkerCardinalityCountCannotBeMissingOrAttachedToAnotherFailure()
    {
        FirmwareMetadataStructure structure = Structure(Absolute(0, 2, "allowed"));

        _ = Assert.Throws<ArgumentException>(() =>
            FirmwareMetadataStructureResolution.Rejected(
                "map",
                structure,
                FirmwareMetadataStructureResolutionFailure.MarkerCardinalityMismatch));
        _ = Assert.Throws<ArgumentException>(() =>
            FirmwareMetadataStructureResolution.Rejected(
                "map",
                structure,
                FirmwareMetadataStructureResolutionFailure.StructureDecodeFailed,
                observedMarkerMatchCount: 1));
    }

    /// <summary>Verifies marker-selected ranges must stay nonnegative and inside the allowed region.</summary>
    [Fact]
    public void MarkerResultRangeRejectsRuntimeEscape()
    {
        FirmwareMetadataLocator[] locators =
        [
            Marker(0, 2, [0xAA], new FirmwareUniqueMarkerSelection(), -1, "root"),
            Marker(14, 2, [0xAA], new FirmwareUniqueMarkerSelection(), 2, "allowed"),
        ];

        foreach (FirmwareMetadataLocator locator in locators)
        {
            FirmwareMetadataStructure structure = Structure(
                locator,
                assertions: [FirmwareMetadataByteAssertion.Exact(0, [0x55, 0x55])]);
            byte[] source = new byte[32];
            FirmwareMarkerRelativeLocator marker = Assert.IsType<FirmwareMarkerRelativeLocator>(locator);
            source[checked((int)marker.SearchRange.Range.Start)] = 0xAA;
            FirmwareMetadataStructureResolution result = Definition(structure).ResolveMetadataStructure(
                "map",
                "config",
                Inputs(new FirmwareArtifactPayload("tp-firmware", source)));

            Assert.Equal(FirmwareMetadataStructureResolutionStatus.Rejected, result.Status);
            Assert.Equal(FirmwareMetadataStructureResolutionFailure.ResolvedRangeOutOfBounds, result.Failure);
        }
    }

    /// <summary>Verifies a marker result may not exceed the artifact even when its map region allows it.</summary>
    [Fact]
    public void MarkerResultRangeRejectsArtifactOnlyEscape()
    {
        FirmwareMetadataStructure structure = Structure(
            Marker(16, 2, [0xAA], new FirmwareUniqueMarkerSelection(), 2, "root"),
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [0x55, 0x55])]);
        byte[] source = new byte[20];
        source[17] = 0xAA;

        FirmwareMetadataStructureResolution result = Definition(structure).ResolveMetadataStructure(
            "map",
            "config",
            Inputs(new FirmwareArtifactPayload("tp-firmware", source)));

        Assert.Equal(FirmwareMetadataStructureResolutionStatus.Rejected, result.Status);
        Assert.Equal(FirmwareMetadataStructureResolutionFailure.ResolvedRangeOutOfBounds, result.Failure);
        Assert.Null(result.Resolved);
    }

    /// <summary>Verifies failed assertions or field decoding reject the complete located structure.</summary>
    [Fact]
    public void StructureDecodeFailureRejectsResolvedRange()
    {
        FirmwareMetadataStructure structure = Structure(
            Absolute(4, 2, "allowed"),
            fields:
            [
                new FirmwareMetadataField(
                    "text",
                    0,
                    2,
                    FirmwareMetadataEncoding.PrintableAscii),
            ],
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [0x41])]);
        byte[] source = new byte[32];
        source[4] = 0x41;
        source[5] = 0x80;

        FirmwareMetadataStructureResolution result = Definition(structure).ResolveMetadataStructure(
            "map",
            "config",
            Inputs(new FirmwareArtifactPayload("tp-firmware", source)));

        Assert.Equal(FirmwareMetadataStructureResolutionStatus.Rejected, result.Status);
        Assert.Equal(FirmwareMetadataStructureResolutionFailure.StructureDecodeFailed, result.Failure);
        Assert.Null(result.Resolved);
    }

    /// <summary>Verifies a failed byte assertion rejects otherwise decodable fields.</summary>
    [Fact]
    public void AssertionFailureRejectsResolvedRange()
    {
        FirmwareMetadataStructure structure = Structure(
            Absolute(4, 2, "allowed"),
            fields: [BytesField("value", 0, 2)],
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [0xAA])]);
        byte[] source = new byte[32];
        source[4] = 0xAB;
        source[5] = 0x01;

        FirmwareMetadataStructureResolution result = Definition(structure).ResolveMetadataStructure(
            "map",
            "config",
            Inputs(new FirmwareArtifactPayload("tp-firmware", source)));

        Assert.Equal(FirmwareMetadataStructureResolutionStatus.Rejected, result.Status);
        Assert.Equal(FirmwareMetadataStructureResolutionFailure.StructureDecodeFailed, result.Failure);
        Assert.Null(result.Resolved);
    }

    /// <summary>Verifies public evaluation cannot bypass map-selected metadata structures.</summary>
    [Fact]
    public void EvaluationRequiresCandidateSelectedStructure()
    {
        FirmwareMetadataStructure structureA = Structure(
            Absolute(0, 2, "allowed"),
            structureId: "config-a");
        FirmwareMetadataStructure structureB = Structure(
            Absolute(2, 2, "allowed"),
            structureId: "config-b");
        FirmwareMetadataSet setA = MetadataSet("metadata-a", structureA);
        FirmwareMetadataSet setB = MetadataSet("metadata-b", structureB);
        FirmwareFamilyResolutionDefinition definition = Definition(
            [
                Map("map-a", [setA]),
                Map("map-b", [setB]),
            ],
            [setA, setB]);
        FirmwareMapResolutionInputs inputs = Inputs(new FirmwareArtifactPayload("tp-firmware", new byte[32]));

        _ = Assert.Throws<KeyNotFoundException>(() =>
            definition.ResolveMetadataStructure("missing-map", "config-a", inputs));
        _ = Assert.Throws<KeyNotFoundException>(() =>
            definition.ResolveMetadataStructure("map-a", "config-b", inputs));
        _ = Assert.Throws<ArgumentException>(() =>
            definition.ResolveMetadataStructure("map-a", " ", inputs));
    }

    /// <summary>Verifies resolution payloads are Domain-created and retain no artifact byte container.</summary>
    [Fact]
    public void ResolutionPayloadsCannotBeForgedOrRetainArtifactPayloads()
    {
        Type[] resultTypes =
        [
            typeof(FirmwareMetadataStructureResolution),
            typeof(FirmwareResolvedMetadataStructure),
            typeof(FirmwareMetadataLocatorOutcome),
        ];

        foreach (Type resultType in resultTypes)
        {
            Assert.Empty(resultType.GetConstructors());
            Assert.DoesNotContain(
                resultType.GetProperties(),
                property => IsForbiddenSourceContainer(property.PropertyType));
            Assert.DoesNotContain(
                resultType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                field => IsForbiddenSourceContainer(field.FieldType));
        }
    }

    /// <summary>Verifies static and marker outcomes reject one-sided marker evidence.</summary>
    [Fact]
    public void LocatorOutcomeRequiresCompleteMarkerEvidenceShape()
    {
        ConstructorInfo constructor = Assert.Single(typeof(FirmwareMetadataLocatorOutcome).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic));
        var range = new FirmwareAddressedRange("flash", new ByteRange(0, 1));

        TargetInvocationException countOnly = Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke(
                [FirmwareMetadataLocatorKind.AbsoluteRange, range, 1, null]));
        TargetInvocationException startOnly = Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke(
                [FirmwareMetadataLocatorKind.AbsoluteRange, range, null, 0L]));

        _ = Assert.IsType<ArgumentException>(countOnly.InnerException);
        _ = Assert.IsType<ArgumentException>(startOnly.InnerException);
    }

    private static FirmwareFamilyResolutionDefinition Definition(FirmwareMetadataStructure structure)
    {
        FirmwareMetadataSet metadataSet = MetadataSet("metadata", structure);
        return Definition([Map("map", [metadataSet])], [metadataSet]);
    }

    private static FirmwareFamilyResolutionDefinition Definition(
        IEnumerable<FirmwareImageMap> maps,
        IEnumerable<FirmwareMetadataSet> metadataSets)
    {
        return new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            maps,
            metadataSets);
    }

    private static FirmwareImageMap Map(string mapId, IEnumerable<FirmwareMetadataSet> metadataSets)
    {
        return FirmwareImageMapTestFactory.CreateDirect(
            mapId,
            "flash",
            new FirmwareMapApplicability(
                ["NT00001"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                32),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [RegionSet()],
            metadataSets,
            ["map-evidence"]);
    }

    private static FirmwareRegionSet RegionSet()
    {
        return new FirmwareRegionSet(
            "physical",
            "flash",
            [
                Region("root", null, 0, 32, FirmwareRegionKind.Image),
                Region("allowed", "root", 0, 16),
                Region("other", "root", 16, 16),
            ],
            ["region-evidence"]);
    }

    private static FirmwareRegion Region(
        string regionId,
        string? parentRegionId,
        long start,
        long length,
        FirmwareRegionKind kind = FirmwareRegionKind.Data)
    {
        return new FirmwareRegion(
            regionId,
            parentRegionId,
            FirmwareRegionOwner.System,
            kind,
            new ByteRange(start, length),
            FirmwareWriteConstraint.Forbidden);
    }

    private static FirmwareMetadataSet MetadataSet(
        string metadataSetId,
        FirmwareMetadataStructure structure)
    {
        return new FirmwareMetadataSet(metadataSetId, [structure], ["metadata-evidence"]);
    }

    private static FirmwareMetadataStructure Structure(
        FirmwareMetadataLocator locator,
        string structureId = "config",
        long lengthBytes = 2,
        IEnumerable<FirmwareMetadataField>? fields = null,
        IEnumerable<FirmwareMetadataByteAssertion>? assertions = null)
    {
        return new FirmwareMetadataStructure(
            structureId,
            "tp-firmware",
            lengthBytes,
            locator,
            fields ?? [],
            assertions ?? []);
    }

    private static FirmwareMapResolutionInputs Inputs(params FirmwareArtifactPayload[] artifacts)
    {
        return new FirmwareMapResolutionInputs(
            "NT00001",
            "standard",
            32,
            requestedTopology: null,
            artifacts);
    }

    private static FirmwareAbsoluteRangeLocator Absolute(
        long start,
        long length,
        string allowedResultRegionId)
    {
        return new FirmwareAbsoluteRangeLocator(
            new FirmwareAddressedRange("flash", new ByteRange(start, length)),
            allowedResultRegionId);
    }

    private static FirmwareMarkerRelativeLocator Marker(
        long searchStart,
        long searchLength,
        ReadOnlySpan<byte> markerBytes,
        FirmwareMarkerSelection selection,
        long resultOffset,
        string allowedResultRegionId)
    {
        return new FirmwareMarkerRelativeLocator(
            new FirmwareAddressedRange("flash", new ByteRange(searchStart, searchLength)),
            markerBytes,
            selection,
            resultOffset,
            allowedResultRegionId);
    }

    private static FirmwareMetadataField BytesField(string fieldId, long offset, int widthBytes)
    {
        return new FirmwareMetadataField(fieldId, offset, widthBytes, FirmwareMetadataEncoding.Bytes);
    }

    private static bool IsForbiddenSourceContainer(Type type)
    {
        return type == typeof(FirmwareArtifactPayload) ||
            type == typeof(byte[]) ||
            type == typeof(Memory<byte>) ||
            type == typeof(ReadOnlyMemory<byte>);
    }
}
