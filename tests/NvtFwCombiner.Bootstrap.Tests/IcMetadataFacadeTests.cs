using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Convergence tests for the read-only IC metadata integration surface.</summary>
public sealed class IcMetadataFacadeTests
{
    /// <summary>The facade delegates selection facts without caching a second joined IC model.</summary>
    [Fact]
    public void SelectionFactsConvergeOnCanonicalCatalogs()
    {
        Assert.Equal(IcSupportCatalog.IcIds, IcMetadataFacade.IcIds);
        Assert.Equal(IcSupportCatalog.DefaultIcId, IcMetadataFacade.DefaultIcId);

        foreach (IcSupportEntry support in IcSupportCatalog.All)
        {
            IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = IcMetadataFacade.GetPostbuildProfiles(support.IcId);
            Assert.True(IcMetadataFacade.IsKnown(support.IcId));
            Assert.Equal(IcNumberChoicePolicy.GetNumberChoices(profiles), IcMetadataFacade.GetNumberChoices(support.IcId));
            Assert.Equal(
                IcNumberChoicePolicy.GetNumberSelectionChoices(profiles),
                IcMetadataFacade.GetNumberSelectionChoices(support.IcId));
            Assert.Equal(
                LegacyCombinerPostbuildCatalog.GetProfiles(support.IcId),
                IcMetadataFacade.GetPostbuildProfiles(support.IcId));
            Assert.Equal(
                support.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace),
                IcMetadataFacade.SupportsCtrlRamReplace(support.IcId));
        }
    }

    /// <summary>The facade accepts both NT-prefixed and short IC identifiers without creating another IC list.</summary>
    [Fact]
    public void MetadataLookupNormalizesIcIdentifiers()
    {
        Assert.True(IcMetadataFacade.IsKnown("51926"));
        Assert.True(IcMetadataFacade.IsKnown("nt51926"));
        Assert.Equal(
            IcMetadataFacade.GetNumberChoices("NT51926"),
            IcMetadataFacade.GetNumberChoices("51926"));
    }
}
