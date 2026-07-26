using System.Buffers.Binary;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Headless evidence for the one canonical FirmwareConfig General Parameters
/// definition and its NT51927/NT51928 TP-metadata pilot.
/// </summary>
public sealed class FirmwareConfigGeneralParametersPilotTests
{
    private const int TpLength = 217088;
    private const int BackupStart = 0x1000;
    private const int MarkerStart = BackupStart + 0xFFC;

    /// <summary>
    /// Both pilot routes bind the same source-backed metadata structure while
    /// retaining member-specific maps and NT51928's distinct LDC part.
    /// </summary>
    [Fact]
    public void Nt51927AndNt51928ReuseOneMetadataDefinitionButRetainDistinctMaps()
    {
        MetadataPlanEntry nt51927 = CreatePlanEntry("NT51927");
        MetadataPlanEntry nt51928 = CreatePlanEntry("NT51928");

        Assert.Equal(
            "nt51917-nt51927-nt51928-canonical-container",
            nt51927.FamilyDefinition.FamilyId);
        Assert.Equal(
            nt51927.FamilyDefinition.FamilyId,
            nt51928.FamilyDefinition.FamilyId);
        Assert.Equal(
            nt51927.FamilyDefinition.FamilyVersion,
            nt51928.FamilyDefinition.FamilyVersion);
        Assert.Equal(
            nt51927.FamilyDefinition.FamilyContentHash,
            nt51928.FamilyDefinition.FamilyContentHash);
        Assert.Equal(
            "firmware-config-general-parameters",
            nt51927.StructureDefinition.StructureId);
        Assert.Equal(
            nt51927.StructureDefinition.StructureId,
            nt51928.StructureDefinition.StructureId);
        Assert.Equal(
            nt51927.StructureDefinition.Fields.Select(FieldIdentity),
            nt51928.StructureDefinition.Fields.Select(FieldIdentity));
        Assert.Contains(
            "common-fw-ap-fwconfig-general-parameters",
            nt51927.MetadataSetBinding.Value.EvidenceRefs);

        FirmwareMetadataStructure structure = nt51927.StructureDefinition;
        Assert.Equal(0x29, structure.LengthBytes);
        Assert.Equal(36, structure.Fields.Count);
        Assert.All(structure.Fields, static field =>
            Assert.False(string.IsNullOrWhiteSpace(field.SourceName)));
        Assert.Equal(
            structure.Fields.Count,
            structure.Fields.Select(static field => field.SourceName)
                .Distinct(StringComparer.Ordinal).Count());

        long nextOffset = 0;
        foreach (FirmwareMetadataField field in
                 structure.Fields.OrderBy(static field => field.Range.Start))
        {
            Assert.Equal(nextOffset, field.Range.Start);
            Assert.Equal(FirmwareMetadataEncoding.UnsignedInteger, field.Encoding);
            Assert.Equal(FirmwareMetadataByteOrder.LittleEndian, field.ByteOrder);
            nextOffset = field.Range.EndExclusive;
        }
        Assert.Equal(0x29, nextOffset);
        Assert.Equal(ExpectedFieldDefinitions, structure.Fields.Select(FieldIdentity));
        FirmwareMetadataFieldRelation relation = Assert.Single(structure.Relations);
        Assert.Equal("firmware-version-complement", relation.RelationId);
        Assert.Equal(
            FirmwareMetadataFieldRelationKind.BitwiseComplement,
            relation.Kind);
        Assert.Equal("tp-firmware-version", relation.SourceFieldId);
        Assert.Equal(
            "tp-firmware-version-complement",
            relation.RelatedFieldId);

        FirmwareImageMap map27 = nt51927.ResolvedMap.ImageMap;
        FirmwareImageMap map28 = nt51928.ResolvedMap.ImageMap;
        Assert.Equal("nt51927-standard-merge-256k", map27.MapId);
        Assert.Equal("nt51928-standard-merge-512k", map28.MapId);
        Assert.Equal(0x40000, map27.CapacityBytes);
        Assert.Equal(0x80000, map28.CapacityBytes);
        Assert.Equal(Region(map27, "tp-code").Range, Region(map28, "tp-code").Range);
        Assert.Equal(Region(map27, "dp-code").Range, Region(map28, "dp-code").Range);
        Assert.DoesNotContain(map27.Regions, static region => region.RegionId == "ldc-code");
        Assert.Equal(new ByteRange(0x40000, 0x22000), Region(map28, "ldc-code").Range);
    }

