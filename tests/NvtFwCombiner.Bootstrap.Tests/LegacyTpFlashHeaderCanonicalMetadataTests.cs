using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Issue #187 contract tests for the admitted NT51917/27/28 and NT51923/26
/// TP Flash Header families.
/// </summary>
public sealed class LegacyTpFlashHeaderCanonicalMetadataTests
{
    private static readonly IReadOnlyList<int>[] Expected927ActiveCrcIndices =
    [
        [0, 1],
        [0, 1, 2],
        [0, 1, 2, 3],
    ];

    /// <summary>
    /// This temporary differential protects #187 while report classification
    /// still consumes the read-only legacy adapter. #194 deletes both together
    /// after the report consumes canonical resolved fields.
    /// </summary>
    [Theory]
    [InlineData("NT51917", "nt51927-927-tp-flash-header-read-model")]
    [InlineData("NT51923", "nt51923-normal-tp-flash-header-read-model")]
    [InlineData("NT51926", "nt51926-normal-tp-flash-header-read-model")]
    [InlineData("NT51927", "nt51927-927-tp-flash-header-read-model")]
    [InlineData("NT51928", "nt51927-927-tp-flash-header-read-model")]
    public void CanonicalDefinitionsMatchLegacyReadOnlyReportAdapterUntil194(
        string icId,
        string bindingId)
    {
        MetadataPlanEntry entry = HeaderEntry(icId, bindingId);
        Assert.True(TpHeaderCatalog.TryGetLayout(icId, out TpHeaderLayout? layout));
        TpHeaderLayout legacy = Assert.IsType<TpHeaderLayout>(layout);

        Assert.Equal(
            legacy.Fields.Select(static field => (field.FieldId, field.Range)),
            entry.StructureDefinition.Definition.Fields.Select(
                static field => (field.FieldId, field.Range)));
    }

