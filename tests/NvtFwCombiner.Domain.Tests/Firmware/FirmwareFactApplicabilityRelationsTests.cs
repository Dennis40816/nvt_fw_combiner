using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests exact finite-domain relations used by map-bound fact applicability.</summary>
public sealed class FirmwareFactApplicabilityRelationsTests
{
    /// <summary>Verifies topology, mode, category, and capacity relations use mathematical containment.</summary>
    [Fact]
    public void RelationsCheckStaticSelectionDimensions()
    {
        var structures = new Dictionary<string, FirmwareMetadataStructure>(StringComparer.Ordinal);
        FirmwareFactApplicability cascade = Applicability(
            topology: TopologyRequirement.RequireCascade(2, 3),
            modeIds: ["standard"],
            categories: ["common-a"]);
        FirmwareFactApplicability broad = Applicability(
            topology: TopologyRequirement.NoTopologyConstraint(),
            modeIds: ["ab", "standard"],
            categories: []);

        Assert.True(FirmwareFactApplicabilityRelations.IsContainedBy(cascade, broad, structures));
        Assert.False(FirmwareFactApplicabilityRelations.IsContainedBy(broad, cascade, structures));
        Assert.True(FirmwareFactApplicabilityRelations.Overlaps(cascade, broad, structures));
        Assert.False(FirmwareFactApplicabilityRelations.Overlaps(
            cascade,
            Applicability(capacityBytes: 32),
            structures));
    }

    /// <summary>Verifies exclusion-only predicates use cardinality without enumerating a field domain.</summary>
    [Fact]
    public void RelationsRejectExclusionsThatCoverTheCompleteFiniteDomain()
    {
        FirmwareMetadataStructure structure = Structure(UnsignedBitField());
        var structures = new Dictionary<string, FirmwareMetadataStructure>(StringComparer.Ordinal)
        {
            [structure.StructureId] = structure,
        };
        FirmwareFactApplicability impossible = Applicability(
            predicates:
            [
                Predicate(FirmwareMetadataPredicateOperator.NotEqual, Unsigned(0)),
                Predicate(FirmwareMetadataPredicateOperator.NotEqual, Unsigned(1)),
            ]);
        FirmwareFactApplicability byteExclusion = Applicability(
            predicates:
            [
                new FirmwareMetadataPredicate(
                    "config",
                    "raw",
                    FirmwareMetadataPredicateOperator.NotEqual,
                    [FirmwareMetadataValue.FromBytes([0])]),
            ]);
        FirmwareMetadataStructure byteStructure = Structure(BytesField());
        var byteStructures = new Dictionary<string, FirmwareMetadataStructure>(StringComparer.Ordinal)
        {
            [byteStructure.StructureId] = byteStructure,
        };

        Assert.False(FirmwareFactApplicabilityRelations.IsSatisfiable(impossible, structures));
        Assert.True(FirmwareFactApplicabilityRelations.IsSatisfiable(byteExclusion, byteStructures));
    }

    /// <summary>Verifies positive typed constraints prove containment while exclusion-only constraints fail closed.</summary>
    [Fact]
    public void RelationsCompareTypedPositiveAndExclusionOnlyConstraints()
    {
        FirmwareMetadataStructure structure = Structure(UnsignedBitField());
        var structures = new Dictionary<string, FirmwareMetadataStructure>(StringComparer.Ordinal)
        {
            [structure.StructureId] = structure,
        };
        FirmwareFactApplicability equalsZero = Applicability(
            predicates: [Predicate(FirmwareMetadataPredicateOperator.Equal, Unsigned(0))]);
        FirmwareFactApplicability oneOf = Applicability(
            predicates: [Predicate(FirmwareMetadataPredicateOperator.OneOf, Unsigned(0), Unsigned(1))]);
        FirmwareFactApplicability excludeOne = Applicability(
            predicates: [Predicate(FirmwareMetadataPredicateOperator.NotEqual, Unsigned(1))]);

        Assert.True(FirmwareFactApplicabilityRelations.IsContainedBy(equalsZero, oneOf, structures));
        Assert.False(FirmwareFactApplicabilityRelations.IsContainedBy(excludeOne, equalsZero, structures));
        Assert.True(FirmwareFactApplicabilityRelations.Overlaps(equalsZero, excludeOne, structures));
    }