    /// <summary>
    /// The generic inspector decodes every physical field once, projects the
    /// accepted semantic subset, and remains byte-for-byte compatible with the
    /// legacy Backup reader for overlapping facts.
    /// </summary>
    [Theory]
    [InlineData("NT51927")]
    [InlineData("NT51928")]
    public void CanonicalInspectionProjectsFactsAndMatchesLegacyBackupReader(string icId)
    {
        byte[] tp = CreateValidTp();
        MetadataInspectionSnapshot snapshot = Inspect(icId, tp);

        MetadataInspectionResult result = Assert.Single(snapshot.Results);
        Assert.Equal(MetadataInspectionState.Value, result.State);
        FirmwareResolvedMetadataStructure resolved =
            Assert.IsType<FirmwareResolvedMetadataStructure>(result.Resolution?.Resolved);
        Assert.Equal(new ByteRange(BackupStart, 0x29), resolved.LocatorOutcome.ResolvedRange.Range);
        Assert.Equal(36, resolved.DecodedStructure.Facts.Count);
        Assert.True(
            FirmwareConfigGeneralParametersProjector.TryProject(
                snapshot,
                out FirmwareConfigGeneralParametersFacts facts));

        Assert.Equal(0x42, facts.TpFirmwareVersion);
        Assert.Equal(0xBD, facts.TpFirmwareVersionComplement);
        Assert.True(facts.IsTpFirmwareVersionComplementValid);
        Assert.Equal(18, facts.SensorCountX);
        Assert.Equal(32, facts.SensorCountY);
        Assert.Equal(1920, facts.DisplayResolutionX);
        Assert.Equal(1080, facts.DisplayResolutionY);
        Assert.Equal(10, facts.MaximumOperableFingers);
        Assert.Equal(3, facts.ReportIrqType);
        Assert.Equal(7, facts.TpFirmwareSubVersion);
        Assert.Equal(4096, facts.TpResolutionX);
        Assert.Equal(2560, facts.TpResolutionY);
        Assert.Equal(3, facts.ObservedIcCount);
        Assert.Equal(1, facts.OutermostIcMasterEnable);
        Assert.True(facts.UseOutermostIcAsMaster);
        Assert.Equal("2.5.9", facts.CommonFirmwareVersion);
        Assert.Equal(0x0927, facts.Pid);

        Assert.True(
            FirmwareConfigMetadataReader.TryReadBackup(
                tp,
                out FirmwareConfigMetadata legacy));
        Assert.Equal(BackupStart, legacy.StructureStart);
        Assert.Equal(legacy.FirmwareVersion, facts.TpFirmwareVersion);
        Assert.Equal(
            legacy.FirmwareVersionBar,
            facts.TpFirmwareVersionComplement);
        Assert.Equal(
            legacy.IsFirmwareVersionBarValid,
            facts.IsTpFirmwareVersionComplementValid);
        Assert.Equal(legacy.FirmwareSubVersion, facts.TpFirmwareSubVersion);
        Assert.Equal(legacy.ChipNumber, facts.ObservedIcCount);
        Assert.Equal(
            legacy.CommonFwMajorVersion,
            facts.CommonFirmwareMajorVersion);
        Assert.Equal(
            legacy.CommonFwMinorVersion,
            facts.CommonFirmwareMinorVersion);
        Assert.Equal(
            legacy.CommonFwAdditionalVersion,
            facts.CommonFirmwareAdditionalVersion);
        Assert.Equal(legacy.ProjectId, facts.Pid);
    }

    /// <summary>A failed complement relation is reported without discarding otherwise valid facts.</summary>
    [Fact]
    public void ComplementMismatchRetainsDecodedFacts()
    {
        byte[] tp = CreateValidTp();
        tp[BackupStart + 1] = 0;

        MetadataInspectionSnapshot snapshot = Inspect("NT51927", tp);

        Assert.Equal(MetadataInspectionState.Value, Assert.Single(snapshot.Results).State);
        Assert.True(
            FirmwareConfigGeneralParametersProjector.TryProject(
                snapshot,
                out FirmwareConfigGeneralParametersFacts facts));
        Assert.False(facts.IsTpFirmwareVersionComplementValid);
        Assert.Equal(0x42, facts.TpFirmwareVersion);
        Assert.Equal(1920, facts.DisplayResolutionX);
    }