    /// <summary>
    /// NT51917/27/28 reuse one 927 definition through the approved perfect and
    /// TP-Header shared relationships without importing NT51928 container facts.
    /// </summary>
    [Fact]
    public void Nt51917Nt51927Nt51928ShareOne927HeaderDefinition()
    {
        MetadataPlanEntry nt51917 = HeaderEntry("NT51917", "nt51927-927-tp-flash-header-read-model");
        MetadataPlanEntry nt51927 = HeaderEntry("NT51927", "nt51927-927-tp-flash-header-read-model");
        MetadataPlanEntry nt51928 = HeaderEntry("NT51928", "nt51927-927-tp-flash-header-read-model");
        MetadataPlanEntry[] copyReferences =
        [
            HeaderEntry("NT51917", "nt51927-927-tp-flash-header-copy-reference"),
            HeaderEntry("NT51927", "nt51927-927-tp-flash-header-copy-reference"),
            HeaderEntry("NT51928", "nt51927-927-tp-flash-header-copy-reference"),
        ];
        FirmwareMetadataStructureDefinition definition =
            nt51927.StructureDefinition.Definition;

        Assert.Same(definition, nt51917.StructureDefinition.Definition);
        Assert.Same(definition, nt51928.StructureDefinition.Definition);
        Assert.Equal("1.4.0", nt51927.FamilyDefinition.FamilyVersion);
        Assert.Equal("1.5.0", nt51928.FamilyDefinition.FamilyVersion);
        Assert.NotEqual(
            nt51927.FamilyDefinition.FamilyContentHash,
            nt51928.FamilyDefinition.FamilyContentHash);
        Assert.Equal("nt51927-927-tp-flash-header", definition.DefinitionId);
        Assert.Equal(FirmwareMetadataStructureKind.TpFlashHeader, definition.StructureKind);
        Assert.Equal(0x460, definition.LengthBytes);
        Assert.Equal(68, definition.Fields.Count);
        Assert.Empty(definition.Assertions);
        Assert.All(
            [nt51917, nt51927, nt51928],
            static entry =>
            {
                Assert.Equal("tp-input", entry.SpaceId);
                Assert.Equal("tp-input", entry.SlotId);
                Assert.DoesNotContain(MetadataReferencePurpose.Integrity, entry.Purposes);
                Assert.DoesNotContain(MetadataReferencePurpose.Processor, entry.Purposes);
                FirmwareRegionRelativeLocator locator =
                    Assert.IsType<FirmwareRegionRelativeLocator>(
                        entry.StructureDefinition.Locator);
                Assert.Equal("tp-code", locator.RegionId);
                Assert.Equal(0, locator.Offset);
                Assert.Equal("tp-code", locator.AllowedResultRegionId);
            });

        FirmwareTpFlashHeaderDefinition header =
            Assert.IsType<FirmwareTpFlashHeaderDefinition>(
                definition.TypedDefinition);
        Assert.Equal(
            new (string Id, ByteRange Range)[]
            {
                ("global-header", new ByteRange(0, 0x100)),
                ("header-refresh-source", new ByteRange(0x200, 0x190)),
                ("modeled-instance-headers", new ByteRange(0x200, 0xE0)),
                ("final-header-backup-source", new ByteRange(0, 0x460)),
            }.OrderBy(static item => item.Range.Start)
                .ThenBy(static item => item.Range.Length)
                .ThenBy(static item => item.Id, StringComparer.Ordinal),
            header.Spans
                .Select(static span => (span.SpanId, span.Range))
                .OrderBy(static item => item.Range.Start)
                .ThenBy(static item => item.Range.Length)
                .ThenBy(static item => item.SpanId, StringComparer.Ordinal));

        Assert.Equal(
            ["dlm-crc-series", "header-crc-series", "ilm-crc-series"],
            header.FieldSeries.Select(static series => series.SeriesId));
        foreach (FirmwareMetadataFieldSeries series in header.FieldSeries)
        {
            Assert.Equal(Enumerable.Range(0, 4), series.Members.Select(static member => member.Index));
            Assert.Equal([1, 2, 3], series.Applicability.Select(static row => row.ChipCount));
            Assert.Equal(
                Expected927ActiveCrcIndices,
                series.Applicability.Select(static row => row.ActiveIndices));
        }

        AssertCrcApplicability(definition, chipCount: null, activeThrough: null);
        AssertCrcApplicability(definition, chipCount: 1, activeThrough: 1);
        AssertCrcApplicability(definition, chipCount: 2, activeThrough: 2);
        AssertCrcApplicability(definition, chipCount: 3, activeThrough: 3);
        Assert.All(
            copyReferences,
            static entry =>
            {
                Assert.Equal([MetadataReferencePurpose.Copy], entry.Purposes);
                Assert.Equal(
                    ["final-header-backup-source", "header-refresh-source"],
                    entry.TargetReferences.Select(static target => target.TargetId));
                Assert.DoesNotContain(MetadataReferencePurpose.Integrity, entry.Purposes);
                Assert.DoesNotContain(MetadataReferencePurpose.Processor, entry.Purposes);
            });

        Assert.All(
            [nt51917, nt51927, nt51928],
            static entry =>
            {
                SharedFactRelationship relationship = Assert.Single(
                    entry.FamilyDefinition.FamilyRelationships.OfType<SharedFactRelationship>(),
                    static candidate =>
                        candidate.Role == FirmwareSharedFactRole.TpFlashHeaderShared);
                FirmwareSharedFactReference sharedDefinition = Assert.Single(
                    relationship.SharedFactReferences,
                    static reference =>
                        reference.Kind == FirmwareSharedFactKind.MetadataDefinition);
                Assert.Equal("nt51927-927-tp-flash-header", sharedDefinition.FactId);
                Assert.Same(
                    entry.StructureDefinition.Definition,
                    sharedDefinition.MetadataDefinition);
                Assert.Equal(["NT51917", "NT51927", "NT51928"], relationship.MemberIds);
            });
    }

