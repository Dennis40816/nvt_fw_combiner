using System.Text.Json;
using NvtFwCombiner.TestSupport;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Checks NT51925 candidate geometry and declared end-flag selection.</summary>
public sealed class Nt51925EndFlagSelectionTests
{
    /// <summary>Try Select Declared Nt51925 End Flag Selects Common Fw Slot.</summary>
    [Theory]
    [InlineData(1, 0, 0, 1)]
    [InlineData(1, 9, 9, 1)]
    [InlineData(2, 0, 0, 2)]
    [InlineData(2, 9, 9, 2)]
    public void TrySelectDeclaredNt51925EndFlagSelectsCommonFwSlot(byte major, byte minor, byte additional, int expectedSlot)
    {
        byte[] image = CreateBase(0x2FFFC, major, minor, additional);
        bool readable = FirmwareConfigMetadataReader.TryReadBackup(image,
            BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag("NT51925"), out FirmwareConfigMetadata metadata, out _);
        Assert.True(readable);
        Assert.True(BuiltInCommonFwSelector.TrySelect("NT51925", readable, metadata.CommonFwVersion,
            out BuiltInCommonFwSelection? selection, out CompositionIssue? issue));
        Assert.Null(issue);
        Assert.Equal(expectedSlot, selection!.TpFlashMap!.EffectiveCommonFwVersion.Major);
    }

    /// <summary>Try Select Marker Outside Declared End Flag Refuses Unreadable Version.</summary>
    [Theory]
    [InlineData(0x1FFC)]
    [InlineData(0x3BFFC)]
    public void TrySelectMarkerOutsideDeclaredEndFlagRefusesUnreadableVersion(int markerStart)
    {
        byte[] image = CreateBase(markerStart, 1, 9, 9);
        bool readable = FirmwareConfigMetadataReader.TryReadBackup(image,
            BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag("NT51925"), out _, out _);
        Assert.False(readable);
        Assert.False(BuiltInCommonFwSelector.TrySelect("NT51925", readable, null,
            out BuiltInCommonFwSelection? selection, out CompositionIssue? issue));
        Assert.Null(selection);
        Assert.Equal(CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnknown, issue!.Code);
    }