    /// <summary>Zero and multiple complete markers preserve their exact observed counts.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void MarkerCardinalityFailureReportsExactOwnerApprovedDiagnostic(
        int markerCount)
    {
        byte[] tp = CreateValidTp();
        if (markerCount == 0)
        {
            Array.Clear(tp, MarkerStart, 4);
        }
        else
        {
            WriteMarker(tp, 0x3000);
        }

        MetadataInspectionSnapshot snapshot = Inspect("NT51927", tp);

        MetadataInspectionResult blocked = Assert.Single(snapshot.Results);
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, blocked.State);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.MarkerCardinalityMismatch,
            blocked.Resolution?.Failure);
        Assert.Equal(markerCount, blocked.Resolution?.ObservedMarkerMatchCount);
        Assert.True(
            FirmwareConfigGeneralParametersProjector.TryCreateDiagnostic(
                snapshot,
                out FirmwareConfigInspectionDiagnostic diagnostic));
        Assert.Equal("firmware-config.marker-count-mismatch", diagnostic.Code);
        Assert.Equal(
            $"Expected exactly one NVT marker (00 4E 56 54), but found {markerCount}.",
            diagnostic.Message);
        Assert.False(FirmwareConfigGeneralParametersProjector.TryProject(snapshot, out _));
    }

    /// <summary>
    /// Missing and truncated TP inputs remain distinct prerequisite/range
    /// failures and never fall back to a speculative Primary address.
    /// </summary>
    [Fact]
    public void MissingAndTruncatedTpRemainDistinctFromMarkerCardinality()
    {
        ResolvedMetadataPlan plan = CreateResolvedPlan("NT51927");
        MetadataInspectionSnapshot missing = FirmwareMetadataInspector.Inspect(plan, []);
        MetadataInspectionSnapshot truncated = FirmwareMetadataInspector.Inspect(
            plan,
            [new FirmwareArtifactPayload("tp-input", new byte[0x2000])]);

        Assert.Equal(
            MetadataInspectionState.WaitingForArtifact,
            Assert.Single(missing.Results).State);
        MetadataInspectionResult blocked = Assert.Single(truncated.Results);
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, blocked.State);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.ArtifactRangeOutOfBounds,
            blocked.Resolution?.Failure);
        Assert.Null(blocked.Resolution?.ObservedMarkerMatchCount);
        Assert.False(
            FirmwareConfigGeneralParametersProjector.TryCreateDiagnostic(
                truncated,
                out _));
    }

    /// <summary>A unique marker whose T - 0xFFF result is invalid stays a typed range failure.</summary>
    [Fact]
    public void UniqueMarkerWithInvalidBackupRangeDoesNotBecomeCountFailure()
    {
        byte[] tp = new byte[TpLength];
        WriteMarker(tp, 0x100);

        MetadataInspectionSnapshot snapshot = Inspect("NT51927", tp);

        MetadataInspectionResult blocked = Assert.Single(snapshot.Results);
        Assert.Equal(MetadataInspectionState.BlockedByArtifact, blocked.State);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.ResolvedRangeOutOfBounds,
            blocked.Resolution?.Failure);
        Assert.Null(blocked.Resolution?.ObservedMarkerMatchCount);
        Assert.False(
            FirmwareConfigGeneralParametersProjector.TryCreateDiagnostic(
                snapshot,
                out _));
    }

    private static MetadataInspectionSnapshot Inspect(string icId, byte[] tp)
    {
        return FirmwareMetadataInspector.Inspect(
            CreateResolvedPlan(icId),
            [new FirmwareArtifactPayload("tp-input", tp)]);
    }

    private static ResolvedMetadataPlan CreateResolvedPlan(string icId)
    {
        MetadataPlanEntry entry = CreatePlanEntry(icId);
        return new MetadataPlanDefinition([entry]).Resolve(
            new ResolutionToken($"fwconfig-pilot:{icId}"));
    }

    private static MetadataPlanEntry CreatePlanEntry(string icId)
    {
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc[icId];
        registration.TryCompile(
            inputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        CompiledComposition compiled = Assert.IsType<CompiledComposition>(composition);
        return Assert.Single(registration.CreateMetadataPlan(compiled).Entries);
    }

    private static byte[] CreateValidTp()
    {
        byte[] tp = new byte[TpLength];
        Span<byte> config = tp.AsSpan(BackupStart, 0x29);
        config[0] = 0x42;
        config[1] = 0xBD;
        config[2] = 18;
        config[3] = 32;
        BinaryPrimitives.WriteUInt16LittleEndian(config[4..], 1920);
        BinaryPrimitives.WriteUInt16LittleEndian(config[6..], 1080);
        config[8] = 4;
        config[9] = 10;
        config[10] = 2;
        config[11] = 3;
        config[12] = 1;
        config[13] = 5;
        config[14] = 6;
        config[15] = 20;
        config[16] = 34;
        config[17] = 7;
        BinaryPrimitives.WriteUInt16LittleEndian(config[18..], 4096);
        BinaryPrimitives.WriteUInt16LittleEndian(config[20..], 2560);
        config[22] = 1;
        config[23] = 3;
        config[24] = 1;
        config[25] = 8;
        config[26] = 2;
        config[27] = 5;
        config[28] = 9;
        config[29] = 0x11;
        config[30] = 0x22;
        config[31] = 0x33;
        config[32] = 0x44;
        config[33] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(config[34..], 0x0927);
        config[36] = 4;
        config[37] = 5;
        config[38] = 6;
        config[39] = 0x12;
        config[40] = 0x34;
        WriteMarker(tp, MarkerStart);
        return tp;
    }

    private static void WriteMarker(Span<byte> bytes, int start)
    {
        bytes[start] = 0x00;
        bytes[start + 1] = (byte)'N';
        bytes[start + 2] = (byte)'V';
        bytes[start + 3] = (byte)'T';
    }

    private static FirmwareRegion Region(FirmwareImageMap map, string regionId)
    {
        return map.Regions.Single(region =>
            StringComparer.Ordinal.Equals(region.RegionId, regionId));
    }

    private static (string FieldId, string? SourceName, long Offset, int Width)
        FieldIdentity(FirmwareMetadataField field)
    {
        return (field.FieldId, field.SourceName, field.Range.Start, field.WidthBytes);
    }

    private static readonly (
        string FieldId,
        string? SourceName,
        long Offset,
        int Width)[] ExpectedFieldDefinitions =
    [
        ("tp-firmware-version", "u8FWVersion", 0x00, 1),
        ("tp-firmware-version-complement", "u8FWVersionBar", 0x01, 1),
        ("sensor-count-x", "u8AlgNumberX", 0x02, 1),
        ("sensor-count-y", "u8AlgNumberY", 0x03, 1),
        ("display-resolution-x", "u16LCMResolutionX", 0x04, 2),
        ("display-resolution-y", "u16LCMResolutionY", 0x06, 2),
        ("common-firmware-format-version", "u8CommonFwFormatVersion", 0x08, 1),
        ("maximum-operable-fingers", "u8ReportFingerNum", 0x09, 1),
        ("button-count", "u8ButtonNum", 0x0A, 1),
        ("report-irq-type", "u8IRQ_Type", 0x0B, 1),
        ("event-buffer-format-version", "u8EventBufferFormatVersion", 0x0C, 1),
        ("customized-function", "u8CustomizedFunction", 0x0D, 1),
        ("customer-tuning-version", "u8CustomerTuningVersion", 0x0E, 1),
        ("hardware-sensor-count-x", "u8HWNumberX", 0x0F, 1),
        ("hardware-sensor-count-y", "u8HWNumberY", 0x10, 1),
        ("tp-firmware-subversion", "u8FWSubVersion", 0x11, 1),
        ("tp-resolution-x", "u16TPResolutionX", 0x12, 2),
        ("tp-resolution-y", "u16TPResolutionY", 0x14, 2),
        ("report-by-command-buffer", "u8ReportByComBuf", 0x16, 1),
        ("observed-ic-count", "u8Chip_Num", 0x17, 1),
        ("outermost-ic-master-enable", "u8BC_EN", 0x18, 1),
        ("maximum-buffer-count", "u8MaxBufferNum", 0x19, 1),
        ("common-firmware-major-version", "u8CommonFwMajorVersion", 0x1A, 1),
        ("common-firmware-minor-version", "u8CommonFwMinorVersion", 0x1B, 1),
        ("common-firmware-additional-version", "u8CommonFwAdditionalVersion", 0x1C, 1),
        ("auto-build-svn-byte-1", "u8AutoBuildSvnVer1", 0x1D, 1),
        ("auto-build-svn-byte-2", "u8AutoBuildSvnVer2", 0x1E, 1),
        ("auto-build-svn-byte-3", "u8AutoBuildSvnVer3", 0x1F, 1),
        ("auto-build-svn-byte-4", "u8AutoBuildSvnVer4", 0x20, 1),
        ("cascade-enable", "u8CascadeEn", 0x21, 1),
        ("pid", "u16NovaTekProjectID", 0x22, 2),
        ("x-one-dimensional-count", "u8X1DNum", 0x24, 1),
        ("y-one-dimensional-count", "u8Y1DNum", 0x25, 1),
        ("afe-count", "u8AFE_Num", 0x26, 1),
        ("event-buffer-high-byte", "u8EventBufHbyte", 0x27, 1),
        ("event-buffer-middle-byte", "u8EventBufMbyte", 0x28, 1),
    ];
}