    /// <summary>Verifies every closed topology shape pair follows its documented chip-count set relation.</summary>
    [Fact]
    public void RelationsExhaustivelyCheckTopologyContainmentAndOverlap()
    {
        (TopologyRequirement Requirement, int[] Counts)[] cases =
        [
            (TopologyRequirement.NoTopologyConstraint(), [1, 2, 3, 4, 8, 64]),
            (TopologyRequirement.RequireSingleChip(), [1]),
            (TopologyRequirement.RequireCascade(2, 3), [2, 3]),
            (TopologyRequirement.RequireCascade(2), [2, 3, 4, 8, 64]),
            (TopologyRequirement.RequireExactCount(1), [1]),
            (TopologyRequirement.RequireExactCount(2), [2]),
            (TopologyRequirement.RequireExactCount(4), [4]),
        ];
        var structures = new Dictionary<string, FirmwareMetadataStructure>(StringComparer.Ordinal);

        foreach ((TopologyRequirement candidateTopology, int[] candidateCounts) in cases)
        {
            foreach ((TopologyRequirement containerTopology, int[] containerCounts) in cases)
            {
                FirmwareFactApplicability candidate = Applicability(topology: candidateTopology);
                FirmwareFactApplicability container = Applicability(topology: containerTopology);
                bool expectedContainment = candidateCounts.All(containerCounts.Contains);
                bool expectedOverlap = candidateCounts.Any(containerCounts.Contains);

                Assert.Equal(
                    expectedContainment,
                    FirmwareFactApplicabilityRelations.IsContainedBy(candidate, container, structures));
                Assert.Equal(
                    expectedOverlap,
                    FirmwareFactApplicabilityRelations.Overlaps(candidate, container, structures));
            }
        }

        Assert.True(FirmwareFactApplicabilityRelations.HasSameScope(
            Applicability(topology: TopologyRequirement.RequireSingleChip()),
            Applicability(topology: TopologyRequirement.RequireExactCount(1)),
            structures));
    }

    /// <summary>Verifies mode, capacity, and Common FW category relations do not widen a fact scope.</summary>
    [Fact]
    public void RelationsCheckStaticScopeContainmentAndDisjointOverlap()
    {
        var structures = new Dictionary<string, FirmwareMetadataStructure>(StringComparer.Ordinal);
        FirmwareFactApplicability narrow = Applicability(
            modeIds: ["standard"],
            categories: ["common-a"],
            topology: TopologyRequirement.RequireExactCount(2));
        FirmwareFactApplicability broad = Applicability(
            modeIds: ["ab", "standard"],
            categories: [],
            topology: TopologyRequirement.RequireCascade(2));
        FirmwareFactApplicability modeDisjoint = Applicability(modeIds: ["ab"]);
        FirmwareFactApplicability categoryDisjoint = Applicability(categories: ["common-b"]);
        FirmwareFactApplicability capacityMismatch = Applicability(capacityBytes: 32);

        Assert.True(FirmwareFactApplicabilityRelations.IsContainedBy(narrow, broad, structures));
        Assert.False(FirmwareFactApplicabilityRelations.IsContainedBy(broad, narrow, structures));
        Assert.False(FirmwareFactApplicabilityRelations.Overlaps(narrow, modeDisjoint, structures));
        Assert.False(FirmwareFactApplicabilityRelations.Overlaps(narrow, categoryDisjoint, structures));
        Assert.False(FirmwareFactApplicabilityRelations.Overlaps(narrow, capacityMismatch, structures));
    }

