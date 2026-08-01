using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;
using ResolvedFirmwareImageMap =
    NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Application.Tests.Metadata;

/// <summary>Tests the semantic FirmwareConfig projection independently from built-in profile loading.</summary>
public sealed class FirmwareConfigGeneralParametersProjectorTests
{
    private const int CapacityBytes = 0x40;
    private const int StructureLengthBytes = 23;
    private const string ArtifactBindingId = "tp-firmware";
    private const string FamilyHash =
        "abababababababababababababababababababababababababababababababab";

    private static readonly FieldDefinition[] FieldDefinitions =
    [
        new(FirmwareConfigGeneralParametersContract.TpFirmwareVersion, 0, 1, 0x5A),
        new(FirmwareConfigGeneralParametersContract.TpFirmwareVersionComplement, 1, 1, 0xA5),
        new(FirmwareConfigGeneralParametersContract.SensorCountX, 2, 1, 18),
        new(FirmwareConfigGeneralParametersContract.SensorCountY, 3, 1, 32),
        new(FirmwareConfigGeneralParametersContract.DisplayResolutionX, 4, 2, 1920),
        new(FirmwareConfigGeneralParametersContract.DisplayResolutionY, 6, 2, 1080),
        new(FirmwareConfigGeneralParametersContract.MaximumOperableFingers, 8, 1, 10),
        new(FirmwareConfigGeneralParametersContract.ReportIrqType, 9, 1, 0x10),
        new(FirmwareConfigGeneralParametersContract.TpFirmwareSubVersion, 10, 1, 3),
        new(FirmwareConfigGeneralParametersContract.TpResolutionX, 11, 2, 2880),
        new(FirmwareConfigGeneralParametersContract.TpResolutionY, 13, 2, 1800),
        new(FirmwareConfigGeneralParametersContract.ObservedIcCount, 15, 1, 3),
        new(FirmwareConfigGeneralParametersContract.OutermostIcMasterEnable, 16, 1, 1),
        new(FirmwareConfigGeneralParametersContract.CommonFirmwareMajorVersion, 17, 1, 4),
        new(FirmwareConfigGeneralParametersContract.CommonFirmwareMinorVersion, 18, 1, 5),
        new(FirmwareConfigGeneralParametersContract.CommonFirmwareAdditionalVersion, 19, 1, 6),
        new(FirmwareConfigGeneralParametersContract.Pid, 20, 2, 0x1234),
        new(FirmwareConfigGeneralParametersContract.CascadeEnable, 22, 1, 1),
    ];

    /// <summary>Every semantic projector input is required; no partial fact object may escape.</summary>
    public static TheoryData<string> RequiredFieldIds =>
        [.. FieldDefinitions.Select(static definition => definition.FieldId)];

    /// <summary>Verifies one complete decoded structure becomes the owner-approved semantic projection.</summary>
    [Fact]
    public void TryProjectCreatesCompleteSemanticFacts()
    {
        MetadataInspectionSnapshot snapshot = CreateSnapshot();

        Assert.True(FirmwareConfigGeneralParametersProjector.TryProject(
            snapshot,
            out FirmwareConfigGeneralParametersFacts facts));
        Assert.Equal((byte)0x5A, facts.TpFirmwareVersion);
        Assert.Equal((byte)0xA5, facts.TpFirmwareVersionComplement);
        Assert.True(facts.IsTpFirmwareVersionComplementValid);
        Assert.Equal((byte)18, facts.SensorCountX);
        Assert.Equal((byte)32, facts.SensorCountY);
        Assert.Equal((ushort)1920, facts.DisplayResolutionX);
        Assert.Equal((ushort)1080, facts.DisplayResolutionY);
        Assert.Equal((byte)10, facts.MaximumOperableFingers);
        Assert.Equal((byte)0x10, facts.ReportIrqType);
        Assert.Equal(FirmwareConfigReportIrqMode.LevelLow, facts.ReportIrqMode);
        Assert.Equal((byte)3, facts.TpFirmwareSubVersion);
        Assert.Equal((ushort)2880, facts.TpResolutionX);
        Assert.Equal((ushort)1800, facts.TpResolutionY);
        Assert.Equal((byte)3, facts.ObservedIcCount);
        Assert.True(facts.IsOutermostIcMasterEnableValid);
        Assert.True(facts.UseOutermostIcAsMaster);
        Assert.Equal((byte)1, facts.CascadeEnable);
        Assert.Equal("4.5.6", facts.CommonFirmwareVersion);
        Assert.Equal((ushort)0x1234, facts.Pid);
    }

    /// <summary>Verifies relation mismatch and a disabled BC_EN remain explicit facts, not decode failures.</summary>
    [Fact]
    public void TryProjectRetainsInvalidComplementAndDisabledOutermostMaster()
    {
        MetadataInspectionSnapshot snapshot = CreateSnapshot(
            validComplement: false,
            outermostIcMasterEnable: 0);

        Assert.True(FirmwareConfigGeneralParametersProjector.TryProject(
            snapshot,
            out FirmwareConfigGeneralParametersFacts facts));
        Assert.False(facts.IsTpFirmwareVersionComplementValid);
        Assert.True(facts.IsOutermostIcMasterEnableValid);
        Assert.False(facts.UseOutermostIcAsMaster);
    }

