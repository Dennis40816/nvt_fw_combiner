using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;
using ResolvedFirmwareImageMap =
    NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Application.Tests.Metadata;

/// <summary>Tests the read-only TP SVN projection independently from built-in profile loading.</summary>
public sealed class TpSvnMetadataProjectorTests
{
    private const int CapacityBytes = 0x40;
    private const string SpaceId = "tp-input";
    private const string FamilyHash =
        "cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd";

    /// <summary>Owner stamps decode into flags, the BCD revision of bytes 1-3 and one anomaly.</summary>
    [Theory]
    [InlineData(new byte[] { 0xC0, 0x19, 0x49, 0x14 }, TpSvnBuildOrigin.LocalBuild | TpSvnBuildOrigin.DiffExist, 0x00, "194914", TpSvnAnomaly.None)]
    [InlineData(new byte[] { 0x60, 0x18, 0x31, 0x51 }, TpSvnBuildOrigin.DiffExist | TpSvnBuildOrigin.NoSvnRecord, 0x00, "183151", TpSvnAnomaly.None)]
    [InlineData(new byte[] { 0xC3, 0x80, 0x13, 0x30 }, TpSvnBuildOrigin.LocalBuild | TpSvnBuildOrigin.DiffExist, 0x03, "801330", TpSvnAnomaly.UndefinedFlagBits)]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 }, TpSvnBuildOrigin.None, 0x00, "000000", TpSvnAnomaly.NotStamped)]
    [InlineData(new byte[] { 0xC2, 0x1A, 0x2B, 0x3C }, TpSvnBuildOrigin.LocalBuild | TpSvnBuildOrigin.DiffExist, 0x02, null, TpSvnAnomaly.UndefinedFlagBits)]
    public void ReadsTheDisplayBoundStampFromTheSameCapture(
        byte[] stamp, TpSvnBuildOrigin flags, byte undefinedBits, string? revision, TpSvnAnomaly anomaly)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        byte[] image = CreateImage(0x24, stamp);

        TpSvnObservation observation = Assert.IsType<TpSvnObservation>(
            TpSvnMetadataProjector.Read(CreatePlan(0x24), SpaceId, image));

        Assert.Equal(stamp, observation.RawBytes.ToArray());
        Assert.Equal(new FirmwareAddressedRange("flash", new ByteRange(0x24, 4)), observation.Range);
        Assert.Equal(flags, observation.Flags);
        Assert.Equal(undefinedBits, observation.UndefinedFlagBits);
        Assert.Equal(revision, observation.RevisionDigits);
        Assert.Equal(anomaly, observation.Anomaly);
    }

    /// <summary>
    /// Synthetic, not Golden: every legal high-bit combination, each undefined bit, leading zeros and a non-BCD
    /// nibble in each revision byte; the revision is always bytes 1-3 in storage order and never uses byte 0.
    /// </summary>
    [Theory]
    [InlineData(0x00, 0x12, 0x34, 0x56, "123456", TpSvnAnomaly.None)]
    [InlineData(0x20, 0x00, 0x00, 0x00, "000000", TpSvnAnomaly.None)]
    [InlineData(0x40, 0x00, 0x01, 0x23, "000123", TpSvnAnomaly.None)]
    [InlineData(0x60, 0x18, 0x31, 0x51, "183151", TpSvnAnomaly.None)]
    [InlineData(0x80, 0x99, 0x99, 0x99, "999999", TpSvnAnomaly.None)]
    [InlineData(0xA0, 0x01, 0x00, 0x00, "010000", TpSvnAnomaly.None)]
    [InlineData(0xC0, 0x20, 0x45, 0x78, "204578", TpSvnAnomaly.None)]
    [InlineData(0xE0, 0x19, 0x49, 0x14, "194914", TpSvnAnomaly.None)]
    [InlineData(0xC1, 0x80, 0x13, 0x30, "801330", TpSvnAnomaly.UndefinedFlagBits)]
    [InlineData(0xC2, 0x80, 0x13, 0x30, "801330", TpSvnAnomaly.UndefinedFlagBits)]
    [InlineData(0xC4, 0x80, 0x13, 0x30, "801330", TpSvnAnomaly.UndefinedFlagBits)]
    [InlineData(0xC8, 0x80, 0x13, 0x30, "801330", TpSvnAnomaly.UndefinedFlagBits)]
    [InlineData(0xD0, 0x80, 0x13, 0x30, "801330", TpSvnAnomaly.UndefinedFlagBits)]
    [InlineData(0x1F, 0x00, 0x00, 0x00, "000000", TpSvnAnomaly.UndefinedFlagBits)]
    [InlineData(0xC0, 0x1A, 0x00, 0x00, null, TpSvnAnomaly.None)]
    [InlineData(0xC0, 0x00, 0xA0, 0x00, null, TpSvnAnomaly.None)]
    [InlineData(0xC0, 0x00, 0x00, 0x0F, null, TpSvnAnomaly.None)]
    public void SyntheticStampsDecodeOnlyTheOwnerVocabulary(
        byte flagByte, byte first, byte second, byte third, string? revision, TpSvnAnomaly anomaly)
    {
        var observation = new TpSvnObservation(
            TpSvnMetadataContract.DefinitionId,
            new FirmwareAddressedRange("flash", new ByteRange(0x24, 4)),
            [flagByte, first, second, third]);

        Assert.Equal((TpSvnBuildOrigin)(flagByte & 0xE0), observation.Flags);
        Assert.Equal((byte)(flagByte & 0x1F), observation.UndefinedFlagBits);
        Assert.Equal(revision, observation.RevisionDigits);
        Assert.Equal(anomaly, observation.Anomaly);
    }

    /// <summary>Owner flag names keep their verbatim spelling, most significant bit first.</summary>
    [Fact]
    public void FlagNamesFollowTheOwnerVocabulary()
    {
        Assert.Equal(["LOCAL_BUILD", "DIFF_EXIST", "NO_SVN_RECORD"], TpSvnMetadataContract.GetFlagNames(
            TpSvnBuildOrigin.NoSvnRecord | TpSvnBuildOrigin.DiffExist | TpSvnBuildOrigin.LocalBuild));
        Assert.Empty(TpSvnMetadataContract.GetFlagNames(TpSvnBuildOrigin.None));
    }

    /// <summary>Only a display-bound declaration of this space and capture size grants a value.</summary>
    [Fact]
    public void UndeclaredOrUnauthorizedStampsYieldNoValue()
    {
        byte[] image = CreateImage(0x24, [0xC0, 0x19, 0x49, 0x14]);

        Assert.Null(TpSvnMetadataProjector.Read(CreatePlan(0x24, [MetadataReferencePurpose.Inspection]), SpaceId, image));
        Assert.Null(TpSvnMetadataProjector.Read(CreatePlan(0x24), "dp-input", image));
        Assert.Null(TpSvnMetadataProjector.Read(CreatePlan(0x24), SpaceId, new byte[CapacityBytes + 1]));
        Assert.Null(TpSvnMetadataProjector.Read(CreatePlan(0x24), SpaceId, new byte[0x26]));
    }

    /// <summary>TP-only consensus publishes a stamp only when every candidate resolves the same bytes and range.</summary>
    [Fact]
    public void ConsensusRequiresIdenticalStamps()
    {
        byte[] image = CreateImage(0x24, [0xC0, 0x19, 0x49, 0x14]);

        Assert.NotNull(TpSvnMetadataProjector.ReadConsensus([CreatePlan(0x24), CreatePlan(0x24)], SpaceId, image));
        Assert.Null(TpSvnMetadataProjector.ReadConsensus([CreatePlan(0x24), CreatePlan(0x28)], SpaceId, image));
    }

    private static byte[] CreateImage(int offset, byte[] stamp)
    {
        byte[] image = new byte[CapacityBytes];
        stamp.CopyTo(image, offset);
        return image;
    }

    private static ResolvedMetadataPlan CreatePlan(
        long offset, MetadataReferencePurpose[]? purposes = null)
    {
        FirmwareMetadataStructure structure = new(
            TpSvnMetadataContract.DefinitionId,
            SpaceId,
            4,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(offset, 4)),
                "tp-code"),
            [
                new FirmwareMetadataField(TpSvnMetadataContract.FlagsFieldId, 0, 1, FirmwareMetadataEncoding.Bytes),
                new FirmwareMetadataField(TpSvnMetadataContract.RevisionFieldId, 1, 3, FirmwareMetadataEncoding.Bytes),
            ],
            assertions: [],
            relations: []);
        var metadataSet = new FirmwareMetadataSet("tp-svn-metadata", [structure], ["synthetic-tp-svn-contract"]);
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT51923"],
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
                            "tp-code",
                            parentRegionId: null,
                            FirmwareRegionOwner.Tp,
                            FirmwareRegionKind.Code,
                            new ByteRange(0, CapacityBytes),
                            FirmwareWriteConstraint.Forbidden),
                    ],
                    ["synthetic-tp-svn-contract"]),
            ],
            [metadataSet],
            ["synthetic-tp-svn-contract"]);
        var family = new FirmwareFamilyResolutionDefinition("synthetic-tp-svn", "1.0.0", FamilyHash, [map], [metadataSet]);
        ResolvedFirmwareImageMap resolvedMap = Assert.IsType<ResolvedFirmwareImageMap>(
            family.ResolveMap(new FirmwareMapResolutionInputs(
                "NT51923", "standard", CapacityBytes, requestedTopology: null,
                artifacts: [new FirmwareArtifactPayload(SpaceId, new byte[CapacityBytes])])).ResolvedMap);
        var entry = new MetadataPlanEntry(
            "tp-svn-display",
            SpaceId,
            SpaceId,
            family,
            resolvedMap,
            Assert.Single(map.MetadataSetBindings),
            structure,
            [TpSvnMetadataContract.FlagsFieldId, TpSvnMetadataContract.RevisionFieldId],
            purposes ?? [MetadataReferencePurpose.Display]);
        return new MetadataPlanDefinition([entry]).Resolve(new ResolutionToken("test-catalog:tp-svn"));
    }
}
