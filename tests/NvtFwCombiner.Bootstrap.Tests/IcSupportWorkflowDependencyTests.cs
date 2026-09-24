using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Cross-owner guards that start from canonical authorable capability routes.</summary>
public sealed class CanonicalCapabilityDependencyTests
{
    /// <summary>Every selectable IC has the flash-map data needed by Merge/Replace workbench planning.</summary>
    [Fact]
    public void CanonicalIcRowsHaveFlashMapProfiles()
    {
        foreach (string icId in BootstrapTestHost.Canonical.Projection.GetIcIds())
        {
            Assert.True(
                BuiltInTpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? flashMapProfile),
                $"Missing TP flash-map profile for canonical IC {icId}.");
            Assert.NotNull(flashMapProfile);
            Assert.NotEmpty(flashMapProfile.Regions);
        }
    }

    /// <summary>Flash-map rows must be reachable through the IC support catalog instead of becoming hidden IC facts.</summary>
    [Fact]
    public void FlashMapProfilesHaveCanonicalRows()
    {
        HashSet<string> supportedIcIds = [.. BootstrapTestHost.Canonical.Projection.GetIcIds()];

        foreach (string icId in BuiltInTpFlashMapCatalog.IcIds)
        {
            Assert.Contains(icId, supportedIcIds);
        }
    }

    /// <summary>CtrlRAM Replace exposure must be backed by executable postbuild branch selections.</summary>
    [Fact]
    public void CtrlRamReplaceSupportHasPostbuildAndNumberChoiceCoverage()
    {
        foreach (string icId in GetAuthorableIcIds(ExperienceIds.CtrlRamReplace))
        {
            IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);

            Assert.NotEmpty(profiles);
            Assert.All(profiles, profile => Assert.Equal(icId, profile.IcId));

            foreach (LegacyCombinerPostbuildProfile profile in profiles)
            {
                IcNumberSelection[] selections = [.. PostbuildSelectionTestCases.GetBranchSelections(profile)];
                Assert.NotEmpty(selections);
                Assert.All(
                    selections,
                    selection => Assert.Equal(profile, profile.ResolvePlan(selection).Profile));
            }
        }
    }

    /// <summary>
    /// Every production postbuild profile must be exposed through CtrlRAM Replace support.
    /// </summary>
    [Fact]
    public void PostbuildProfilesHaveCtrlRamReplaceSupportOrExplicitBlockedRows()
    {
        HashSet<string> ctrlRamReplaceIcIds =
        [
            .. GetAuthorableIcIds(ExperienceIds.CtrlRamReplace),
        ];

        foreach (string icId in LegacyCombinerPostbuildCatalog.All
                     .Select(profile => profile.IcId)
                     .Distinct(StringComparer.Ordinal))
        {
            Assert.Contains(icId, ctrlRamReplaceIcIds);
        }

        Assert.Equal(10, ctrlRamReplaceIcIds.Count);
    }

    /// <summary>Retired DP Replace has no authorable member or exact route.</summary>
    [Fact]
    public void DpReplaceWorkflowHasNoAuthorableMembers()
    {
        Assert.Empty(GetAuthorableIcIds(ExperienceIds.DpReplace));
        foreach (string icId in BootstrapTestHost.Canonical.Projection.GetIcIds())
        {
            CapabilityWorkflowReadiness readiness = BootstrapTestHost.Canonical.Projection
                .GetReplaceWorkflowReadiness(icId, ExperienceIds.DpReplace);
            Assert.False(readiness.HasExactRoute);
            Assert.False(readiness.IsAvailable);
        }
    }

    /// <summary>Every postbuild branch staged BIN is explainable by the profile-adjusted TP Overview CtrlRAM rows.</summary>
    [Fact]
    public void PostbuildBranchesMapToProfileAdjustedCtrlRamRows()
    {
        foreach (string icId in GetAuthorableIcIds(ExperienceIds.CtrlRamReplace))
        {
            foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.GetProfiles(icId))
            {
                foreach (IcNumberSelection selection in PostbuildSelectionTestCases.GetBranchSelections(profile))
                {
                    LegacyCombinerPostbuildCommandPlan plan = profile.ResolvePlan(selection);
                    IReadOnlyList<TpFlashMapRegion> regions = BuiltInTpFlashMapCatalog.GetRegions(
                        profile.IcId,
                        selection,
                        profile,
                        TpFlashMapRegionKind.CtrlRam);

                    foreach (LegacyCombinerBlockArgument block in LegacyCombinerPostbuildPlanCompiler.GetStagedFileBlocks(plan))
                    {
                        Assert.Contains(
                            regions,
                            region => string.Equals(region.PostbuildFileName, block.SourceFileName, StringComparison.Ordinal) &&
                                      region.Range.Overlaps(block.FirmwareRange));
                    }
                }
            }
        }
    }

    private static string[] GetAuthorableIcIds(string workflowId)
    {
        CanonicalCapabilityCatalogSnapshot snapshot = BootstrapTestHost.Canonical.Catalog
            .GetCurrentSnapshot();
        return
        [
            .. snapshot.Capabilities
                .Where(capability =>
                    capability.Authoring.Value ==
                        CapabilityAuthoringAvailability.Available &&
                    StringComparer.Ordinal.Equals(
                        capability.Identity.WorkflowId,
                        workflowId))
                .Select(static capability => capability.Identity.IcId)
                .Concat(snapshot.DynamicRoutes
                    .Where(route =>
                        route.Authoring.Value ==
                            CapabilityAuthoringAvailability.Available &&
                        StringComparer.Ordinal.Equals(
                            route.Identity.WorkflowId,
                            workflowId))
                    .Select(static route => route.Identity.IcId))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

}
