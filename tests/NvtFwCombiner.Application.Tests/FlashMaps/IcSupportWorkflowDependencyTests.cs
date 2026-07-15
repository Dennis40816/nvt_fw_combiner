using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

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
                TpFlashMapCatalog.TryFind(entry.IcId, out TpFlashMapProfile? flashMapProfile),
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

        foreach (string icId in TpFlashMapCatalog.IcIds)
        {
            Assert.Contains(icId, supportedIcIds);
        }
    }

    /// <summary>CtrlRAM Replace exposure must be backed by postbuild profiles and selectable IC-number choices.</summary>
    [Fact]
    public void CtrlRamReplaceSupportHasPostbuildAndNumberChoiceCoverage()
    {
        foreach (IcSupportEntry entry in IcSupportCatalog.All.Where(entry =>
                     entry.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace)))
        {
            IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(entry.IcId);
            IReadOnlyList<string> numberChoices = TpFlashMapCatalog.GetNumberChoices(entry.IcId);

            Assert.NotEmpty(profiles);
            Assert.NotEmpty(numberChoices);
            Assert.All(profiles, profile => Assert.Equal(entry.IcId, profile.IcId));

            foreach (string numberChoice in numberChoices)
            {
                IcNumberSelection selection = PostbuildSelectionTestCases.ToNumberChoiceSelection(numberChoice);
                Assert.Contains(
                    profiles,
                    profile => CanCreatePostbuildPlan(profile, selection));
            }
        }
    }

    /// <summary>Postbuild profiles must be exposed through CtrlRAM Replace support instead of becoming hidden processor facts.</summary>
    [Fact]
    public void PostbuildProfilesHaveCtrlRamReplaceSupportRows()
    {
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
                    IReadOnlyList<TpFlashMapRegion> regions = TpFlashMapCatalog.GetCtrlRamRegions(
                        profile.IcId,
                        selection,
                        profile);

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

    private static bool CanCreatePostbuildPlan(
        LegacyCombinerPostbuildProfile profile,
        IcNumberSelection selection)
    {
        try
        {
            _ = LegacyCombinerPostbuildPlanner.CreatePlan(profile, selection);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
