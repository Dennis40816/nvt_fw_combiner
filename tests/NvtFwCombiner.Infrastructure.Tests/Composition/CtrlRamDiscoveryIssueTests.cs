using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Infrastructure.Composition;
using NvtFwCombiner.Infrastructure.FlashMaps;
using NvtFwCombiner.Infrastructure.Tests.FlashMaps;

namespace NvtFwCombiner.Infrastructure.Tests.Composition;

public sealed class CtrlRamDiscoveryIssueTests
{
    [Fact]
    public void CreateDisplay_MarkerSlot_CarriesMarkerReason()
    {
        BuiltInTpFlashMapCatalog.LoadedCatalog loaded = TpFlashMapCatalogTestData.LoadCatalog(
            [], TpFlashMapCatalogTestData.Pending("NT00001", "1.0.0", "Owner map is pending"));
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(loaded.Profiles, loaded.PendingMaps);
        CtrlRamInspectionDisplay display = BuiltInCtrlRamAuthoringAdapter.CreateDisplay(
            "NT00001", "1", null, "1.0.0", false, catalog);
        Assert.Equal("Owner map is pending", display.Issue);
        Assert.Empty(display.Regions);
    }

    [Fact]
    public void CreateDisplay_MapSlot_LeavesIssueNull()
    {
        var profile = TpFlashMapCatalogTestData.Profile("NT00001");
        profile["regions"]![0]!["kind"] = "ctrlram";
        var catalog = new BuiltInTpFlashMapCatalog.Catalog(TpFlashMapCatalogTestData.Load(profile));
        CtrlRamInspectionDisplay display = BuiltInCtrlRamAuthoringAdapter.CreateDisplay(
            "NT00001", "1", null, "1.0.0", false, catalog);
        Assert.Null(display.Issue);
        Assert.Single(display.Regions);
    }
}
