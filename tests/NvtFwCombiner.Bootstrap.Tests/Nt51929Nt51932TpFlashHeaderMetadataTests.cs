using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Pins the approved NT51929/NT51932 Type-AB TP Flash Header provider and its
/// standard/AB read-only instances.
/// </summary>
public sealed class Nt51929Nt51932TpFlashHeaderMetadataTests
{
    /// <summary>
    /// NT51929 and NT51932 share one physical definition, while NT51919 does
    /// not inherit it merely because the profiles are packaged together.
    /// </summary>
    [Fact]
    public void StandardProfilesShareHeaderDefinitionOnlyForApprovedMembers()
    {
        MetadataPlanDefinition nt51919 = CreatePlan(
            BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51919"]);
        MetadataPlanDefinition nt51929 = CreatePlan(
            BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51929"]);
        MetadataPlanDefinition nt51932 = CreatePlan(
            BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51932"]);

        Assert.Empty(nt51919.Entries);
        MetadataPlanEntry header29 = ReadModel(nt51929, "type-ab-tp-flash-header-read-model");
        MetadataPlanEntry header32 = ReadModel(nt51932, "type-ab-tp-flash-header-read-model");

        Assert.Same(
            header29.StructureDefinition.Definition,
            header32.StructureDefinition.Definition);
        Assert.Equal(
            FirmwareMetadataStructureKind.TpFlashHeader,
            header29.StructureDefinition.Definition.StructureKind);
        Assert.Equal("type-ab-tp-flash-header", header29.StructureDefinition.Definition.DefinitionId);
        Assert.Equal(0x100, header29.StructureDefinition.Definition.LengthBytes);
        Assert.Equal(22, header29.StructureDefinition.Definition.Fields.Count);
        Assert.Empty(header29.StructureDefinition.Definition.Assertions);
        FirmwareTpFlashHeaderDefinition typed =
            Assert.IsType<FirmwareTpFlashHeaderDefinition>(
                header29.StructureDefinition.Definition.TypedDefinition);
        AssertStoredAddress(
            typed,
            "ilm-destination-address-in-sram",
            "sram",
            TpFlashHeaderStoredAddressBasis.Absolute);
        AssertStoredAddress(
            typed,
            "dlm-destination-address-in-sram",
            "sram",
            TpFlashHeaderStoredAddressBasis.Absolute);
        AssertStoredAddress(
            typed,
            "dlm-diff-destination-address-in-sram",
            "sram",
            TpFlashHeaderStoredAddressBasis.Absolute);
        AssertStoredAddress(
            typed,
            "ilm-start-address-in-bin",
            "tp-bin",
            TpFlashHeaderStoredAddressBasis.TpBinOffset);
        AssertStoredAddress(
            typed,
            "dlm-start-address-in-bin",
            "tp-bin",
            TpFlashHeaderStoredAddressBasis.TpBinOffset);
        AssertStoredAddress(
            typed,
            "dlm-diff-start-address-in-bin",
            "tp-bin",
            TpFlashHeaderStoredAddressBasis.TpBinOffset);
        Assert.All(
            [header29, header32],
            static entry =>
            {
                Assert.Equal("tp-input", entry.SpaceId);
                Assert.Equal("tp-input", entry.SlotId);
                FirmwareRegionRelativeLocator locator =
                    Assert.IsType<FirmwareRegionRelativeLocator>(
                        entry.StructureDefinition.Locator);
                Assert.Equal("tp-code", locator.RegionId);
                Assert.Equal(0x100, locator.Offset);
                Assert.Equal("tp-code", locator.AllowedResultRegionId);
            });
    }

    /// <summary>
    /// The deployed provider pins the complete owner table, every IC Count
    /// applicability row, and the reference-only consumer boundary.
    /// </summary>
    [Fact]
    public void ProviderPinsCompleteOwnerTableAndReadOnlyConsumerClosure()
    {
        BuiltInV2Registration standardRegistration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51929"];
        BuiltInV2Registration abRegistration =
            BuiltInV2RegistrationRegistry.AbMergeByIc["NT51929"];
        MetadataPlanDefinition standard = CreatePlan(standardRegistration);
        MetadataPlanDefinition ab = CreatePlan(
            abRegistration,
            inputLength: 0x80000);
        FirmwareMetadataStructureDefinition definition =
            ReadModel(standard, "type-ab-tp-flash-header-read-model")
                .StructureDefinition.Definition;

        Assert.Equal(
            new (string Id, long Start, long Length)[]
            {
                ("header-crc", 0x00, 4),
                ("ilm-destination-address-in-sram", 0x04, 4),
                ("ilm-size", 0x08, 4),
                ("ilm-crc-0", 0x0C, 4),
                ("dlm-destination-address-in-sram", 0x10, 4),
                ("dlm-size", 0x14, 4),
                ("dlm-crc-0", 0x18, 4),
                ("dlm-diff-destination-address-in-sram", 0x1C, 4),
                ("dlm-diff-size", 0x20, 2),
                ("build-read-command", 0x24, 1),
                ("build-divider-count", 0x25, 1),
                ("spi-option", 0x26, 1),
                ("dlm-crc-1", 0x28, 4),
                ("dlm-crc-2", 0x2C, 4),
                ("dlm-crc-3", 0x30, 4),
                ("dlm-crc-4", 0x34, 4),
                ("dlm-crc-5", 0x38, 4),
                ("dlm-crc-6", 0x3C, 4),
                ("dlm-crc-7", 0x40, 4),
                ("ilm-start-address-in-bin", 0x64, 4),
                ("dlm-start-address-in-bin", 0x68, 4),
                ("dlm-diff-start-address-in-bin", 0x6C, 4),
            },
            definition.Fields.Select(static field =>
                (field.FieldId, field.Range.Start, field.Range.Length)));

        FirmwareTpFlashHeaderDefinition typed =
            Assert.IsType<FirmwareTpFlashHeaderDefinition>(
                definition.TypedDefinition);
        var spans = typed.Spans.ToDictionary(
            static span => span.SpanId,
            static span => span.Range,
            StringComparer.Ordinal);
        Assert.Equal(new ByteRange(0x00, 0x100), spans["complete-header"]);
        Assert.Equal(new ByteRange(0x00, 0x44), spans["descriptor-table"]);
        Assert.Equal(
            new ByteRange(0x64, 0x0C),
            spans["bank-relative-start-addresses"]);

        FirmwareMetadataFieldSeries series = Assert.Single(typed.FieldSeries);
        Assert.Equal("dlm-crc-series", series.SeriesId);
        Assert.Equal(
            Enumerable.Range(0, 8).Select(static index =>
                (index, $"dlm-crc-{index}")),
            series.Members.Select(static member =>
                (member.Index, member.FieldId)));
        Assert.Equal(Enumerable.Range(1, 8), series.Applicability.Select(
            static row => row.ChipCount));
        Assert.All(
            series.Applicability,
            static row => Assert.Equal(
                Enumerable.Range(0, row.ChipCount),
                row.ActiveIndices));

        Assert.Contains(
            standard.Entries,
            static entry =>
                entry.Purposes.Contains(MetadataReferencePurpose.Copy) &&
                entry.TargetReferences.Contains(
                    new FirmwareMetadataReferenceTarget(
                        FirmwareMetadataReferenceTargetKind.Span,
                        "complete-header")));
        MetadataPlanEntry relocation = Assert.Single(
            ab.Entries,
            static entry =>
                entry.Purposes.Contains(MetadataReferencePurpose.Relocation));
        Assert.Equal(
            [
                new FirmwareMetadataReferenceTarget(
                    FirmwareMetadataReferenceTargetKind.Group,
                    "tp-bank-relative-start-addresses"),
            ],
            relocation.TargetReferences);
        Assert.All(
            standard.Entries.Concat(ab.Entries),
            static entry =>
            {
                Assert.DoesNotContain(
                    MetadataReferencePurpose.Integrity,
                    entry.Purposes);
                Assert.DoesNotContain(
                    MetadataReferencePurpose.Processor,
                    entry.Purposes);
            });

        CompiledComposition abComposition = Compile(
            abRegistration,
            inputLength: 0x80000);
        Assert.DoesNotContain(
            abComposition.Plan.OrderedOperations,
            static operation =>
                operation.Kind == CompositionOperationKind.RunExternalProcessor);
        Assert.Equal(
            [0x7164L, 0x7168L, 0x716CL],
            abComposition.Plan.OrderedOperations
                .Where(static operation =>
                    operation.Kind == CompositionOperationKind.TransformScalar)
                .Select(static operation => operation.TargetRange.Start)
                .Order());

        MetadataInspectionSnapshot inspected = FirmwareMetadataInspector.Inspect(
            standard.Resolve(new ResolutionToken("tp-header-test:1")),
            [
                new FirmwareArtifactPayload(
                    "tp-input",
                    new byte[0x7200]),
            ]);
        FirmwareResolvedMetadataStructure resolvedHeader = Assert.IsType<
            FirmwareResolvedMetadataStructure>(
            Assert.Single(
                inspected.Results,
                static result => StringComparer.Ordinal.Equals(
                    result.PlanEntry.Definition.BindingId,
                    "type-ab-tp-flash-header-read-model"))
                .Resolution?.Resolved);
        Assert.All(
            resolvedHeader.Fields.Where(static field =>
                field.Field.FieldId.StartsWith(
                    "dlm-crc-",
                    StringComparison.Ordinal)),
            static field => Assert.Equal(
                FirmwareMetadataFieldApplicabilityState.Unknown,
                field.Applicability));
        Assert.All(
            resolvedHeader.Fields.Where(static field =>
                !field.Field.FieldId.StartsWith(
                    "dlm-crc-",
                    StringComparison.Ordinal)),
            static field => Assert.Equal(
                FirmwareMetadataFieldApplicabilityState.Active,
                field.Applicability));
    }

    /// <summary>
    /// TPA and TPB retain distinct artifact instances but exact-reference the
    /// same provider definition at the unshifted source offset 0x7100.
    /// </summary>
    [Theory]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void AbProfilesReferenceOneProviderAtUnshiftedSourceOffset(string icId)
    {
        MetadataPlanDefinition standard = CreatePlan(
            BuiltInV2RegistrationRegistry.StandardMergeByIc[icId]);
        MetadataPlanDefinition ab = CreatePlan(
            BuiltInV2RegistrationRegistry.AbMergeByIc[icId],
            inputLength: 0x80000);

        FirmwareMetadataStructureDefinition provider =
            ReadModel(standard, "type-ab-tp-flash-header-read-model")
                .StructureDefinition.Definition;
        MetadataPlanEntry tpa = ReadModel(
            ab,
            "type-ab-tpa-tp-flash-header-read-model");
        MetadataPlanEntry tpb = ReadModel(
            ab,
            "type-ab-tpb-tp-flash-header-read-model");

        Assert.Same(provider, tpa.StructureDefinition.Definition);
        Assert.Same(provider, tpb.StructureDefinition.Definition);
        Assert.NotSame(tpa.StructureDefinition, tpb.StructureDefinition);
        Assert.Equal("tp-a-input", tpa.SpaceId);
        Assert.Equal("tp-b-input", tpb.SpaceId);
        AssertUnshiftedSourceLocator(tpa.StructureDefinition);
        AssertUnshiftedSourceLocator(tpb.StructureDefinition);
    }

    /// <summary>NT51919 AB also remains free of the unevidenced TP Header definition.</summary>
    [Fact]
    public void Nt51919AbDoesNotAcquireTpHeaderFromFamilyPackaging()
    {
        MetadataPlanDefinition plan = CreatePlan(
            BuiltInV2RegistrationRegistry.AbMergeByIc["NT51919"],
            inputLength: 0x80000);

        Assert.Empty(plan.Entries);
    }

    private static MetadataPlanDefinition CreatePlan(
        BuiltInV2Registration registration,
        long? inputLength = null)
    {
        return registration.CreateMetadataPlan(
            Compile(registration, inputLength));
    }

    private static CompiledComposition Compile(
        BuiltInV2Registration registration,
        long? inputLength = null)
    {
        registration.TryCompile(
            inputLength,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        return Assert.IsType<CompiledComposition>(composition);
    }

    private static MetadataPlanEntry ReadModel(
        MetadataPlanDefinition plan,
        string bindingId)
    {
        return Assert.Single(
            plan.Entries,
            entry => StringComparer.Ordinal.Equals(entry.BindingId, bindingId));
    }

    private static void AssertUnshiftedSourceLocator(
        FirmwareMetadataStructure structure)
    {
        FirmwareAbsoluteRangeLocator locator =
            Assert.IsType<FirmwareAbsoluteRangeLocator>(structure.Locator);
        Assert.Equal("flash", locator.Range.AddressSpaceId);
        Assert.Equal(0x7100, locator.Range.Range.Start);
        Assert.Equal(0x100, locator.Range.Range.Length);
        Assert.Equal("tpa-code", locator.AllowedResultRegionId);
    }

    private static void AssertStoredAddress(
        FirmwareTpFlashHeaderDefinition header,
        string fieldId,
        string addressSpaceId,
        TpFlashHeaderStoredAddressBasis basis)
    {
        FirmwareTpFlashHeaderStoredAddressSemantics stored =
            Assert.IsType<FirmwareTpFlashHeaderStoredAddressSemantics>(
                header.FieldSemantics.Single(semantics =>
                    StringComparer.Ordinal.Equals(
                        semantics.FieldId,
                        fieldId)).StoredAddress);
        Assert.Equal(addressSpaceId, stored.AddressSpaceId);
        Assert.Equal(basis, stored.Basis);
    }
}
