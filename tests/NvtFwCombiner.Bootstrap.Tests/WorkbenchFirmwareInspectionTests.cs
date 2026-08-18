using System.Text;
using System.Text.RegularExpressions;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Parity and read-count evidence for the shell firmware inspection facade.</summary>
public sealed partial class FirmwareInspectionSnapshotTests
{
    /// <summary>NT51950/NT51951 recognize a plausible TP overlay in their standalone 0x37000 TP FW shape.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    [InlineData("51950")]
    [InlineData("51951")]
    public void Nt51950FamilyTpPrefixClassifiesBeforeDpContent(string icId)
    {
        byte[] tpFirmware = new byte[0x37000];
        tpFirmware[0xA000] = 0x5A;

        FirmwareInspectionSnapshot inspection = FirmwareInspectionTestSupport.InspectFirmware(
            icId,
            "tp-fw.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => tpFirmware);

        Assert.Equal(BaseFirmwareArtifactKind.TpFirmware, inspection.BaseFirmwareArtifactKind);
        Assert.Null(inspection.DpVersion);
        Assert.Null(inspection.CmiDpCode);
    }

    /// <summary>Other ICs require both declared DP and TP plausibility before claiming FlashCode.</summary>
    [Fact]
    public void OtherIcFullBaseClassifiesFromDeclaredDpContent()
    {
        byte[] tpOnly = new byte[0x40000];
        tpOnly[0x7000] = 0x3C;
        byte[] flashCode = (byte[])tpOnly.Clone();
        flashCode[0] = 0x5A;

        FirmwareInspectionSnapshot tp = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51929",
            "tp.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => tpOnly);
        FirmwareInspectionSnapshot flash = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51929",
            "flash.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => flashCode);

        Assert.Equal(BaseFirmwareArtifactKind.TpFirmware, tp.BaseFirmwareArtifactKind);
        Assert.Equal(BaseFirmwareArtifactKind.FlashCode, flash.BaseFirmwareArtifactKind);
        Assert.Null(tp.DpVersion);
        Assert.Null(tp.CmiDpCode);
        Assert.NotNull(flash.ArtifactClassification);
    }

    /// <summary>A full-length Initial Code candidate with a dummy TP range is not promoted to FlashCode.</summary>
    [Fact]
    public void FullLengthInitialCodeWithoutTpContentRemainsUnknown()
    {
        byte[] initialCode = new byte[0x40000];
        initialCode[0] = 0x5A;

        FirmwareInspectionSnapshot inspection = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51929",
            "initial-code.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => initialCode);

        Assert.Equal(BaseFirmwareArtifactKind.Unknown, inspection.BaseFirmwareArtifactKind);
        Assert.Equal(
            CompiledFirmwareArtifactKind.Unknown,
            Assert.IsType<CompiledFirmwareArtifactClassification>(inspection.ArtifactClassification).Kind);
    }

    /// <summary>NT51950 full containers require plausible Initial Code and TP overlay ranges.</summary>
    [Fact]
    public void Nt51950FullBaseClassifiesFromDeclaredDpContent()
    {
        byte[] tpOnly = new byte[0x40000];
        tpOnly[0xA000] = 0x3C;
        byte[] flashCode = (byte[])tpOnly.Clone();
        flashCode[0] = 0x5A;

        FirmwareInspectionSnapshot tp = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51950",
            "tp.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => tpOnly);
        FirmwareInspectionSnapshot flash = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51950",
            "flash.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => flashCode);

        Assert.Equal(BaseFirmwareArtifactKind.TpFirmware, tp.BaseFirmwareArtifactKind);
        Assert.Equal(BaseFirmwareArtifactKind.FlashCode, flash.BaseFirmwareArtifactKind);
    }

    /// <summary>A dynamic Standard Merge route projects DP DPCMI from its canonical metadata plan.</summary>
    [Fact]
    public void Nt51926StandardMergeInspectionProjectsCanonicalDpcmi()
    {
        string dpPath = GoldenArtifactPath("51926", "dp-input");
        string tpPath = GoldenArtifactPath("51926", "tp-input");

        FirmwareInspectionSnapshot inspection = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            dpPath,
            tpPath);

        Assert.Equal("0100", Assert.IsType<DpVersionMetadata>(inspection.DpVersion).VersionToken);
        CmiDpCodeMetadata cmi = Assert.IsType<CmiDpCodeMetadata>(inspection.CmiDpCode);
        Assert.Equal((byte)0x01, cmi.MajorVersionByte);
        Assert.Equal((byte)0x00, cmi.MinorVersionNibble);
        Assert.Equal((ushort)597, cmi.JiraNumber);
    }

