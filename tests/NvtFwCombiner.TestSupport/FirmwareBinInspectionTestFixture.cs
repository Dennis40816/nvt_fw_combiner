using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using ResolvedFirmwareImageMap =
    NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.TestSupport;

/// <summary>Declarative unsigned field used by the real metadata-inspection test fixture.</summary>
public sealed record FirmwareBinInspectionFieldFixture(
    string FieldId,
    string DisplayName,
    long Offset,
    int WidthBytes);

/// <summary>Declarative absolute structure used by the real metadata-inspection test fixture.</summary>
public sealed record FirmwareBinInspectionStructureFixture(
    string BindingId,
    ByteRange Range,
    IReadOnlyList<FirmwareBinInspectionFieldFixture> Fields);

/// <summary>Builds a real formatter-rooted BIN snapshot without fabricating formatted metadata DTOs.</summary>
public static class FirmwareBinInspectionTestFixture
{
    private const string ArtifactId = "test-bin-input";
    private const string FamilyHash =
        "abababababababababababababababababababababababababababababababab";

    /// <summary>Resolves, inspects, formats, and identity-binds the supplied immutable artifact bytes.</summary>
    public static FirmwareBinInspectionSnapshot Create(
        byte[] artifactBytes,
        IEnumerable<FirmwareBinInspectionStructureFixture> structureFixtures,
        long authoringRevision = 7)
    {
        ArgumentNullException.ThrowIfNull(artifactBytes);
        ArgumentNullException.ThrowIfNull(structureFixtures);
        FirmwareBinInspectionStructureFixture[] fixtures = [.. structureFixtures];
        FirmwareMetadataStructure[] structures =
        [
            .. fixtures.Select(CreateStructure),
        ];
        var metadataSet = new FirmwareMetadataSet(
            "test-bin-metadata",
            structures,
            ["test-bin-evidence"]);
        var regionSet = new FirmwareRegionSet(
            "test-bin-regions",
            "flash",
            [
                new FirmwareRegion(
                    "image",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, artifactBytes.LongLength),
                    FirmwareWriteConstraint.Forbidden),
            ],
            ["test-bin-evidence"]);
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "test-bin-map",
            "flash",
            new FirmwareMapApplicability(
                ["NT51929"],
                ["test-bin-inspection"],
                TopologyRequirement.NoTopologyConstraint(),
                artifactBytes.LongLength),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [regionSet],
            [metadataSet],
            ["test-bin-evidence"]);
        var family = new FirmwareFamilyResolutionDefinition(
            "test-bin-family",
            "1.0.0",
            FamilyHash,
            [map],
            [metadataSet]);
        var artifact = new FirmwareArtifactPayload(ArtifactId, artifactBytes);
        ResolvedFirmwareImageMap resolvedMap = family.ResolveMap(new FirmwareMapResolutionInputs(
            "NT51929",
            "test-bin-inspection",
            artifactBytes.LongLength,
            requestedTopology: null,
            [artifact])).ResolvedMap!;
        FirmwareMapFactBinding<FirmwareMetadataSet> metadataBinding = AssertSingle(map.MetadataSetBindings);
        var planDefinition = new MetadataPlanDefinition(fixtures.Select((fixture, index) =>
            new MetadataPlanEntry(
                fixture.BindingId,
                ArtifactId,
                ArtifactId,
                family,
                resolvedMap,
                metadataBinding,
                structures[index],
                fixture.Fields.Select(static field => field.FieldId),
                [MetadataReferencePurpose.Display])));
        ResolvedMetadataPlan plan = planDefinition.Resolve(new ResolutionToken("test-bin-catalog:1"));
        MetadataInspectionSnapshot inspected = FirmwareMetadataInspector.Inspect(
            new MetadataInspectionRequest(
                plan,
                authoringRevision,
                [artifact]));
        return FirmwareBinInspectionSnapshot.Create(
            inspected,
            [new FirmwareBinInspectionArtifact(ArtifactId, artifactBytes)]);
    }

    private static FirmwareMetadataStructure CreateStructure(
        FirmwareBinInspectionStructureFixture fixture)
    {
        return new FirmwareMetadataStructure(
            fixture.BindingId,
            ArtifactId,
            fixture.Range.Length,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", fixture.Range),
                "image"),
            fixture.Fields.Select(field => new FirmwareMetadataField(
                field.FieldId,
                field.Offset,
                field.WidthBytes,
                FirmwareMetadataEncoding.UnsignedInteger,
                FirmwareMetadataByteOrder.LittleEndian,
                sourceName: field.DisplayName)),
            assertions: []);
    }

    private static T AssertSingle<T>(IReadOnlyList<T> values)
    {
        return values.Count == 1
            ? values[0]
            : throw new InvalidOperationException("The test fixture expected exactly one value.");
    }
}
