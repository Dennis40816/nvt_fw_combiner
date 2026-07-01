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
