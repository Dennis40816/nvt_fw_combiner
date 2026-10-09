using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Infrastructure.Composition;
using NvtFwCombiner.Infrastructure.Tests.FlashMaps;

namespace NvtFwCombiner.Infrastructure.Tests.Composition;

/// <summary>Tests ctrl ram discovery issue tests.</summary>
public sealed class CtrlRamDiscoveryIssueTests
{
    /// <summary>Create Display Marker Slot Carries Marker Reason.</summary>
    [Fact]
    public void CreateDisplayMarkerSlotCarriesMarkerReason()
    {
        BuiltInTpFlashMapCatalog.LoadedCatalog loaded = TpFlashMapCatalogTestData.LoadCatalog(
            [], TpFlashMapCatalogTestData.Pending("NT00001", "1.0.0", "Owner map is pending"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(loaded.Profiles, loaded.PendingMaps);
        CtrlRamInspectionDisplay display = BuiltInCtrlRamAuthoringAdapter.CreateDisplay(
            "NT00001", "1", null, "1.0.0", false, catalog);
        Assert.Equal("Owner map is pending", display.Issue);
        Assert.Empty(display.Regions);
    }

    /// <summary>Create Display Map Slot Leaves Issue Null.</summary>
    [Fact]
    public void CreateDisplayMapSlotLeavesIssueNull()
    {
        JsonObject profile = TpFlashMapCatalogTestData.Profile("NT00001");
        profile["regions"]![0]!["kind"] = "ctrlram";
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(TpFlashMapCatalogTestData.Load(profile));
        CtrlRamInspectionDisplay display = BuiltInCtrlRamAuthoringAdapter.CreateDisplay(
            "NT00001", "1", null, "1.0.0", false, catalog);
        Assert.Null(display.Issue);
        _ = Assert.Single(display.Regions);
    }
}
