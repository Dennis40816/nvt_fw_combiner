using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Convergence tests for the read-only IC metadata integration surface.</summary>
public sealed class IcMetadataFacadeTests
{
    /// <summary>Each facade row projects one onboarding entry and its canonical flash-map and postbuild facts.</summary>
    [Fact]
    public void MetadataRowsConvergeOnCanonicalCatalogs()
    {
        Assert.Equal(IcSupportCatalog.IcIds, IcMetadataFacade.IcIds);
        Assert.Equal(IcSupportCatalog.DefaultIcId, IcMetadataFacade.DefaultIcId);

        foreach (IcSupportEntry support in IcSupportCatalog.All)
        {
            Assert.True(IcMetadataFacade.TryFind(support.IcId, out IcMetadata? metadata));
            Assert.NotNull(metadata);
            Assert.Equal(support.IcId, metadata!.IcId);
            Assert.Equal(support.WorkflowIds, metadata.WorkflowIds);
            Assert.Equal(support.StandardMergeSourceIcId, metadata.StandardMergeSourceIcId);
            Assert.Equal(support.CtrlRamPostbuildSourceIcId, metadata.CtrlRamPostbuildSourceIcId);
            Assert.Equal(support.Notes, metadata.Notes);
            Assert.True(TpFlashMapCatalog.TryFind(support.IcId, out TpFlashMapProfile? flashMap));
            Assert.Equal(flashMap!.OverviewSource, metadata.TpOverviewSource);
            Assert.Equal(flashMap.FirmwareConfigStart, metadata.FirmwareConfigStart);
            Assert.Equal(TpFlashMapCatalog.GetNumberChoices(support.IcId), metadata.NumberChoices);
            Assert.Equal(
                LegacyCombinerPostbuildCatalog.GetProfiles(support.IcId)
                    .Select(profile => profile.DisplayCategory)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
                metadata.PostbuildCategories);
            Assert.Equal(DpPerspectiveCatalog.IsSupportedIc(support.IcId), metadata.UsesDpPerspective);
            Assert.Equal(
                support.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace),
                metadata.HasPostbuild);
        }
    }

    /// <summary>The facade accepts both NT-prefixed and short IC identifiers without creating another IC list.</summary>
    [Fact]
    public void MetadataLookupNormalizesIcIdentifiers()
    {
        Assert.True(IcMetadataFacade.TryFind("51926", out IcMetadata? shortId));
        Assert.True(IcMetadataFacade.TryFind("nt51926", out IcMetadata? normalizedId));

        Assert.Equal("NT51926", shortId!.IcId);
        Assert.Same(shortId, normalizedId);
        Assert.True(IcMetadataFacade.TryGetFirmwareConfigStart("51926", out long firmwareConfigStart));
        Assert.Equal(shortId.FirmwareConfigStart, firmwareConfigStart);
    }

    /// <summary>TP header labels remain owned by the shared header taxonomy rather than copied per IC row.</summary>
    [Fact]
    public void HeaderSectionTaxonomyIsSharedWithoutPerIcCopies()
    {
        Assert.Same(TpHeaderCatalog.All, IcMetadataFacade.TpHeaderSections);
        Assert.Contains(
            IcMetadataFacade.TpHeaderSections,
            section => section.SectionId == TpHeaderSectionIds.FlashHeaderCrc);
        Assert.Contains(
            IcMetadataFacade.TpHeaderSections,
            section => section.SectionId == TpHeaderSectionIds.HeaderCopy);
    }
}