    /// <summary>
    /// Both admitted NT51928 capacities retain the same read-only Header
    /// definition and copy references without gaining mutation authority.
    /// </summary>
    [Fact]
    public void Nt51928BothCapacitiesRetainOneReadOnly927HeaderContract()
    {
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51928"];
        IReadOnlyList<long> capacities =
            registration.GetMapCapacities(
                out IReadOnlyList<CompositionIssue> capacityIssues);
        Assert.Empty(capacityIssues);
        Assert.Equal([0x40000L, 0x80000L], capacities);

        FirmwareMetadataStructureDefinition? canonicalDefinition = null;
        foreach (long capacity in capacities)
        {
            IReadOnlyCollection<string> selectedInputSlotIds = capacity == 0x40000
                ? []
                : [CompositionAddressSpaceIds.LdcInput];
            MetadataPlanDefinition plan = CreatePlan(
                registration,
                capacity,
                selectedInputSlotIds);
            MetadataPlanEntry readModel = Assert.Single(
                plan.Entries,
                static entry => entry.BindingId ==
                    "nt51927-927-tp-flash-header-read-model");
            MetadataPlanEntry copyReference = Assert.Single(
                plan.Entries,
                static entry => entry.BindingId ==
                    "nt51927-927-tp-flash-header-copy-reference");
            string expectedMapId = capacity == 0x40000
                ? "nt51928-standard-merge-256k"
                : "nt51928-standard-merge-512k";

            canonicalDefinition ??= readModel.StructureDefinition.Definition;
            Assert.Same(canonicalDefinition, readModel.StructureDefinition.Definition);
            Assert.Same(
                canonicalDefinition,
                copyReference.StructureDefinition.Definition);
            Assert.Equal(expectedMapId, readModel.ResolvedMap.ImageMap.MapId);
            Assert.Equal(expectedMapId, copyReference.ResolvedMap.ImageMap.MapId);
            Assert.Equal(
                [
                    MetadataReferencePurpose.Inspection,
                    MetadataReferencePurpose.Formatting,
                    MetadataReferencePurpose.MemoryProjection,
                    MetadataReferencePurpose.ReportClassification,
                ],
                readModel.Purposes);
            Assert.Equal([MetadataReferencePurpose.Copy], copyReference.Purposes);
            Assert.Equal(
                ["final-header-backup-source", "header-refresh-source"],
                copyReference.TargetReferences.Select(static target => target.TargetId));
            Assert.DoesNotContain(MetadataReferencePurpose.Integrity, readModel.Purposes);
            Assert.DoesNotContain(MetadataReferencePurpose.Processor, readModel.Purposes);
            Assert.DoesNotContain(
                MetadataReferencePurpose.Integrity,
                copyReference.Purposes);
            Assert.DoesNotContain(
                MetadataReferencePurpose.Processor,
                copyReference.Purposes);
        }
    }

    /// <summary>
    /// NT51923 and NT51926 retain distinct normal-header definitions while
    /// exposing the exact workbook descriptor difference at [0x20,0x24).
    /// </summary>
    [Fact]
    public void Nt51923Nt51926RetainDistinctNormalHeaderDefinitions()
    {
        MetadataPlanEntry nt51923 = HeaderEntry(
            "NT51923",
            "nt51923-normal-tp-flash-header-read-model");
        MetadataPlanEntry nt51926 = HeaderEntry(
            "NT51926",
            "nt51926-normal-tp-flash-header-read-model");
        MetadataPlanEntry nt51923Copy = HeaderEntry(
            "NT51923",
            "nt51923-normal-tp-flash-header-copy-reference");
        MetadataPlanEntry nt51926Copy = HeaderEntry(
            "NT51926",
            "nt51926-normal-tp-flash-header-copy-reference");
        FirmwareMetadataStructureDefinition header23 =
            nt51923.StructureDefinition.Definition;
        FirmwareMetadataStructureDefinition header26 =
            nt51926.StructureDefinition.Definition;

        Assert.NotSame(header23, header26);
        Assert.Equal("nt51923-normal-tp-flash-header", header23.DefinitionId);
        Assert.Equal("nt51926-normal-tp-flash-header", header26.DefinitionId);
        Assert.Equal(25, header23.Fields.Count);
        Assert.Equal(26, header26.Fields.Count);
        Assert.All(
            [header23, header26],
            static definition =>
            {
                Assert.Equal(FirmwareMetadataStructureKind.TpFlashHeader, definition.StructureKind);
                Assert.Equal(0x100, definition.LengthBytes);
                Assert.Empty(definition.Assertions);
                FirmwareTpFlashHeaderDefinition typed =
                    Assert.IsType<FirmwareTpFlashHeaderDefinition>(
                        definition.TypedDefinition);
                Assert.Equal(
                    new ByteRange(0, 0x100),
                    Assert.Single(
                        typed.Spans,
                        static span => span.SpanId == "complete-header").Range);
                Assert.Empty(typed.FieldSeries);
            });

        Assert.Equal(
            [
                ("same-code", new ByteRange(0x20, 1)),
                ("spi-option", new ByteRange(0x21, 3)),
            ],
            header23.Fields
                .Where(static field => field.Range.Start is >= 0x20 and < 0x24)
                .Select(static field => (field.FieldId, field.Range)));
        Assert.Equal(
            [
                ("cascade-info", new ByteRange(0x20, 1)),
                ("spi-option", new ByteRange(0x21, 1)),
                ("t6-t4", new ByteRange(0x22, 2)),
            ],
            header26.Fields
                .Where(static field => field.Range.Start is >= 0x20 and < 0x24)
                .Select(static field => (field.FieldId, field.Range)));

        Assert.Equal(
            [
                ("ilm-crc-0", new ByteRange(0x18, 4)),
                ("dlm-crc-0", new ByteRange(0x1C, 4)),
                ("fw-config-crc", new ByteRange(0x3C, 4)),
                ("ctrlram-crc", new ByteRange(0x4C, 4)),
                ("mp-ctrlram-crc", new ByteRange(0x5C, 4)),
                ("header-crc", new ByteRange(0xFC, 4)),
            ],
            header23.Fields
                .Where(static field => field.FieldId.EndsWith("crc", StringComparison.Ordinal) ||
                                       field.FieldId.Contains("-crc-", StringComparison.Ordinal))
                .Select(static field => (field.FieldId, field.Range)));
        Assert.DoesNotContain(
            nt51923.FamilyDefinition.FamilyRelationships,
            static relationship => relationship is PerfectFamilyRelationship);
        Assert.DoesNotContain(
            nt51926.FamilyDefinition.FamilyRelationships,
            static relationship => relationship is PerfectFamilyRelationship);
        Assert.All(
            [nt51923, nt51926],
            static entry =>
            {
                Assert.Contains(MetadataReferencePurpose.Inspection, entry.Purposes);
                Assert.DoesNotContain(MetadataReferencePurpose.Copy, entry.Purposes);
                Assert.DoesNotContain(MetadataReferencePurpose.Integrity, entry.Purposes);
                Assert.DoesNotContain(MetadataReferencePurpose.Processor, entry.Purposes);
            });
        Assert.All(
            [nt51923Copy, nt51926Copy],
            static entry =>
            {
                Assert.Equal([MetadataReferencePurpose.Copy], entry.Purposes);
                Assert.Equal(
                    ["complete-header"],
                    entry.TargetReferences.Select(static target => target.TargetId));
                Assert.DoesNotContain(MetadataReferencePurpose.Integrity, entry.Purposes);
                Assert.DoesNotContain(MetadataReferencePurpose.Processor, entry.Purposes);
            });
    }

