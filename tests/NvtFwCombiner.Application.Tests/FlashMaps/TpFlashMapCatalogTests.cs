using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Executable checks for the TP Overview-derived production flash-map catalog.</summary>
public sealed class TpFlashMapCatalogTests
{
    /// <summary>Every postbuild-backed IC must have a TP flash-map profile.</summary>
    [Fact]
    public void CatalogCoversAllPostbuildIcIds()
    {
        foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.All)
        {
            Assert.True(
                TpFlashMapCatalog.TryFind(profile.IcId, out TpFlashMapProfile? flashMapProfile),
                $"Missing flash-map profile for {profile.IcId}.");
            Assert.NotNull(flashMapProfile);
        }
    }

    /// <summary>NT51927 numeric selections expose the expected master/right/left CtrlRAM rows.</summary>
    [Fact]
    public void Nt51927CtrlRamRowsFollowNumericIcCount()
    {
        IReadOnlyList<TpFlashMapRegion> single = TpFlashMapCatalog.GetCtrlRamRegions(
            "NT51927",
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["1"]));
        IReadOnlyList<TpFlashMapRegion> twoChip = TpFlashMapCatalog.GetCtrlRamRegions(
            "NT51927",
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]));
        IReadOnlyList<TpFlashMapRegion> threeChip = TpFlashMapCatalog.GetCtrlRamRegions(
            "NT51927",
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));

        Assert.DoesNotContain(single, region => region.DisplayName.Contains("Slave", StringComparison.Ordinal));
        Assert.Contains(twoChip, region => region.RegionId == "normal-slave-r");
        Assert.DoesNotContain(twoChip, region => region.RegionId == "normal-slave-l");
        Assert.Contains(threeChip, region => region.RegionId == "normal-slave-l");
    }

    /// <summary>NT51927 three-chip postbuild slots include both right and left slave CtrlRAM regions.</summary>
    [Fact]
    public void Nt51927PostbuildMappedCtrlRamRowsIncludeRightAndLeftSlaves()
    {
        IReadOnlyList<TpFlashMapRegion> mapped = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(
            "NT51927",
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));

        Assert.Contains(mapped, region => region.RegionId == "normal-slave-r");
        Assert.Contains(mapped, region => region.RegionId == "normal-slave-l");
        Assert.Contains(mapped, region => region.RegionId == "mp-slave-r");
        Assert.Contains(mapped, region => region.RegionId == "mp-slave-l");
    }

    /// <summary>Single-chip selections hide DIFF/DLM rows while cascade selections expose them.</summary>
    [Fact]
    public void SingleSelectionHidesDiffDlmRows()
    {
        IReadOnlyList<TpFlashMapRegion> single = TpFlashMapCatalog.GetCtrlRamRegions(
            "NT51950",
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
        IReadOnlyList<TpFlashMapRegion> cascade = TpFlashMapCatalog.GetCtrlRamRegions(
            "NT51950",
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.DoesNotContain(single, region => region.RegionId == "diff");
        Assert.Contains(cascade, region => region.RegionId == "diff");
    }

    /// <summary>General region lookups use the same IC-count visibility policy as CtrlRAM rows.</summary>
    [Fact]
    public void RegionLookupAppliesNumberVisibilityAcrossKinds()
    {
        IReadOnlyList<TpFlashMapRegion> singleDpRegions = TpFlashMapCatalog.GetRegions(
            "NT51950",
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
            TpFlashMapRegionKind.Dp);
        IReadOnlyList<TpFlashMapRegion> twoChipDpRegions = TpFlashMapCatalog.GetRegions(
            "NT51950",
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]),
            TpFlashMapRegionKind.Dp);

        Assert.DoesNotContain(singleDpRegions, region => region.RegionId == "dp-2ic-only");
        Assert.Contains(twoChipDpRegions, region => region.RegionId == "dp-2ic-only");
        Assert.Contains(twoChipDpRegions, region => region.RegionId == "dp-ldc-51951");
    }

    /// <summary>NT51950/NT51951 retain the owner-confirmed customer-info preserve window.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void Nt51950BasedProfilesDeclareCustomerInformationPreserveRegion(string icId)
    {
        Assert.True(TpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? profile));

        TpFlashMapRegion region = Assert.Single(
            profile!.Regions,
            candidate => candidate.Kind == TpFlashMapRegionKind.CustomerInfo &&
                         candidate.RegionId == "customer-info");

        Assert.Equal(new ByteRange(0x37000, 0x1000), region.Range);
        Assert.Contains("preserve", region.Tags);
    }

    /// <summary>FWConfig primary starts are explicit profile facts used for metadata display.</summary>
    [Theory]
    [InlineData("NT51917", 0x16000)]
    [InlineData("NT51919", 0x1F200)]
    [InlineData("NT51920", 0x22000)]
    [InlineData("NT51923", 0x22000)]
    [InlineData("NT51926", 0x22000)]
    [InlineData("NT51927", 0x16000)]
    [InlineData("NT51928", 0x16000)]
    [InlineData("NT51929", 0x1F200)]
    [InlineData("NT51930", 0x1F200)]
    [InlineData("NT51931", 0x16000)]
    [InlineData("NT51932", 0x1F200)]
    [InlineData("NT51950", 0x22200)]
    [InlineData("NT51951", 0x22200)]
    public void FirmwareConfigStartComesFromFlashMapReference(string icId, long expectedStart)
    {
        Assert.True(TpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? profile));
        Assert.True(TpFlashMapCatalog.TryGetFirmwareConfigStart(icId, out long start));

        Assert.Equal(expectedStart, profile!.FirmwareConfigStart);
        Assert.Equal(expectedStart, start);
    }

    /// <summary>TP Overview backup rows used by postbuild traceability are declared explicitly.</summary>
    [Theory]
    [InlineData("NT51920", "fw-config-backup", 0x2F000, 0x00780)]
    [InlineData("NT51926", "fw-config-backup", 0x3B000, 0x00780)]
    [InlineData("NT51927", "header-backup", 0x32DC0, 0x00460)]
    [InlineData("NT51927", "fw-config-reg-backup", 0x34000, 0x00800)]
    [InlineData("NT51931", "fw-config-backup", 0x3B000, 0x00800)]
    public void BackupRowsFromTpOverviewAreDeclared(string icId, string regionId, long start, long length)
    {
        Assert.True(TpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? profile));

        TpFlashMapRegion region = Assert.Single(profile!.Regions, candidate => candidate.RegionId == regionId);

        Assert.Equal(TpFlashMapRegionKind.Other, region.Kind);
        Assert.Equal(new ByteRange(start, length), region.Range);
        Assert.Contains("backup", region.Tags);
        Assert.Contains("postbuild", region.Tags);
    }

    /// <summary>Rows adjacent to the TP end flag are cataloged as protected traceability rows.</summary>
    [Theory]
    [InlineData("NT51920", "fw-config-backup", 0x2F000, 0x00780, "backup")]
    [InlineData("NT51923", "fw-config-backup", 0x3B000, 0x00800, "fw-config")]
    [InlineData("NT51927", "fw-config-reg-backup", 0x34000, 0x00800, "backup")]
    [InlineData("NT51929", "fw-information", 0x3F000, 0x00FFC, "fw-information")]
    [InlineData("NT51930", "fw-information-host", 0x3F000, 0x00FFC, "fw-information")]
    [InlineData("NT51931", "fw-config-backup", 0x3B000, 0x00800, "backup")]
    [InlineData("NT51950", "fw-information-host", 0x36000, 0x00FFC, "fw-information")]
    [InlineData("NT51951", "fw-information-host", 0x36000, 0x00FFC, "fw-information")]
    public void EndFlagAdjacentRowsFromTpOverviewAreDeclared(
        string icId,
        string regionId,
        long start,
        long length,
        string expectedTag)
    {
        Assert.True(TpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? profile));

        TpFlashMapRegion region = Assert.Single(profile!.Regions, candidate => candidate.RegionId == regionId);

        Assert.Equal(TpFlashMapRegionKind.Other, region.Kind);
        Assert.Equal(new ByteRange(start, length), region.Range);
        Assert.Contains(expectedTag, region.Tags);
    }

    /// <summary>NT51923 keeps the workbook label distinct from postbuild's fw-config-backup block id.</summary>
    [Fact]
    public void Nt51923FwConfigKeepsWorkbookLabel()
    {
        Assert.True(TpFlashMapCatalog.TryFind("NT51923", out TpFlashMapProfile? profile));

        TpFlashMapRegion region = Assert.Single(profile!.Regions, candidate => candidate.RegionId == "fw-config-backup");

        Assert.Equal("FW Config", region.DisplayName);
        Assert.DoesNotContain("backup", region.Tags);
        Assert.Contains("workbook-label-fw-config", region.Tags);
    }

    /// <summary>Every staged postbuild CtrlRAM block must overlap a TP Overview CtrlRAM row with the same BIN name.</summary>
    [Fact]
    public void PostbuildStagedBlocksMapToTpOverviewCtrlRamRows()
    {
        foreach ((LegacyCombinerPostbuildProfile profile, IcNumberSelection selection) in AllPostbuildSelections())
        {
            LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(profile, selection);
            IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetCtrlRamRegions(profile.IcId, selection);
            foreach (LegacyCombinerBlockArgument block in LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan))
            {
                Assert.Contains(
                    regions,
                    region => string.Equals(region.PostbuildFileName, block.SourceFileName, StringComparison.Ordinal) &&
                              region.Range.Overlaps(block.FirmwareRange));
            }
        }
    }

    /// <summary>Overview-only rows remain visible even when postbuild does not currently consume a separate BIN.</summary>
    [Fact]
    public void Nt51930MpCtrlRamIsVisibleAsOverviewOnly()
    {
        IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetCtrlRamRegions(
            "NT51930",
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));
        IReadOnlyList<TpFlashMapRegion> postbuildMapped = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(
            "NT51930",
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Contains(regions, region => region.RegionId == "mp" && region.Tags.Contains("overview-only"));
        Assert.DoesNotContain(postbuildMapped, region => region.RegionId == "mp");
    }

    /// <summary>NT51926 CtrlRAM rows follow the selected Common FW postbuild category.</summary>
    [Fact]
    public void Nt51926PostbuildCategoryOverridesVersionedLengths()
    {
        var selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]);

        IReadOnlyList<TpFlashMapRegion> commonFw141Regions = TpFlashMapCatalog.GetRegions(
            "NT51926",
            selection,
            LegacyCombinerPostbuildCatalog.Nt51926CommonFw141);
        IReadOnlyList<TpFlashMapRegion> commonFw200Regions = TpFlashMapCatalog.GetRegions(
            "NT51926",
            selection,
            LegacyCombinerPostbuildCatalog.Nt51926);

        Assert.Equal(new ByteRange(0x315D0, 0x1660), commonFw141Regions.Single(region => region.RegionId == "vn").Range);
        Assert.Equal(
            new ByteRange(0x3B000, 0x800),
            commonFw141Regions.Single(region => region.RegionId == "fw-config-backup").Range);
        Assert.Equal(new ByteRange(0x315D0, 0x149E), commonFw200Regions.Single(region => region.RegionId == "vn").Range);
        Assert.Equal(
            new ByteRange(0x3B000, 0x780),
            commonFw200Regions.Single(region => region.RegionId == "fw-config-backup").Range);
    }

    /// <summary>NT51930 Common FW 1.x consumes MP CtrlRAM while 2.0.0 keeps MP overview-only.</summary>
    [Fact]
    public void Nt51930PostbuildCategoryControlsMpConsumption()
    {
        var selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]);

        IReadOnlyList<TpFlashMapRegion> commonFw1xMapped = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(
            "NT51930",
            selection,
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x);
        IReadOnlyList<TpFlashMapRegion> commonFw200Mapped = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(
            "NT51930",
            selection,
            LegacyCombinerPostbuildCatalog.Nt51930);
        IReadOnlyList<TpFlashMapRegion> commonFw1xExtendedMapped = TpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(
            "NT51930",
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["14"]),
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x);

        Assert.Contains(commonFw1xMapped, region => region.RegionId == "mp" && region.Range == new ByteRange(0x24250, 0x3400));
        Assert.Contains(commonFw1xMapped, region => region.RegionId == "vn" && region.Range == new ByteRange(0x27650, 0x195E));
        Assert.Contains(commonFw1xExtendedMapped, region => region.RegionId == "diff" && region.Range == new ByteRange(0x2F200, 0x23000));
        Assert.DoesNotContain(commonFw200Mapped, region => region.RegionId == "mp");
        Assert.Contains(commonFw200Mapped, region => region.RegionId == "vn" && region.Range == new ByteRange(0x27650, 0x1960));
    }

    /// <summary>NT51930 exposes numeric choices because Common FW 1.x has an extended cascade branch.</summary>
    [Fact]
    public void Nt51930NumberChoicesExposeExtendedCascadeCounts()
    {
        IReadOnlyList<string> choices = TpFlashMapCatalog.GetNumberChoices("NT51930");

        Assert.Contains("single", choices);
        Assert.Contains("13", choices);
        Assert.Contains("14", choices);
        Assert.Contains("29", choices);
        Assert.DoesNotContain("cascade", choices);
    }

    private static IEnumerable<(LegacyCombinerPostbuildProfile Profile, IcNumberSelection Selection)> AllPostbuildSelections()
    {
        foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.All)
        {
            yield return (profile, new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
            yield return (profile, new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));
            if (profile.TwoChipCommands is not null)
            {
                yield return (profile, new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]));
            }

            if (profile.ThreeChipCommands is not null)
            {
                yield return (profile, new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));
            }
        }
    }
}
