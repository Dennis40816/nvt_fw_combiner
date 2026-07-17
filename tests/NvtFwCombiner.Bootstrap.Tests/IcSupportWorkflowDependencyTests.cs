using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Cross-catalog guards that start from the IC onboarding support catalog.</summary>
public sealed class IcSupportWorkflowDependencyTests
{
    /// <summary>Every selectable IC has the flash-map data needed by Merge/Replace workbench planning.</summary>
    [Fact]
    public void IcSupportRowsHaveFlashMapProfiles()
    {
        foreach (IcSupportEntry entry in IcSupportCatalog.All)
        {
            Assert.True(
                BuiltInTpFlashMapCatalog.TryFind(entry.IcId, out TpFlashMapProfile? flashMapProfile),
                $"Missing TP flash-map profile for support catalog IC {entry.IcId}.");
            Assert.NotNull(flashMapProfile);
            Assert.NotEmpty(flashMapProfile.Regions);
        }
    }

    /// <summary>Flash-map rows must be reachable through the IC support catalog instead of becoming hidden IC facts.</summary>
    [Fact]
    public void FlashMapProfilesHaveIcSupportRows()
    {
        HashSet<string> supportedIcIds = [.. IcSupportCatalog.IcIds];

        foreach (string icId in BuiltInTpFlashMapCatalog.IcIds)
        {
            Assert.Contains(icId, supportedIcIds);
        }
    }

    /// <summary>CtrlRAM Replace exposure must be backed by executable postbuild branch selections.</summary>
    [Fact]
    public void CtrlRamReplaceSupportHasPostbuildAndNumberChoiceCoverage()
    {
        foreach (IcSupportEntry entry in IcSupportCatalog.All.Where(entry =>
            entry.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace)))
        {
            IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(entry.IcId);

            Assert.NotEmpty(profiles);
            Assert.All(profiles, profile => Assert.Equal(entry.IcId, profile.IcId));

            foreach (LegacyCombinerPostbuildProfile profile in profiles)
            {
                IcNumberSelection[] selections = [.. PostbuildSelectionTestCases.GetBranchSelections(profile)];
                Assert.NotEmpty(selections);
                Assert.All(
                    selections,
                    selection => Assert.Equal(profile, LegacyCombinerPostbuildPlanner.CreatePlan(profile, selection).Profile));
            }
        }
    }

    /// <summary>
    /// Postbuild profiles must be exposed through CtrlRAM Replace support unless the owner-facing support catalog
    /// explicitly closes every Replace workflow while retaining the profile as failure evidence.
    /// </summary>
    [Fact]
    public void PostbuildProfilesHaveCtrlRamReplaceSupportOrExplicitBlockedRows()
    {
        string[] evidenceOnlyIcIds = ["NT51931"];
        HashSet<string> ctrlRamReplaceIcIds =
        [
            .. IcSupportCatalog.All
                .Where(entry => entry.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace))
                .Select(entry => entry.IcId),
        ];

        foreach (string icId in LegacyCombinerPostbuildCatalog.All
                     .Select(profile => profile.IcId)
                     .Distinct(StringComparer.Ordinal))
        {
            if (evidenceOnlyIcIds.Contains(icId, StringComparer.Ordinal))
            {
                Assert.True(IcSupportCatalog.TryFind(icId, out IcSupportEntry? blockedEntry));
                Assert.NotNull(blockedEntry);
                Assert.False(blockedEntry.SupportsWorkflow(IcWorkflowIds.DpReplace));
                Assert.False(blockedEntry.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace));
                Assert.False(blockedEntry.SupportsWorkflow(IcWorkflowIds.GeneralReplace));
                Assert.Contains("Not Supported", blockedEntry.Notes, StringComparison.Ordinal);
                continue;
            }

            Assert.Contains(icId, ctrlRamReplaceIcIds);
        }
    }

    /// <summary>DP Replace exposure stays closed to the two members with trusted V2 runtime registrations.</summary>
    [Fact]
    public void DpReplaceWorkflowRemainsClosedToV2RegisteredMembers()
    {
        string[] dpReplaceIcIds =
        [
            .. IcSupportCatalog.All
                .Where(entry => entry.SupportsWorkflow(IcWorkflowIds.DpReplace))
                .Select(entry => entry.IcId)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(["NT51950", "NT51951"], dpReplaceIcIds);
    }

    /// <summary>Every postbuild branch staged BIN is explainable by the profile-adjusted TP Overview CtrlRAM rows.</summary>
    [Fact]
    public void PostbuildBranchesMapToProfileAdjustedCtrlRamRows()
    {
        foreach (IcSupportEntry entry in IcSupportCatalog.All.Where(entry =>
                     entry.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace)))
        {
            foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.GetProfiles(entry.IcId))
            {
                foreach (IcNumberSelection selection in PostbuildSelectionTestCases.GetBranchSelections(profile))
                {
                    LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(profile, selection);
                    IReadOnlyList<TpFlashMapRegion> regions = BuiltInTpFlashMapCatalog.GetRegions(
                        profile.IcId,
                        selection,
                        profile,
                        TpFlashMapRegionKind.CtrlRam);

                    foreach (LegacyCombinerBlockArgument block in LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan))
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

}