    private static void AssertCrcApplicability(
        FirmwareMetadataStructureDefinition definition,
        int? chipCount,
        int? activeThrough)
    {
        TopologySelection? topology = chipCount is { } count
            ? new TopologySelection(
                count,
                count == 1 ? "single" : $"{count}-ic",
                TopologySelectionSource.Requested,
                "test")
            : null;
        FirmwareResolvedMetadataField[] crcFields =
        [
            .. definition.ResolveFields(topology).Where(static field =>
                field.Field.FieldId.StartsWith("header-", StringComparison.Ordinal) &&
                (field.Field.FieldId.EndsWith("-ilm-crc", StringComparison.Ordinal) ||
                 field.Field.FieldId.EndsWith("-dlm-crc", StringComparison.Ordinal) ||
                 field.Field.FieldId.EndsWith("-header-crc", StringComparison.Ordinal))),
        ];
        Assert.Equal(12, crcFields.Length);
        foreach (FirmwareResolvedMetadataField field in crcFields)
        {
            int index = field.Field.FieldId["header-".Length] - '0';
            FirmwareMetadataFieldApplicabilityState expected = activeThrough is null
                ? FirmwareMetadataFieldApplicabilityState.Unknown
                : index <= activeThrough
                    ? FirmwareMetadataFieldApplicabilityState.Active
                    : FirmwareMetadataFieldApplicabilityState.Unused;
            Assert.Equal(expected, field.Applicability);
        }
    }

    private static MetadataPlanEntry HeaderEntry(string icId, string bindingId)
    {
        MetadataPlanDefinition plan = CreatePlan(
            BuiltInV2RegistrationRegistry.StandardMergeByIc[icId]);
        return Assert.Single(
            plan.Entries,
            entry => StringComparer.Ordinal.Equals(entry.BindingId, bindingId));
    }

    private static MetadataPlanDefinition CreatePlan(
        BuiltInV2Registration registration)
    {
        IReadOnlyList<long> capacities =
            registration.GetMapCapacities(
                out IReadOnlyList<CompositionIssue> capacityIssues);
        Assert.Empty(capacityIssues);
        return CreatePlan(registration, capacities[0]);
    }

    private static MetadataPlanDefinition CreatePlan(
        BuiltInV2Registration registration,
        long capacity,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        CompiledComposition? composition;
        IReadOnlyList<CompositionIssue> issues;
        if (selectedInputSlotIds is null)
        {
            registration.TryCompile(capacity, out composition, out issues);
        }
        else
        {
            registration.TryCompile(
                capacity,
                selectedInputSlotIds,
                out composition,
                out issues);
        }
        Assert.Empty(issues);
        return registration.CreateMetadataPlan(
            Assert.IsType<CompiledComposition>(composition));
    }
}