    /// <summary>All four owner-defined IRQ encodings retain their raw byte and typed meaning.</summary>
    [Theory]
    [InlineData(0x00, FirmwareConfigReportIrqMode.AttentionEdgeFalling)]
    [InlineData(0x01, FirmwareConfigReportIrqMode.AttentionEdgeRising)]
    [InlineData(0x10, FirmwareConfigReportIrqMode.LevelLow)]
    [InlineData(0x11, FirmwareConfigReportIrqMode.LevelHigh)]
    public void TryProjectMapsKnownIrqBytesWithoutDiscardingRawValue(
        byte rawValue,
        FirmwareConfigReportIrqMode expectedMode)
    {
        MetadataInspectionSnapshot snapshot = CreateSnapshot(reportIrqType: rawValue);

        Assert.True(FirmwareConfigGeneralParametersProjector.TryProject(
            snapshot,
            out FirmwareConfigGeneralParametersFacts facts));
        Assert.Equal(rawValue, facts.ReportIrqType);
        Assert.Equal(expectedMode, facts.ReportIrqMode);
    }

    /// <summary>Unknown IRQ and BC_EN values remain readable while their semantic validity is explicit.</summary>
    [Fact]
    public void TryProjectRetainsUnknownIrqAndInvalidOutermostMasterFlag()
    {
        MetadataInspectionSnapshot snapshot = CreateSnapshot(
            reportIrqType: 0xFF,
            outermostIcMasterEnable: 2);

        Assert.True(FirmwareConfigGeneralParametersProjector.TryProject(
            snapshot,
            out FirmwareConfigGeneralParametersFacts facts));
        Assert.Equal((byte)0xFF, facts.ReportIrqType);
        Assert.Null(facts.ReportIrqMode);
        Assert.Equal((byte)2, facts.OutermostIcMasterEnable);
        Assert.False(facts.IsOutermostIcMasterEnableValid);
        Assert.False(facts.UseOutermostIcAsMaster);
    }

    /// <summary>Verifies an absent relation cannot masquerade as invalid firmware bytes.</summary>
    [Fact]
    public void TryProjectRejectsMissingRequiredComplementRelation()
    {
        MetadataInspectionSnapshot snapshot = CreateSnapshot(
            includeComplementRelation: false);

        Assert.Equal(MetadataInspectionState.Value, Assert.Single(snapshot.Results).State);
        Assert.False(FirmwareConfigGeneralParametersProjector.TryProject(snapshot, out _));
    }

    /// <summary>Verifies the expected relation id cannot be rebound to unrelated canonical fields.</summary>
    [Fact]
    public void TryProjectRejectsMisboundRequiredComplementRelation()
    {
        MetadataInspectionSnapshot snapshot = CreateSnapshot(
            misbindComplementRelation: true);

        Assert.Equal(MetadataInspectionState.Value, Assert.Single(snapshot.Results).State);
        Assert.False(FirmwareConfigGeneralParametersProjector.TryProject(snapshot, out _));
    }

    /// <summary>Verifies omission of any required canonical fact rejects the whole semantic projection.</summary>
    [Theory]
    [MemberData(nameof(RequiredFieldIds))]
    public void TryProjectRejectsEveryIncompleteRequiredFactSet(string omittedFieldId)
    {
        MetadataInspectionSnapshot snapshot = CreateSnapshot(omittedFieldId);

        Assert.Equal(MetadataInspectionState.Value, Assert.Single(snapshot.Results).State);
        Assert.False(FirmwareConfigGeneralParametersProjector.TryProject(snapshot, out _));
    }

    /// <summary>Verifies absent or ambiguous canonical structure results never select an arbitrary projection.</summary>
    [Fact]
    public void TryProjectRejectsAbsentOrAmbiguousCanonicalResults()
    {
        MetadataInspectionSnapshot one = CreateSnapshot();
        MetadataInspectionResult result = Assert.Single(one.Results);
        var absent = new MetadataInspectionSnapshot(one.ResolutionToken, []);
        var ambiguous = new MetadataInspectionSnapshot(one.ResolutionToken, [result, result]);

        Assert.False(FirmwareConfigGeneralParametersProjector.TryProject(absent, out _));
        Assert.False(FirmwareConfigGeneralParametersProjector.TryProject(ambiguous, out _));
        Assert.False(FirmwareConfigGeneralParametersProjector.TryCreateDiagnostic(absent, out _));
    }