    /// <summary>The consolidated snapshot preserves existing metadata and CtrlRAM display projections.</summary>
    [Fact]
    public void InspectionMatchesExistingFirmwareAndCtrlRamDisplayReaders()
    {
        string basePath = GoldenArtifactPath("51926", "expected-output");
        var ctrlRamRequest = new CtrlRamInspectionRequest(IcNumberSelectionTokens.Cascade);

        FirmwareInspectionSnapshot inspection = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            basePath,
            basePath,
            ctrlRamRequest);

        Assert.Equal(
            BuiltInFirmwareInspection.TryReadFirmwareConfigMetadata(BootstrapTestHost.Canonical, "NT51926", basePath),
            inspection.FirmwareConfig);
        Assert.Equal(
            FirmwareInspectionTestSupport.TryReadDpVersionMetadata("NT51926", basePath),
            inspection.DpVersion);
        Assert.Equal(
            FirmwareInspectionTestSupport.TryReadCmiDpCodeMetadata("NT51926", basePath, basePath),
            inspection.CmiDpCode);
        Assert.Equal(
            FirmwareInspectionTestSupport.TryReadFirmwareContextSuggestion("NT51926", basePath),
            inspection.ContextSuggestion);

        CtrlRamInspectionDisplay display = Assert.IsType<CtrlRamInspectionDisplay>(inspection.CtrlRamDisplay);
        CtrlRamInspectionDisplay expectedDisplay =
            BootstrapTestHost.Services.CtrlRamAuthoring.GetDiscoveryDisplay(
                "NT51926",
                ctrlRamRequest.NumberToken,
                basePath);
        Assert.Equal(ctrlRamRequest.NumberToken, display.NumberToken);
        Assert.Equal(expectedDisplay.Regions, display.Regions);
        AssertCtrlRamInputSlotsEqual(expectedDisplay.InputSlots, display.InputSlots);
    }

    private static void AssertCtrlRamInputSlotsEqual(
        IReadOnlyList<ReplaceInputSlot> expected,
        IReadOnlyList<ReplaceInputSlot> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int index = 0; index < expected.Count; index++)
        {
            ReplaceInputSlot expectedSlot = expected[index];
            ReplaceInputSlot actualSlot = actual[index];
            Assert.Equal(
                expectedSlot with { CtrlRamDescription = null },
                actualSlot with { CtrlRamDescription = null });

            CtrlRamInputDescriptionFacts expectedFacts = Assert.IsType<CtrlRamInputDescriptionFacts>(
                expectedSlot.CtrlRamDescription);
            CtrlRamInputDescriptionFacts actualFacts = Assert.IsType<CtrlRamInputDescriptionFacts>(
                actualSlot.CtrlRamDescription);
            Assert.Equal(expectedFacts.SourceFileName, actualFacts.SourceFileName);
            Assert.Equal(expectedFacts.RequiresDiffNfMerge, actualFacts.RequiresDiffNfMerge);
            Assert.Equal(expectedFacts.TitleStem, actualFacts.TitleStem);
            Assert.Equal(expectedFacts.IsShared, actualFacts.IsShared);
            Assert.Equal(expectedFacts.Sections, actualFacts.Sections);
        }
    }

    /// <summary>Each distinct path is read once; a missing primary prevents any secondary read.</summary>
    [Fact]
    public void InspectionReadsEachDistinctPathAtMostOnce()
    {
        byte[] baseBytes = File.ReadAllBytes(GoldenArtifactPath("51926", "expected-output"));
        byte[] dpBytes = File.ReadAllBytes(GoldenArtifactPath("51950", "dp-input", "dp-256k"));
        byte[] tpBytes = File.ReadAllBytes(GoldenArtifactPath("51950", "tp-input", "dp-256k"));
        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["base.bin"] = baseBytes,
            ["dp.bin"] = dpBytes,
            ["tp.bin"] = tpBytes,
        };
        var reads = new List<string>();
        byte[]? Read(string path)
        {
            reads.Add(path);
            return artifacts.GetValueOrDefault(path);
        }

        FirmwareInspectionSnapshot samePath = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "base.bin",
            "base.bin",
            new CtrlRamInspectionRequest(IcNumberSelectionTokens.Cascade),
            readFirmwareImage: Read);
        Assert.NotNull(samePath.FirmwareConfig);
        Assert.Equal(["base.bin"], reads);

        reads.Clear();
        FirmwareInspectionSnapshot distinctPaths = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51950",
            "dp.bin",
            "tp.bin",
            ctrlRamRequest: null,
            readFirmwareImage: Read);
        Assert.Equal("CC00", distinctPaths.DpVersion?.VersionToken);
        CmiDpCodeMetadata cmi = Assert.IsType<CmiDpCodeMetadata>(
            distinctPaths.CmiDpCode);
        Assert.Equal((byte)0xCC, cmi.MajorVersionByte);
        Assert.Equal((byte)0x00, cmi.MinorVersionNibble);
        Assert.Equal((ushort)0x0240, cmi.JiraNumber);
        Assert.Equal(0x3B016, cmi.Register16Offset);
        Assert.Equal(["dp.bin", "tp.bin"], reads);

        reads.Clear();
        FirmwareInspectionSnapshot missingPrimary = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51950",
            "missing.bin",
            "tp.bin",
            ctrlRamRequest: null,
            readFirmwareImage: Read);
        Assert.Null(missingPrimary.FirmwareConfig);
        Assert.Null(missingPrimary.DpVersion);
        Assert.Null(missingPrimary.CmiDpCode);
        Assert.Null(missingPrimary.ContextSuggestion);
        Assert.Null(missingPrimary.CtrlRamDisplay);
        Assert.Equal(["missing.bin"], reads);
    }

    /// <summary>Filename hints win over a bounded 256 KiB header scan without exposing image bytes.</summary>
    [Fact]
    public void InspectionKeepsFilenameFirstBoundedIcHintSemantics()
    {
        byte[] headerBytes = new byte[(256 * 1024) + 32];
        Encoding.ASCII.GetBytes("NT51926").CopyTo(headerBytes, 16);
        Encoding.ASCII.GetBytes("NT51929").CopyTo(headerBytes, (256 * 1024) + 8);

        FirmwareInspectionSnapshot fileNameHint = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "NT51927TT_payload.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => headerBytes);
        Assert.Equal("NT51927", fileNameHint.DetectedIcId);

        FirmwareInspectionSnapshot unreadableFileNameHint = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "NT51927_unreadable.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => null);
        Assert.Equal("NT51927", unreadableFileNameHint.DetectedIcId);
        Assert.Null(unreadableFileNameHint.FirmwareConfig);

        FirmwareInspectionSnapshot headerHint = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "payload.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => headerBytes);
        Assert.Equal("NT51926", headerHint.DetectedIcId);

        Array.Clear(headerBytes, 16, "NT51926".Length);
        FirmwareInspectionSnapshot outOfProbeHint = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "payload.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => headerBytes);
        Assert.Null(outOfProbeHint.DetectedIcId);

        Assert.DoesNotContain(
            typeof(FirmwareInspectionSnapshot).GetProperties(),
            property => property.PropertyType == typeof(byte[]) ||
                property.PropertyType == typeof(ReadOnlyMemory<byte>));
    }

    /// <summary>The bounded byte probe preserves the outgoing regex across boundary and randomized cases.</summary>
    [Fact]
    public void InspectionHeaderHintMatchesLegacyAsciiRegex()
    {
        Regex legacyMarker = LegacyFirmwareIcHintMarker();
        byte[][] craftedSamples =
        [
            [],
            Encoding.ASCII.GetBytes("51926"),
            Encoding.ASCII.GetBytes("151926"),
            Encoding.ASCII.GetBytes("519260"),
            Encoding.ASCII.GetBytes("NT51927TT"),
            Encoding.ASCII.GetBytes("151926 51928"),
            Encoding.ASCII.GetBytes("51926TT7"),
            Encoding.ASCII.GetBytes("a51923b51926"),
            [0xFF, (byte)'5', (byte)'1', (byte)'9', (byte)'3', (byte)'2', 0x80],
        ];
        foreach (byte[] sample in craftedSamples)
        {
            AssertHeaderHintMatchesLegacyRegex(sample, legacyMarker);
        }

        var random = new Random(51910);
        for (int index = 0; index < 128; index++)
        {
            byte[] sample = new byte[random.Next(0, 513)];
            random.NextBytes(sample);
            if (sample.Length >= 7 && index % 2 == 0)
            {
                int offset = random.Next(0, sample.Length - 6);
                Encoding.ASCII.GetBytes($"519{index % 100:D2}").CopyTo(sample, offset);
            }

            AssertHeaderHintMatchesLegacyRegex(sample, legacyMarker);
        }
    }

    /// <summary>A full 256 KiB probe plus its canonical snapshot does not allocate a decoded text copy.</summary>
    [Fact]
    public void InspectionHeaderHintAvoidsWholeProbeTextAllocation()
    {
        const int probeLength = 256 * 1024;
        byte[] headerBytes = new byte[probeLength];
        Encoding.ASCII.GetBytes("NT51926").CopyTo(headerBytes, probeLength - "NT51926".Length);
        _ = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "payload.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => headerBytes);

        long before = GC.GetAllocatedBytesForCurrentThread();
        FirmwareInspectionSnapshot inspection = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "payload.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => headerBytes);
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal("NT51926", inspection.DetectedIcId);
        Assert.InRange(allocatedBytes, 0, 384 * 1024);
    }

    /// <summary>FW/bar validity is independent from Common FW interval selection.</summary>
    [Fact]
    public void InspectionKeepsMultiProfileSelectionIndependentFromFirmwareVersionBar()
    {
        byte[] validBytes = File.ReadAllBytes(GoldenArtifactPath("51926", "expected-output"));
        FirmwareInspectionSnapshot validInspection = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "base.bin",
            "base.bin",
            new CtrlRamInspectionRequest(IcNumberSelectionTokens.Cascade),
            _ => validBytes);
        FirmwareConfigMetadataSnapshot validMetadata = Assert.IsType<FirmwareConfigMetadataSnapshot>(
            validInspection.FirmwareConfig);
        Assert.True(validMetadata.IsFirmwareVersionBarValid);
        Assert.NotNull(validMetadata.PostbuildCategory);

        byte[] invalidBytes = (byte[])validBytes.Clone();
        int versionBarOffset = checked(
            (int)validMetadata.FirmwareConfigBackupStart + FirmwareConfigLayout.FirmwareVersionBarOffset);
        invalidBytes[versionBarOffset] ^= 0x01;

        FirmwareInspectionSnapshot invalidInspection = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "base.bin",
            "base.bin",
            new CtrlRamInspectionRequest(IcNumberSelectionTokens.Cascade),
            _ => invalidBytes);
        FirmwareConfigMetadataSnapshot invalidMetadata = Assert.IsType<FirmwareConfigMetadataSnapshot>(
            invalidInspection.FirmwareConfig);
        Assert.False(invalidMetadata.IsFirmwareVersionBarValid);
        Assert.Equal(validMetadata.PostbuildCategory, invalidMetadata.PostbuildCategory);
        Assert.NotEmpty(Assert.IsType<CtrlRamInspectionDisplay>(invalidInspection.CtrlRamDisplay).InputSlots);
    }

    private static void AssertHeaderHintMatchesLegacyRegex(byte[] image, Regex legacyMarker)
    {
        Match expectedMatch = legacyMarker.Match(Encoding.ASCII.GetString(image));
        string? expected = expectedMatch.Success ? $"NT{expectedMatch.Groups["ic"].Value}" : null;

        FirmwareInspectionSnapshot inspection = FirmwareInspectionTestSupport.InspectFirmware(
            "NT51926",
            "payload.bin",
            tpPath: null,
            ctrlRamRequest: null,
            _ => image);

        Assert.Equal(expected, inspection.DetectedIcId);
    }

    [GeneratedRegex(@"(?<!\d)(?:NT)?(?<ic>519\d{2})(?:TT)?(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyFirmwareIcHintMarker();

    /// <summary>Paired DP/TP projections share one physical read for every distinct selected path.</summary>
    [Fact]
    public void InspectionBatchReadsEveryDistinctPathOnce()
    {
        byte[] dpBytes = File.ReadAllBytes(GoldenArtifactPath("51950", "dp-input", "dp-256k"));
        byte[] tpBytes = File.ReadAllBytes(GoldenArtifactPath("51950", "tp-input", "dp-256k"));
        var artifacts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp.bin"] = dpBytes,
            ["tp.bin"] = tpBytes,
        };
        var reads = new List<string>();

        IReadOnlyList<FirmwareInspectionSnapshotResult> results =
            BuiltInFirmwareInspection.InspectFirmwareBatch(BootstrapTestHost.Canonical,
                "NT51950",
                [
                    new FirmwareInspectionSnapshotInput("tp", "tp.bin"),
                    new FirmwareInspectionSnapshotInput("dp", "dp.bin", "tp.bin"),
                ],
                path =>
                {
                    reads.Add(path);
                    return artifacts.GetValueOrDefault(path);
                });

        Assert.Equal(["tp.bin", "dp.bin"], reads);
        Assert.Equal(2, results.Count);
        Assert.NotNull(results.Single(result => result.InspectionId == "tp").Inspection.FirmwareConfig);
        _ = Assert.NotNull(results.Single(result => result.InspectionId == "dp").Inspection.CmiDpCode);
    }

    /// <summary>Named batch projections reject ambiguous duplicate result identities.</summary>
    [Fact]
    public void InspectionBatchRejectsDuplicateIds()
    {
        int reads = 0;
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            BuiltInFirmwareInspection.InspectFirmwareBatch(BootstrapTestHost.Canonical,
                "NT51926",
                [
                    new FirmwareInspectionSnapshotInput("slot", "first.bin"),
                    new FirmwareInspectionSnapshotInput("slot", "second.bin"),
                ],
                _ =>
                {
                    reads++;
                    return null;
                }));

        Assert.Equal("inputs", exception.ParamName);
        Assert.Equal(0, reads);
    }

    /// <summary>A null batch item is rejected before any firmware reader can observe the request.</summary>
    [Fact]
    public void InspectionBatchRejectsNullItemsBeforeReading()
    {
        int reads = 0;

        _ = Assert.Throws<ArgumentNullException>(() =>
            BuiltInFirmwareInspection.InspectFirmwareBatch(BootstrapTestHost.Canonical,
                "NT51926",
                [
                    new FirmwareInspectionSnapshotInput("first", "first.bin"),
                    null!,
                ],
                _ =>
                {
                    reads++;
                    return null;
                }));

        Assert.Equal(0, reads);
    }
}
