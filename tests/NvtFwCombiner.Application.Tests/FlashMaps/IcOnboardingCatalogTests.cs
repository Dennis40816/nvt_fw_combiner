using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Cross-catalog checks for the IC onboarding entry point.</summary>
public sealed class IcOnboardingCatalogTests
{
    /// <summary>Every selectable IC must have a TP flash-map profile for workbench region display.</summary>
    [Fact]
    public void SupportedIcIdsHaveFlashMapProfiles()
    {
        foreach (string icId in IcSupportCatalog.IcIds)
        {
            Assert.True(TpFlashMapCatalog.TryFind(icId, out _), $"Missing TP flash-map profile for {icId}.");
        }
    }

    /// <summary>Every CtrlRAM-capable IC must have a structured postbuild profile.</summary>
    [Fact]
    public void CtrlRamWorkflowIcIdsHavePostbuildProfiles()
    {
        foreach (IcSupportEntry entry in IcSupportCatalog.All.Where(entry =>
                     entry.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace)))
        {
            Assert.NotEmpty(LegacyCombinerPostbuildCatalog.GetProfiles(entry.IcId));
        }
    }

    /// <summary>DP Replace exposure follows the shared DP Perspective catalog until more DP policies are approved.</summary>
    [Fact]
    public void DpReplaceWorkflowMatchesDpPerspectiveCatalog()
    {
        string[] dpReplaceIcIds =
        [
            .. IcSupportCatalog.All
                .Where(entry => entry.SupportsWorkflow(IcWorkflowIds.DpReplace))
                .Select(entry => entry.IcId)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(["NT51950", "NT51951"], dpReplaceIcIds);
        Assert.All(dpReplaceIcIds, icId => Assert.True(DpPerspectiveCatalog.IsSupportedIc(icId), icId));
    }
}