    private static MetadataInspectionSnapshot CreateSnapshot(
        string? omittedFieldId = null,
        bool validComplement = true,
        byte reportIrqType = 0x10,
        byte outermostIcMasterEnable = 1,
        bool includeComplementRelation = true,
        bool misbindComplementRelation = false)
    {
        FieldDefinition[] selectedDefinitions =
        [
            .. FieldDefinitions.Where(field =>
                !StringComparer.Ordinal.Equals(field.FieldId, omittedFieldId)),
        ];
        FirmwareMetadataField[] fields =
        [
            .. selectedDefinitions.Select(static field => new FirmwareMetadataField(
                field.FieldId,
                field.Offset,
                field.WidthBytes,
                FirmwareMetadataEncoding.UnsignedInteger,
                FirmwareMetadataByteOrder.LittleEndian)),
        ];
        bool hasVersionPair = selectedDefinitions.Any(static field =>
                field.FieldId == FirmwareConfigGeneralParametersContract.TpFirmwareVersion) &&
            selectedDefinitions.Any(static field =>
                field.FieldId ==
                FirmwareConfigGeneralParametersContract.TpFirmwareVersionComplement);
        FirmwareMetadataFieldRelation[] relations =
            hasVersionPair && includeComplementRelation
            ?
            [
                new FirmwareMetadataFieldRelation(
                    FirmwareConfigGeneralParametersContract
                        .TpFirmwareVersionComplementRelation,
                    FirmwareMetadataFieldRelationKind.BitwiseComplement,
                    misbindComplementRelation
                        ? FirmwareConfigGeneralParametersContract.SensorCountX
                        : FirmwareConfigGeneralParametersContract.TpFirmwareVersion,
                    misbindComplementRelation
                        ? FirmwareConfigGeneralParametersContract.SensorCountY
                        : FirmwareConfigGeneralParametersContract
                            .TpFirmwareVersionComplement),
            ]
            : [];
        FirmwareMetadataStructure structure = new(
            FirmwareConfigGeneralParametersContract.StructureId,
            ArtifactBindingId,
            StructureLengthBytes,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange(
                    "flash",
                    new ByteRange(0, StructureLengthBytes)),
                "flash-image"),
            fields,
            assertions: [],
            relations);
        var metadataSet = new FirmwareMetadataSet(
            "firmware-config",
            [structure],
            ["synthetic-projector-contract"]);
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT51927"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                CapacityBytes),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [
                new FirmwareRegionSet(
                    "physical",
                    "flash",
                    [
                        new FirmwareRegion(
                            "flash-image",
                            parentRegionId: null,
                            FirmwareRegionOwner.System,
                            FirmwareRegionKind.Image,
                            new ByteRange(0, CapacityBytes),
                            FirmwareWriteConstraint.Forbidden),
                    ],
                    ["synthetic-projector-contract"]),
            ],
            [metadataSet],
            ["synthetic-projector-contract"]);
        var family = new FirmwareFamilyResolutionDefinition(
            "synthetic-firmware-config",
            "1.0.0",
            FamilyHash,
            [map],
            [metadataSet]);
        byte[] artifactBytes = CreateArtifactBytes(
            validComplement,
            reportIrqType,
            outermostIcMasterEnable);
        var artifact = new FirmwareArtifactPayload(ArtifactBindingId, artifactBytes);
        ResolvedFirmwareImageMap resolvedMap = Assert.IsType<ResolvedFirmwareImageMap>(
            family.ResolveMap(new FirmwareMapResolutionInputs(
                "NT51927",
                "standard",
                CapacityBytes,
                requestedTopology: null,
                artifacts: [artifact])).ResolvedMap);
        FirmwareMapFactBinding<FirmwareMetadataSet> metadataBinding =
            Assert.Single(map.MetadataSetBindings);
        var entry = new MetadataPlanEntry(
            "firmware-config-inspection",
            ArtifactBindingId,
            ArtifactBindingId,
            family,
            resolvedMap,
            metadataBinding,
            structure,
            selectedDefinitions.Select(static field => field.FieldId),
            [MetadataReferencePurpose.Display, MetadataReferencePurpose.Version]);
        ResolvedMetadataPlan plan = new MetadataPlanDefinition([entry])
            .Resolve(new ResolutionToken("test-catalog:firmware-config"));
        return FirmwareMetadataInspector.Inspect(plan, [artifact]);
    }

    private static byte[] CreateArtifactBytes(
        bool validComplement,
        byte reportIrqType,
        byte outermostIcMasterEnable)
    {
        byte[] bytes = new byte[CapacityBytes];
        foreach (FieldDefinition field in FieldDefinitions)
        {
            ulong value = field.FieldId switch
            {
                FirmwareConfigGeneralParametersContract.TpFirmwareVersionComplement
                    when !validComplement => 0xA4,
                FirmwareConfigGeneralParametersContract.OutermostIcMasterEnable =>
                    outermostIcMasterEnable,
                FirmwareConfigGeneralParametersContract.ReportIrqType =>
                    reportIrqType,
                _ => field.Value,
            };
            for (int index = 0; index < field.WidthBytes; index++)
            {
                bytes[field.Offset + index] =
                    (byte)((value >> (index * 8)) & byte.MaxValue);
            }
        }

        return bytes;
    }

    private sealed record FieldDefinition(
        string FieldId,
        int Offset,
        int WidthBytes,
        ulong Value);
}
