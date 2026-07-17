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
        foreach (IcSupportEntry support in IcSupportCatalog.All)
        {
            IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = IcMetadataFacade.GetPostbuildProfiles(support.IcId);
            Assert.Equal(
                IcNumberChoicePolicy.GetNumberSelectionChoices(profiles),
                IcMetadataFacade.GetNumberSelectionChoices(support.IcId));
            Assert.Equal(
                LegacyCombinerPostbuildCatalog.GetProfiles(support.IcId),
                IcMetadataFacade.GetPostbuildProfiles(support.IcId));
            foreach (IcNumberChoice choice in IcMetadataFacade.GetNumberSelectionChoices(support.IcId))
            {
                Assert.True(IcMetadataFacade.IsNumberSelectionSupported(
                    support.IcId,
                    PostbuildSelectionTestCases.ToNumberChoiceSelection(choice.Token)));
            }
        }
    }

    /// <summary>The facade accepts both NT-prefixed and short IC identifiers without creating another IC list.</summary>
    [Fact]
    public void MetadataLookupNormalizesIcIdentifiers()
    {
        Assert.Equal(
            IcMetadataFacade.GetNumberSelectionChoices("NT51926"),
            IcMetadataFacade.GetNumberSelectionChoices("51926"));
        Assert.Equal(
            IcMetadataFacade.GetPostbuildProfiles("NT51926"),
            IcMetadataFacade.GetPostbuildProfiles("nt51926"));
    }
}
