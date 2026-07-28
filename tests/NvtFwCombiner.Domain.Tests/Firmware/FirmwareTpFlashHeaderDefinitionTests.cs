using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests the typed TP Flash Header specialization of canonical metadata.</summary>
public sealed class FirmwareTpFlashHeaderDefinitionTests
{
    /// <summary>
    /// The Type-AB table keeps each physical CRC field exactly once while the
    /// repeated series references those fields by an explicit logical index.
    /// </summary>
    [Fact]
    public void TypeAbHeaderKeepsPhysicalFieldsOnceAndSeriesReferencesThem()
    {
        FirmwareMetadataStructureDefinition definition = CreateTypeAbDefinition();
        FirmwareTpFlashHeaderDefinition header =
            Assert.IsType<FirmwareTpFlashHeaderDefinition>(definition.TypedDefinition);

        Assert.Equal(FirmwareMetadataStructureKind.TpFlashHeader, definition.StructureKind);
        Assert.Equal(0x100, definition.LengthBytes);
        Assert.Equal(
            [
                ("dlm-crc-0", 0x18L),
                ("dlm-crc-1", 0x28L),
                ("dlm-crc-2", 0x2CL),
                ("dlm-crc-3", 0x30L),
                ("dlm-crc-4", 0x34L),
                ("dlm-crc-5", 0x38L),
                ("dlm-crc-6", 0x3CL),
                ("dlm-crc-7", 0x40L),
            ],
            definition.Fields
                .Where(static field => field.FieldId.StartsWith("dlm-crc-", StringComparison.Ordinal))
                .Select(static field => (field.FieldId, field.Range.Start)));

        FirmwareMetadataFieldSeries series = Assert.Single(header.FieldSeries);
        Assert.Equal(
            Enumerable.Range(0, 8).Select(index => (index, $"dlm-crc-{index}")),
            series.Members.Select(static member => (member.Index, member.FieldId)));
        Assert.Equal(
            ["dlm-crc-series"],
            header.FieldGroups.Single(static group =>
                group.GroupId == "dlm-integrity-values").SeriesIds);
    }

    /// <summary>
    /// Owner-declared IC Count rows select Active versus Unused repeated fields;
    /// fixed fields stay active and no formula is inferred from the index.
    /// </summary>
    [Fact]
    public void TypeAbHeaderResolvesExplicitIcCountApplicability()
    {
        FirmwareMetadataStructureDefinition definition = CreateTypeAbDefinition();
        IReadOnlyList<FirmwareResolvedMetadataField> resolved =
            definition.ResolveFields(Topology(chipCount: 4));
        var states =
            resolved.ToDictionary(
                static field => field.Field.FieldId,
                static field => field.Applicability,
                StringComparer.Ordinal);

        Assert.Equal(FirmwareMetadataFieldApplicabilityState.Active, states["header-crc"]);
        Assert.All(
            Enumerable.Range(0, 4),
            index => Assert.Equal(
                FirmwareMetadataFieldApplicabilityState.Active,
                states[$"dlm-crc-{index}"]));
        Assert.All(
            Enumerable.Range(4, 4),
            index => Assert.Equal(
                FirmwareMetadataFieldApplicabilityState.Unused,
                states[$"dlm-crc-{index}"]));
    }

    /// <summary>Missing or unlisted topology remains Unknown instead of being guessed.</summary>
    [Fact]
    public void TypeAbHeaderDoesNotInferMissingApplicability()
    {
        FirmwareMetadataStructureDefinition definition = CreateTypeAbDefinition();

        foreach (TopologySelection? topology in new[] { null, Topology(chipCount: 9) })
        {
            IReadOnlyList<FirmwareResolvedMetadataField> resolved =
                definition.ResolveFields(topology);

            Assert.All(
                resolved.Where(static field =>
                    field.Field.FieldId.StartsWith("dlm-crc-", StringComparison.Ordinal)),
                static field => Assert.Equal(
                    FirmwareMetadataFieldApplicabilityState.Unknown,
                    field.Applicability));
            Assert.Equal(
                FirmwareMetadataFieldApplicabilityState.Active,
                resolved.Single(static field => field.Field.FieldId == "header-crc").Applicability);
        }
    }