    /// <summary>Verifies typed predicate conjunctions use finite domains without scalar coercion or enumeration shortcuts.</summary>
    [Fact]
    public void RelationsCheckPredicateConjunctionsAndTypedFiniteDomains()
    {
        FirmwareMetadataStructure structure = MultiFieldStructure();
        var structures = new Dictionary<string, FirmwareMetadataStructure>(StringComparer.Ordinal)
        {
            [structure.StructureId] = structure,
        };
        FirmwareFactApplicability contradictory = Applicability(
            predicates:
            [
                Predicate("value", FirmwareMetadataPredicateOperator.Equal, Unsigned(0)),
                Predicate("value", FirmwareMetadataPredicateOperator.Equal, Unsigned(1)),
            ]);
        FirmwareFactApplicability positiveMinusExclusion = Applicability(
            predicates:
            [
                Predicate("value", FirmwareMetadataPredicateOperator.OneOf, Unsigned(0), Unsigned(1)),
                Predicate("value", FirmwareMetadataPredicateOperator.NotEqual, Unsigned(0)),
            ]);
        FirmwareFactApplicability equalsOne = Applicability(
            predicates: [Predicate("value", FirmwareMetadataPredicateOperator.Equal, Unsigned(1))]);
        FirmwareFactApplicability bytesExhausted = Applicability(
            predicates:
            [
                .. Enumerable.Range(0, 256).Select(value => Predicate(
                    "raw",
                    FirmwareMetadataPredicateOperator.NotEqual,
                    FirmwareMetadataValue.FromBytes([(byte)value]))),
            ]);
        FirmwareFactApplicability textExhausted = Applicability(
            predicates:
            [
                .. Enumerable.Range(0x20, 95).Select(value => Predicate(
                    "text",
                    FirmwareMetadataPredicateOperator.NotEqual,
                    FirmwareMetadataValue.FromText(((char)value).ToString()))),
            ]);
        FirmwareFactApplicability signedExhausted = Applicability(
            predicates:
            [
                .. Enumerable.Range(-128, 256).Select(value => Predicate(
                    "signed",
                    FirmwareMetadataPredicateOperator.NotEqual,
                    Signed(value))),
            ]);
        FirmwareFactApplicability wrongTypedValue = Applicability(
            predicates:
            [
                Predicate("signed", FirmwareMetadataPredicateOperator.Equal, Unsigned(1)),
            ]);

        Assert.False(FirmwareFactApplicabilityRelations.IsSatisfiable(contradictory, structures));
        Assert.True(FirmwareFactApplicabilityRelations.IsContainedBy(
            positiveMinusExclusion,
            equalsOne,
            structures));
        Assert.False(FirmwareFactApplicabilityRelations.Overlaps(
            positiveMinusExclusion,
            Applicability(predicates: [Predicate("value", FirmwareMetadataPredicateOperator.Equal, Unsigned(0))]),
            structures));
        Assert.False(FirmwareFactApplicabilityRelations.IsSatisfiable(bytesExhausted, structures));
        Assert.False(FirmwareFactApplicabilityRelations.IsSatisfiable(textExhausted, structures));
        Assert.False(FirmwareFactApplicabilityRelations.IsSatisfiable(signedExhausted, structures));
        _ = Assert.Throws<ArgumentException>(() =>
            FirmwareFactApplicabilityRelations.IsSatisfiable(wrongTypedValue, structures));
    }

    private static FirmwareFactApplicability Applicability(
        TopologyRequirement? topology = null,
        IReadOnlyList<string>? modeIds = null,
        IReadOnlyList<string>? categories = null,
        long capacityBytes = 16,
        IReadOnlyList<FirmwareMetadataPredicate>? predicates = null)
    {
        return new FirmwareFactApplicability(
            modeIds ?? ["standard"],
            topology ?? TopologyRequirement.NoTopologyConstraint(),
            capacityBytes,
            categories,
            predicates);
    }

    private static FirmwareMetadataPredicate Predicate(
        FirmwareMetadataPredicateOperator comparison,
        params FirmwareMetadataValue[] values)
    {
        return Predicate("value", comparison, values);
    }

    private static FirmwareMetadataPredicate Predicate(
        string fieldId,
        FirmwareMetadataPredicateOperator comparison,
        params FirmwareMetadataValue[] values)
    {
        return new FirmwareMetadataPredicate("config", fieldId, comparison, values);
    }

    private static FirmwareMetadataStructure Structure(FirmwareMetadataField field)
    {
        return new FirmwareMetadataStructure(
            "config",
            "tp-firmware",
            1,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(0, 1)),
                "root"),
            [field],
            []);
    }

    private static FirmwareMetadataStructure MultiFieldStructure()
    {
        return new FirmwareMetadataStructure(
            "config",
            "tp-firmware",
            4,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(0, 4)),
                "root"),
            [
                UnsignedBitField(),
                new FirmwareMetadataField(
                    "signed",
                    1,
                    1,
                    FirmwareMetadataEncoding.SignedInteger,
                    FirmwareMetadataByteOrder.LittleEndian),
                new FirmwareMetadataField("raw", 2, 1, FirmwareMetadataEncoding.Bytes),
                new FirmwareMetadataField("text", 3, 1, FirmwareMetadataEncoding.PrintableAscii),
            ],
            []);
    }

    private static FirmwareMetadataField UnsignedBitField()
    {
        return new FirmwareMetadataField(
            "value",
            0,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            new FirmwareMetadataBitSlice(0, 1));
    }

    private static FirmwareMetadataField BytesField()
    {
        return new FirmwareMetadataField("raw", 0, 1, FirmwareMetadataEncoding.Bytes);
    }

    private static FirmwareMetadataValue Unsigned(ulong value)
    {
        return FirmwareMetadataValue.FromUnsignedInteger(value);
    }

    private static FirmwareMetadataValue Signed(long value)
    {
        return FirmwareMetadataValue.FromSignedInteger(value);
    }
}