    /// <summary>Read Backup Extra Marker Outside Declared Range Uses Declared End Flag.</summary>
    [Fact]
    public void ReadBackupExtraMarkerOutsideDeclaredRangeUsesDeclaredEndFlag()
    {
        byte[] image = CreateBase(0x2FFFC, 2, 0, 0);
        FirmwareNvtEndFlag.MarkerBytes.CopyTo(image.AsSpan(0x3BFFC));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image,
            BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag("NT51925"), out FirmwareConfigMetadata metadata, out _));
        Assert.Equal("2.0.0", metadata.CommonFwVersion);
    }

    /// <summary>Resolve Nvt End Flag Every Candidate Map Declares Same Marker.</summary>
    [Theory]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw141-runtime-single", "nt51925-ctrlram-fw141-single-tp-work-192k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw141-runtime-single", "nt51925-ctrlram-fw141-single-full-flash-256k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw141-runtime-cascade", "nt51925-ctrlram-fw141-cascade-tp-work-192k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw141-runtime-cascade", "nt51925-ctrlram-fw141-cascade-full-flash-256k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw200-runtime-single", "nt51925-ctrlram-fw200-single-tp-work-192k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw200-runtime-single", "nt51925-ctrlram-fw200-single-full-flash-256k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw200-runtime-cascade", "nt51925-ctrlram-fw200-cascade-tp-work-192k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw200-runtime-cascade", "nt51925-ctrlram-fw200-cascade-full-flash-256k")]
    [InlineData("nt51925-standard-merge-candidate", "nt51925-standard-merge-gen-flash", "nt51925-standard-merge-256k")]
    public void ResolveNvtEndFlagEveryCandidateMapDeclaresSameMarker(string bundleId, string profileId, string mapId)
    {
        FirmwareFamilyResolutionDefinition family = BuiltInV2BundleRegistry.All[bundleId].GetFirmwareFamily(profileId, "0.1.0");
        FirmwareNvtEndFlagResolution resolution = family.ResolveNvtEndFlag(mapId);
        Assert.True(resolution.Succeeded);
        Assert.False(resolution.UsesLegacyCompatibilityRead);
        Assert.Equal(0x2FFFC, resolution.EndFlag!.Position.Range.Start);
        Assert.Equal(4, resolution.EndFlag.Position.Range.Length);
        Assert.Equal("flash", resolution.EndFlag.Position.AddressSpaceId);
    }

    /// <summary>TP Overview C101: every region, including explicit gaps, has the declared 1.x Single extent.</summary>
    [Fact]
    public void GetFirmwareFamilyFw1xSingleRegionsMatchOwnerOverview()
    {
        AssertLayoutRegions("141", "single",
        [
            .. CommonRegions(0x780, 0x77C, 0x2277C, 4),
            ("normal-ctrlram", 0x22780, 0x2800), ("mp-ctrlram", 0x24F80, 0x2000),
            ("vn-ctrlram-fw141", 0x26F80, 0x338), ("nf-ctrlram", 0x272B8, 0xD00),
            ("gap-27fb8", 0x27FB8, 0x7048), ("fw-config-backup", 0x2F000, 0x780),
            ("gap-2f780", 0x2F780, 0x87C),
        ]);
    }

    /// <summary>TP Overview C130: the 1.x Cascade owns separate slave CtrlRAM and Vec Table ranges.</summary>
    [Fact]
    public void GetFirmwareFamilyFw1xCascadeRegionsMatchOwnerOverview()
    {
        AssertLayoutRegions("141", "cascade",
        [
            .. CommonRegions(0x780, 0x77C, 0x2277C, 4),
            ("normal-ctrlram", 0x22780, 0x2800), ("mp-ctrlram", 0x24F80, 0x2000),
            ("normal-slave-ctrlram", 0x26F80, 0x2800), ("mp-slave-ctrlram", 0x29780, 0x2000),
            ("vn-ctrlram-fw141", 0x2B780, 0x338), ("nf-ctrlram", 0x2BAB8, 0x1EB0),
            ("vector-table", 0x2D968, 0x190), ("gap-2daf8", 0x2DAF8, 0x1508),
            ("fw-config-backup", 0x2F000, 0x780), ("gap-2f780", 0x2F780, 0x87C),
        ]);
    }

    /// <summary>TP Overview C68: the postbuild-only 2.0.0 Single layout leaves the DIFF interval unmapped.</summary>
    [Fact]
    public void GetFirmwareFamilyFw200SingleRegionsMatchOwnerOverview()
    {
        AssertLayoutRegions("200", "single", [.. Fw200Regions(), ("gap-27a00", 0x27A00, 0x2800)]);
    }

    /// <summary>TP Overview C68: the 2.0.0 Cascade declares DIFF at its owner range.</summary>
    [Fact]
    public void GetFirmwareFamilyFw200CascadeRegionsMatchOwnerOverview()
    {
        AssertLayoutRegions("200", "cascade", [.. Fw200Regions(), ("diff-ctrlram", 0x27A00, 0x2800)]);
    }

    /// <summary>The candidate and the independent catalog declarations cannot drift for any visible row.</summary>
    [Theory]
    [InlineData("141", "single", "1.0.0")]
    [InlineData("141", "cascade", "1.0.0")]
    [InlineData("200", "single", "2.0.0")]
    [InlineData("200", "cascade", "2.0.0")]
    public void GetFirmwareFamilyEachLayoutRegionsAgreeWithFlashMap(string fw, string topology, string version)
    {
        FirmwareImageMap map = CandidateMap(fw, topology, "full-flash-256k");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "profiles/built-in/ctrlram-postbuild-v2/flash-map.json")));
        JsonElement declared = Assert.Single(document.RootElement.GetProperty("profiles").EnumerateArray(),
            row => row.GetProperty("icId").GetString() == "NT51925" &&
                row.GetProperty("effectiveCommonFwVersion").GetString() == version);
        var regionIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["normal"] = "normal-ctrlram",
            ["mp"] = "mp-ctrlram",
            ["diff"] = "diff-ctrlram",
            ["normal-slave"] = "normal-slave-ctrlram",
            ["mp-slave"] = "mp-slave-ctrlram",
            ["vn-single"] = "vn-ctrlram-fw141",
            ["vn"] = $"vn-ctrlram-fw{fw}",
            ["nf-single"] = "nf-ctrlram",
            ["nf"] = "nf-ctrlram",
            ["header-backup"] = "header-copy",
            ["fw-config-backup"] = "fw-config-backup",
            ["customer-info"] = "full-flash-customer-info",
            ["dp"] = "full-flash-dp-code",
        };
        JsonElement[] rows = [.. declared.GetProperty("regions").EnumerateArray().Where(row =>
            row.GetProperty("visibility").GetString() == "always" ||
            row.GetProperty("visibility").GetString() == (topology == "single" ? "single-chip-only" : "multi-chip-only"))];

        Assert.Equal(declared.GetProperty("tpPrefixLength").GetInt64(),
            Assert.Single(map.Regions, static region => region.RegionId == "flash-image").Range.Length);
        Assert.All(rows, row =>
        {
            FirmwareRegion region = Assert.Single(map.Regions,
                candidate => candidate.RegionId == regionIds[row.GetProperty("regionId").GetString()!]);
            Assert.Equal(new ByteRange(row.GetProperty("start").GetInt64(), row.GetProperty("length").GetInt64()), region.Range);
        });
        Assert.Equal(rows.Where(static row => row.GetProperty("kind").GetString() == "ctrlram")
                .Select(row => regionIds[row.GetProperty("regionId").GetString()!]).Order(StringComparer.Ordinal),
            map.Regions.Where(static region => region.Kind == FirmwareRegionKind.CtrlRam)
                .Select(static region => region.RegionId).Order(StringComparer.Ordinal));
    }

    /// <summary>Every declared postbuild block uses its selected candidate's geometry.</summary>
    [Theory]
    [InlineData("141", "single", "1.0.0", "normal,mp,vn,nf,fw-config-backup")]
    [InlineData("141", "cascade", "1.0.0", "normal,mp,vn,nf,fw-config-backup")]
    [InlineData("200", "single", "2.0.0", "normal,mp,vn,nf,fw-config-backup,header-copy,header-copy-final")]
    [InlineData("200", "cascade", "2.0.0", "normal,diff,mp,vn,nf,fw-config-backup,header-copy,header-copy-final")]
    public void GetPostbuildCatalogEachLayoutCommandRangesMatchCandidateFamily(
        string fw,
        string topology,
        string version,
        string expectedBlocks)
    {
        ArgumentNullException.ThrowIfNull(expectedBlocks);
        FirmwareImageMap map = CandidateMap(fw, topology, "tp-work-192k");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "profiles/built-in/ctrlram-postbuild-v2/catalog.json")));
        JsonElement profile = Assert.Single(document.RootElement.GetProperty("profiles").EnumerateArray(),
            row => row.GetProperty("icId").GetString() == "NT51925" &&
                row.GetProperty("effectiveCommonFwVersion").GetString() == version);
        JsonElement[] blocks = [.. profile.GetProperty(topology == "single" ? "singleCommands" : "cascadeCommands")
            .EnumerateArray().SelectMany(static command => command.GetProperty("blocks").EnumerateArray())];

        Assert.Equal(
            expectedBlocks.Split(','),
            blocks.Select(static block => block.GetProperty("blockId").GetString()));
        Assert.All(blocks, block =>
        {
            string regionId = block.GetProperty("blockId").GetString() switch
            {
                "normal" => "normal-ctrlram",
                "mp" => "mp-ctrlram",
                "vn" => $"vn-ctrlram-fw{fw}",
                "nf" => "nf-ctrlram",
                "diff" => "diff-ctrlram",
                "fw-config-backup" => "fw-config-backup",
                "header-copy" or "header-copy-final" => "header-copy",
                _ => throw new InvalidOperationException("Unknown NT51925 postbuild block."),
            };
            FirmwareRegion region = Assert.Single(map.Regions, candidate => candidate.RegionId == regionId);
            Assert.Equal(new ByteRange(block.GetProperty("targetStart").GetInt64(),
                block.GetProperty("targetLength").GetInt64()), region.Range);
        });
    }

    /// <summary>Only the declared header, backup and CtrlRAM regions can be written; the end flag, reserved, unmapped, customer and DP regions cannot.</summary>
    [Theory]
    [InlineData("141", "single", "tp-work-192k", "flash-image,fw-header,header-crc-18,header-crc-1c,header-crc-3c,header-crc-4c,header-crc-5c,header-crc-fc,normal-ctrlram,mp-ctrlram,vn-ctrlram-fw141,nf-ctrlram,fw-config-backup")]
    [InlineData("141", "single", "full-flash-256k", "flash-image,fw-header,header-crc-18,header-crc-1c,header-crc-3c,header-crc-4c,header-crc-5c,header-crc-fc,normal-ctrlram,mp-ctrlram,vn-ctrlram-fw141,nf-ctrlram,fw-config-backup")]
    [InlineData("141", "cascade", "tp-work-192k", "flash-image,fw-header,header-crc-18,header-crc-1c,header-crc-3c,header-crc-4c,header-crc-5c,header-crc-fc,normal-ctrlram,mp-ctrlram,normal-slave-ctrlram,mp-slave-ctrlram,vn-ctrlram-fw141,nf-ctrlram,fw-config-backup")]
    [InlineData("141", "cascade", "full-flash-256k", "flash-image,fw-header,header-crc-18,header-crc-1c,header-crc-3c,header-crc-4c,header-crc-5c,header-crc-fc,normal-ctrlram,mp-ctrlram,normal-slave-ctrlram,mp-slave-ctrlram,vn-ctrlram-fw141,nf-ctrlram,fw-config-backup")]
    [InlineData("200", "single", "tp-work-192k", "flash-image,fw-header,header-crc-18,header-crc-1c,header-crc-3c,header-crc-4c,header-crc-5c,header-crc-fc,normal-ctrlram,mp-ctrlram,nf-ctrlram,vn-ctrlram-fw200,header-copy,fw-config-backup")]
    [InlineData("200", "single", "full-flash-256k", "flash-image,fw-header,header-crc-18,header-crc-1c,header-crc-3c,header-crc-4c,header-crc-5c,header-crc-fc,normal-ctrlram,mp-ctrlram,nf-ctrlram,vn-ctrlram-fw200,header-copy,fw-config-backup")]
    [InlineData("200", "cascade", "tp-work-192k", "flash-image,fw-header,header-crc-18,header-crc-1c,header-crc-3c,header-crc-4c,header-crc-5c,header-crc-fc,normal-ctrlram,mp-ctrlram,diff-ctrlram,nf-ctrlram,vn-ctrlram-fw200,header-copy,fw-config-backup")]
    [InlineData("200", "cascade", "full-flash-256k", "flash-image,fw-header,header-crc-18,header-crc-1c,header-crc-3c,header-crc-4c,header-crc-5c,header-crc-fc,normal-ctrlram,mp-ctrlram,diff-ctrlram,nf-ctrlram,vn-ctrlram-fw200,header-copy,fw-config-backup")]
    public void GetFirmwareFamilyEachLayoutOnlyDeclaredRegionsAreWritable(string fw, string topology, string shape, string expectedWritable)
    {
        ArgumentNullException.ThrowIfNull(expectedWritable);
        FirmwareImageMap map = CandidateMap(fw, topology, shape);

        Assert.Equal(
            expectedWritable.Split(',').Order(StringComparer.Ordinal),
            map.Regions
                .Where(static region => region.WriteConstraint != FirmwareWriteConstraint.Forbidden)
                .Select(static region => region.RegionId)
                .Order(StringComparer.Ordinal));
        Assert.All(
            map.Regions.Where(static region => region.Kind is FirmwareRegionKind.Reserved or FirmwareRegionKind.Unmapped),
            static region => Assert.Equal(FirmwareWriteConstraint.Forbidden, region.WriteConstraint));
        FirmwareRegion endFlag = Assert.Single(map.Regions, static region => region.RegionId == "end-flag");
        Assert.Equal(new ByteRange(0x2FFFC, 4), endFlag.Range);
        Assert.Equal(FirmwareRegionKind.Reserved, endFlag.Kind);
    }

    /// <summary>Only the Common FW 2.0.0 layouts declare a copied header.</summary>
    [Theory]
    [InlineData("141", "single", false)]
    [InlineData("141", "cascade", false)]
    [InlineData("200", "single", true)]
    [InlineData("200", "cascade", true)]
    public void GetFirmwareFamilyEachLayoutHeaderCopyExistsOnlyForFw200(string fw, string topology, bool expected)
    {
        Assert.Equal(expected, CandidateMap(fw, topology, "tp-work-192k").Regions.Any(static region => region.RegionId == "header-copy"));
    }

    /// <summary>Only the 1.x Cascade owns both CtrlRAM_S ranges.</summary>
    [Theory]
    [InlineData("141", "single", 0)]
    [InlineData("141", "cascade", 2)]
    [InlineData("200", "single", 0)]
    [InlineData("200", "cascade", 0)]
    public void GetFirmwareFamilyEachLayoutSlaveCtrlRamExistsOnlyForFw1xCascade(string fw, string topology, int expected)
    {
        Assert.Equal(expected, CandidateMap(fw, topology, "tp-work-192k").Regions.Count(static region =>
            region.RegionId is "normal-slave-ctrlram" or "mp-slave-ctrlram"));
    }

    /// <summary>Only the 1.x Cascade has a Vec Table.</summary>
    [Theory]
    [InlineData("141", "single", false)]
    [InlineData("141", "cascade", true)]
    [InlineData("200", "single", false)]
    [InlineData("200", "cascade", false)]
    public void GetFirmwareFamilyEachLayoutVectorTableExistsOnlyForFw1xCascade(string fw, string topology, bool expected)
    {
        Assert.Equal(expected, CandidateMap(fw, topology, "tp-work-192k").Regions.Any(static region => region.RegionId == "vector-table"));
    }

    /// <summary>Only the 2.0.0 Cascade owns DIFF.</summary>
    [Theory]
    [InlineData("141", "single", false)]
    [InlineData("141", "cascade", false)]
    [InlineData("200", "single", false)]
    [InlineData("200", "cascade", true)]
    public void GetFirmwareFamilyEachLayoutDiffExistsOnlyForFw200Cascade(string fw, string topology, bool expected)
    {
        Assert.Equal(expected, CandidateMap(fw, topology, "tp-work-192k").Regions.Any(static region => region.RegionId == "diff-ctrlram"));
    }

    /// <summary>No family or profile can retain NT51926 capacities or displaced region starts.</summary>
    [Theory]
    [InlineData("nt51925-ctrlram-replace-candidate")]
    [InlineData("nt51925-standard-merge-candidate")]
    public void ReadCandidateDataFamiliesAndProfilesNoBorrowedNt51926Geometry(string bundle)
    {
        string directory = RepositoryPaths.FromRepositoryRoot("profiles", "built-in", bundle);
        string[] files = [.. Directory.EnumerateFiles(Path.Combine(directory, "families"), "*.json")
            .Concat(Directory.EnumerateFiles(Path.Combine(directory, "profiles"), "*.json"))];
        long[] forbidden = [0x3C000, 0x3BFFC, 0x25400, 0x27800, 0x2A000, 0x2C800,
            0x2F5D0, 0x315D0, 0x32C30, 0x32F50, 0x33050, 0x3B000, 0x3B800, 0x32A6E, 0x32A70, 0x32B70, 5278, 11728];

        Assert.All(files, path =>
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            Assert.DoesNotContain(JsonScalars(document.RootElement), value =>
                value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) && forbidden.Contains(number));
            Assert.DoesNotContain(JsonScalars(document.RootElement), value => value.ValueKind == JsonValueKind.String &&
                (value.GetString()!.Contains("0x3C000", StringComparison.OrdinalIgnoreCase) ||
                 value.GetString()!.Contains("0x3BFFC", StringComparison.OrdinalIgnoreCase) ||
                 value.GetString()!.Contains("borrowed NT51926 contract", StringComparison.OrdinalIgnoreCase)));
        });
    }

    /// <summary>The Standard image partitions the owner TP prefix, explicit gap, customer data window and DP window.</summary>
    [Fact]
    public void GetFirmwareFamilyStandardMergeTpCodeLengthIs196608()
    {
        FirmwareFamilyResolutionDefinition family = BuiltInV2BundleRegistry.All["nt51925-standard-merge-candidate"]
            .GetFirmwareFamily("nt51925-standard-merge-gen-flash", "0.1.0");
        FirmwareImageMap map = Assert.Single(family.ImageMaps);
        (string Id, long Start, long Length)[] expected = [
            ("flash-image", 0, 0x40000), ("tp-code", 0, 196608),
            ("unmapped-gap", 0x30000, 0xD000), ("customer-info", 0x3D000, 0x1000), ("dp-code", 253952, 8192)];

        Assert.Equal(expected.OrderBy(static row => row.Id, StringComparer.Ordinal),
            map.Regions.Select(static region => (region.RegionId, region.Range.Start, region.Range.Length))
                .OrderBy(static row => row.RegionId, StringComparer.Ordinal));
    }

    /// <summary>Two and three ICs share Cascade geometry; other counts cannot select that map.</summary>
    [Theory]
    [InlineData("141", 1, false)]
    [InlineData("141", 2, true)]
    [InlineData("141", 3, true)]
    [InlineData("141", 4, false)]
    [InlineData("200", 1, false)]
    [InlineData("200", 2, true)]
    [InlineData("200", 3, true)]
    [InlineData("200", 4, false)]
    public void GetFirmwareFamilyCascadeLayoutOnlyTwoAndThreeIcCountsMatch(string fw, int count, bool expected)
    {
        FirmwareImageMap map = CandidateMap(fw, "cascade", "tp-work-192k");

        Assert.Equal(expected, map.Applicability.TopologyRequirement.Matches(
            new TopologySelection(count, "test", TopologySelectionSource.Requested, "ic-number")));
    }

    private static FirmwareImageMap CandidateMap(string fw, string topology, string shape)
    {
        FirmwareFamilyResolutionDefinition family = BuiltInV2BundleRegistry.All["nt51925-ctrlram-replace-candidate"]
            .GetFirmwareFamily($"nt51925-ctrlram-replace-fw{fw}-runtime-{topology}", "0.1.0");
        return Assert.Single(family.ImageMaps, map => map.MapId == $"nt51925-ctrlram-fw{fw}-{topology}-{shape}");
    }

    private static void AssertLayoutRegions(string fw, string topology, (string Id, long Start, long Length)[] expected)
    {
        Assert.Equal(expected.OrderBy(static row => row.Id, StringComparer.Ordinal),
            CandidateMap(fw, topology, "tp-work-192k").Regions
                .Select(static region => (region.RegionId, region.Range.Start, region.Range.Length))
                .OrderBy(static row => row.RegionId, StringComparer.Ordinal));
        (string Id, long Start, long Length)[] fullFlash = [.. expected,
            ("full-flash-unmapped-gap", 0x30000, 0xD000), ("full-flash-customer-info", 0x3D000, 0x1000),
            ("full-flash-dp-code", 0x3E000, 0x2000)];
        Assert.Equal(fullFlash.OrderBy(static row => row.Id, StringComparer.Ordinal),
            CandidateMap(fw, topology, "full-flash-256k").Regions
                .Select(static region => (region.RegionId, region.Range.Start, region.Range.Length))
                .OrderBy(static row => row.RegionId, StringComparer.Ordinal));
    }

    private static (string Id, long Start, long Length)[] CommonRegions(long sourceLength, long configLength, long registerStart, long registerLength)
    {
        return [
            ("flash-image", 0, 0x30000), ("fw-header", 0, 0x100),
            ("gap-prefix-00", 0, 0x18), ("header-crc-18", 0x18, 4), ("header-crc-1c", 0x1C, 4),
            ("gap-prefix-20", 0x20, 0x1C), ("header-crc-3c", 0x3C, 4), ("gap-prefix-40-to-4c", 0x40, 0xC),
            ("header-crc-4c", 0x4C, 4), ("gap-prefix-50-to-5c", 0x50, 0xC), ("header-crc-5c", 0x5C, 4),
            ("gap-prefix-60-to-fc", 0x60, 0x9C), ("header-crc-fc", 0xFC, 4), ("gap-prefix-100", 0x100, 0x100),
            ("fw-code", 0x200, 0x1FE00), ("overlay-ilm", 0x20000, 0x2000),
            ("fw-config-source", 0x22000, sourceLength), ("fw-config", 0x22000, configLength),
            ("fw-register-reserved", registerStart, registerLength), ("end-flag", 0x2FFFC, 4),
        ];
    }

    private static (string Id, long Start, long Length)[] Fw200Regions()
    {
        return [
            .. CommonRegions(0x800, 0x780, 0x22780, 0x80), ("reserved-blank", 0x22800, 0x200),
            ("normal-ctrlram", 0x22A00, 0x2C00), ("mp-ctrlram", 0x25600, 0x2400),
            ("nf-ctrlram", 0x2A200, 0x2E00), ("vn-ctrlram-fw200", 0x2D000, 0x1660),
            ("gap-2e660", 0x2E660, 0x320), ("header-copy", 0x2E980, 0x100), ("gap-2ea80", 0x2EA80, 0x580),
            ("fw-config-backup", 0x2F000, 0x800), ("gap-2f800", 0x2F800, 0x7FC),
        ];
    }

    private static IEnumerable<JsonElement> JsonScalars(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject().SelectMany(static property => JsonScalars(property.Value)),
            JsonValueKind.Array => value.EnumerateArray().SelectMany(JsonScalars),
            JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or
                JsonValueKind.Null or JsonValueKind.Undefined => [value],
            _ => throw new InvalidOperationException("Unknown JSON value kind."),
        };
    }

    private static byte[] CreateBase(int markerStart, byte major, byte minor, byte additional)
    {
        byte[] image = new byte[0x40000];
        int backupStart = markerStart - 0xFFC;
        image[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = major;
        image[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = minor;
        image[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = additional;
        FirmwareNvtEndFlag.MarkerBytes.CopyTo(image.AsSpan(markerStart));
        return image;
    }
}