    /// <summary>TP fields, series, groups, and applicability references fail closed.</summary>
    [Fact]
    public void TypeAbHeaderRejectsDanglingOrContradictoryReferences()
    {
        FirmwareMetadataField[] fields =
        [
            UInt32("header-crc", 0),
            UInt32("dlm-crc-0", 0x18),
        ];
        FirmwareMetadataNamedSpan[] spans =
        [
            new("complete-header", new ByteRange(0, 0x20)),
        ];

        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataStructureDefinition(
            "type-ab",
            0x20,
            fields,
            [],
            typedDefinition: new FirmwareTpFlashHeaderDefinition(
                spans,
                [
                    Semantics("header-crc", TpFlashHeaderFieldSubject.Header),
                    Semantics("missing", TpFlashHeaderFieldSubject.Dlm, logicalIndex: 0),
                ],
                [],
                [])));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataStructureDefinition(
            "type-ab",
            0x20,
            fields,
            [],
            typedDefinition: new FirmwareTpFlashHeaderDefinition(
                spans,
                [
                    Semantics("header-crc", TpFlashHeaderFieldSubject.Header),
                    Semantics("dlm-crc-0", TpFlashHeaderFieldSubject.Dlm, logicalIndex: 0),
                ],
                [
                    new FirmwareMetadataFieldSeries(
                        "dlm-crc-series",
                        [new FirmwareMetadataFieldSeriesMember(0, "dlm-crc-0")],
                        [
                            new FirmwareMetadataFieldSeriesApplicability(
                                2,
                                [1]),
                        ]),
                ],
                [])));
    }

    /// <summary>Series and groups reject incomplete or contradictory table declarations.</summary>
    [Fact]
    public void TypeAbHeaderRejectsInvalidSeriesAndGroupShapes()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FirmwareMetadataReferenceTarget(
                (FirmwareMetadataReferenceTargetKind)int.MaxValue,
                "field"));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FirmwareResolvedMetadataField(
                UInt32("field", 0),
                (FirmwareMetadataFieldApplicabilityState)int.MaxValue));
        _ = Assert.Throws<ArgumentException>(() =>
            new FirmwareMetadataFieldSeriesApplicability(2, [-1]));
        _ = Assert.Throws<ArgumentException>(() =>
            new FirmwareMetadataFieldSeries(
                "dlm-crc-series",
                [],
                [new FirmwareMetadataFieldSeriesApplicability(1, [])]));
        _ = Assert.Throws<ArgumentException>(() =>
            new FirmwareMetadataFieldSeries(
                "dlm-crc-series",
                [new FirmwareMetadataFieldSeriesMember(0, "dlm-crc-0")],
                []));
        _ = Assert.Throws<ArgumentException>(() =>
            new FirmwareMetadataFieldSeries(
                "dlm-crc-series",
                [new FirmwareMetadataFieldSeriesMember(0, "dlm-crc-0")],
                [
                    new FirmwareMetadataFieldSeriesApplicability(1, [0]),
                    new FirmwareMetadataFieldSeriesApplicability(1, [0]),
                ]));
        _ = Assert.Throws<ArgumentException>(() =>
            new FirmwareMetadataFieldSeries(
                "dlm-crc-series",
                [new FirmwareMetadataFieldSeriesMember(0, "dlm-crc-0")],
                [new FirmwareMetadataFieldSeriesApplicability(1, [1])]));
        _ = Assert.Throws<ArgumentException>(() =>
            new FirmwareMetadataFieldGroup("empty-group", [], []));
        _ = Assert.Throws<ArgumentException>(() =>
            new FirmwareMetadataFieldGroup(
                "duplicate-group",
                ["dlm-crc-0", "dlm-crc-0"],
                []));
    }

    /// <summary>
    /// Address roles require an explicit value address space/basis, while
    /// non-address fields cannot carry one.
    /// </summary>
    [Fact]
    public void TypeAbHeaderRejectsMissingOrIncompatibleStoredAddressSemantics()
    {
        FirmwareMetadataField[] fields = [UInt32("address", 0)];
        FirmwareMetadataNamedSpan[] spans =
        [
            new("complete-header", new ByteRange(0, 4)),
        ];

        _ = Assert.Throws<ArgumentException>(() =>
            AddressDefinition(
                fields,
                spans,
                new FirmwareTpFlashHeaderFieldSemantics(
                    "address",
                    "complete-header",
                    TpFlashHeaderFieldSubject.Ilm,
                    TpFlashHeaderFieldRole.DestinationAddress)));
        _ = Assert.Throws<ArgumentException>(() =>
            AddressDefinition(
                fields,
                spans,
                new FirmwareTpFlashHeaderFieldSemantics(
                    "address",
                    "complete-header",
                    TpFlashHeaderFieldSubject.Ilm,
                    TpFlashHeaderFieldRole.DestinationAddress,
                    storedAddress:
                        new FirmwareTpFlashHeaderStoredAddressSemantics(
                            "sram",
                            TpFlashHeaderStoredAddressBasis.TpBinOffset))));
        _ = Assert.Throws<ArgumentException>(() =>
            AddressDefinition(
                fields,
                spans,
                new FirmwareTpFlashHeaderFieldSemantics(
                    "address",
                    "complete-header",
                    TpFlashHeaderFieldSubject.Ilm,
                    TpFlashHeaderFieldRole.Size,
                    storedAddress:
                        new FirmwareTpFlashHeaderStoredAddressSemantics(
                            "sram",
                            TpFlashHeaderStoredAddressBasis.Absolute))));
    }

    private static FirmwareMetadataStructureDefinition CreateTypeAbDefinition()
    {
        FirmwareMetadataField[] fields =
        [
            UInt32("header-crc", 0x00),
            UInt32("ilm-destination-address", 0x04),
            UInt32("ilm-size", 0x08),
            UInt32("ilm-crc", 0x0C),
            UInt32("dlm-destination-address", 0x10),
            UInt32("dlm-size", 0x14),
            UInt32("dlm-crc-0", 0x18),
            UInt32("dlm-diff-destination-address", 0x1C),
            UInt16("dlm-diff-size", 0x20),
            Byte("build-read-command", 0x24),
            Byte("build-divider-count", 0x25),
            Byte("spi-option", 0x26),
            UInt32("dlm-crc-1", 0x28),
            UInt32("dlm-crc-2", 0x2C),
            UInt32("dlm-crc-3", 0x30),
            UInt32("dlm-crc-4", 0x34),
            UInt32("dlm-crc-5", 0x38),
            UInt32("dlm-crc-6", 0x3C),
            UInt32("dlm-crc-7", 0x40),
            UInt32("ilm-bin-start-address", 0x64),
            UInt32("dlm-bin-start-address", 0x68),
            UInt32("dlm-diff-bin-start-address", 0x6C),
        ];
        FirmwareTpFlashHeaderFieldSemantics[] semantics =
        [
            Semantics("header-crc", TpFlashHeaderFieldSubject.Header),
            AddressSemantics("ilm-destination-address", TpFlashHeaderFieldSubject.Ilm),
            SizeSemantics("ilm-size", TpFlashHeaderFieldSubject.Ilm),
            Semantics("ilm-crc", TpFlashHeaderFieldSubject.Ilm),
            AddressSemantics("dlm-destination-address", TpFlashHeaderFieldSubject.Dlm),
            SizeSemantics("dlm-size", TpFlashHeaderFieldSubject.Dlm),
            .. Enumerable.Range(0, 8).Select(index =>
                Semantics($"dlm-crc-{index}", TpFlashHeaderFieldSubject.Dlm, index)),
            AddressSemantics(
                "dlm-diff-destination-address",
                TpFlashHeaderFieldSubject.DlmDifference),
            SizeSemantics("dlm-diff-size", TpFlashHeaderFieldSubject.DlmDifference),
            OptionSemantics("build-read-command"),
            OptionSemantics("build-divider-count"),
            OptionSemantics("spi-option"),
            BinStartSemantics("ilm-bin-start-address", TpFlashHeaderFieldSubject.Ilm),
            BinStartSemantics("dlm-bin-start-address", TpFlashHeaderFieldSubject.Dlm),
            BinStartSemantics(
                "dlm-diff-bin-start-address",
                TpFlashHeaderFieldSubject.DlmDifference),
        ];
        var series = new FirmwareMetadataFieldSeries(
            "dlm-crc-series",
            [
                .. Enumerable.Range(0, 8).Select(index =>
                    new FirmwareMetadataFieldSeriesMember(index, $"dlm-crc-{index}")),
            ],
            [
                .. Enumerable.Range(1, 8).Select(chipCount =>
                    new FirmwareMetadataFieldSeriesApplicability(
                        chipCount,
                        Enumerable.Range(0, chipCount))),
            ]);
        var typedDefinition = new FirmwareTpFlashHeaderDefinition(
            [new FirmwareMetadataNamedSpan("complete-header", new ByteRange(0, 0x100))],
            semantics,
            [series],
            [
                new FirmwareMetadataFieldGroup(
                    "dlm-integrity-values",
                    [],
                    ["dlm-crc-series"]),
                new FirmwareMetadataFieldGroup(
                    "tp-bank-relative-start-addresses",
                    [
                        "ilm-bin-start-address",
                        "dlm-bin-start-address",
                        "dlm-diff-bin-start-address",
                    ],
                    []),
            ]);
        return new FirmwareMetadataStructureDefinition(
            "type-ab-tp-flash-header",
            0x100,
            fields,
            [],
            typedDefinition: typedDefinition);
    }

    private static FirmwareTpFlashHeaderFieldSemantics Semantics(
        string fieldId,
        TpFlashHeaderFieldSubject subject,
        int? logicalIndex = null)
    {
        return new FirmwareTpFlashHeaderFieldSemantics(
            fieldId,
            "complete-header",
            subject,
            TpFlashHeaderFieldRole.IntegrityValue,
            logicalIndex);
    }

    private static FirmwareTpFlashHeaderFieldSemantics AddressSemantics(
        string fieldId,
        TpFlashHeaderFieldSubject subject)
    {
        return new FirmwareTpFlashHeaderFieldSemantics(
            fieldId,
            "complete-header",
            subject,
            TpFlashHeaderFieldRole.DestinationAddress,
            storedAddress:
                new FirmwareTpFlashHeaderStoredAddressSemantics(
                    "sram",
                    TpFlashHeaderStoredAddressBasis.Absolute));
    }

    private static FirmwareTpFlashHeaderFieldSemantics SizeSemantics(
        string fieldId,
        TpFlashHeaderFieldSubject subject)
    {
        return new FirmwareTpFlashHeaderFieldSemantics(
            fieldId,
            "complete-header",
            subject,
            TpFlashHeaderFieldRole.Size);
    }

    private static FirmwareTpFlashHeaderFieldSemantics BinStartSemantics(
        string fieldId,
        TpFlashHeaderFieldSubject subject)
    {
        return new FirmwareTpFlashHeaderFieldSemantics(
            fieldId,
            "complete-header",
            subject,
            TpFlashHeaderFieldRole.TpBinStartAddress,
            storedAddress:
                new FirmwareTpFlashHeaderStoredAddressSemantics(
                    "tp-bin",
                    TpFlashHeaderStoredAddressBasis.TpBinOffset));
    }

    private static FirmwareTpFlashHeaderFieldSemantics OptionSemantics(string fieldId)
    {
        return new FirmwareTpFlashHeaderFieldSemantics(
            fieldId,
            "complete-header",
            TpFlashHeaderFieldSubject.Header,
            TpFlashHeaderFieldRole.Option);
    }

    private static FirmwareMetadataField UInt32(string fieldId, long offset)
    {
        return new FirmwareMetadataField(
            fieldId,
            offset,
            4,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian);
    }

    private static FirmwareMetadataField UInt16(string fieldId, long offset)
    {
        return new FirmwareMetadataField(
            fieldId,
            offset,
            2,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian);
    }

    private static FirmwareMetadataField Byte(string fieldId, long offset)
    {
        return new FirmwareMetadataField(
            fieldId,
            offset,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian);
    }

    private static TopologySelection Topology(int chipCount)
    {
        return new TopologySelection(
            chipCount,
            $"{chipCount} IC",
            TopologySelectionSource.Requested,
            "test");
    }

    private static FirmwareMetadataStructureDefinition AddressDefinition(
        FirmwareMetadataField[] fields,
        FirmwareMetadataNamedSpan[] spans,
        FirmwareTpFlashHeaderFieldSemantics semantics)
    {
        return new FirmwareMetadataStructureDefinition(
            "address-header",
            4,
            fields,
            [],
            typedDefinition: new FirmwareTpFlashHeaderDefinition(
                spans,
                [semantics],
                [],
                []));
    }
}
