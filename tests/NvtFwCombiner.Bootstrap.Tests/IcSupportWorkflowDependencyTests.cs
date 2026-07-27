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
    /// Every production postbuild profile must be exposed through CtrlRAM Replace support.
    /// </summary>
    [Fact]
    public void PostbuildProfilesHaveCtrlRamReplaceSupportOrExplicitBlockedRows()
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

        Assert.Equal(13, ctrlRamReplaceIcIds.Count);
    }

    /// <summary>DP Replace exposure stays closed to members with trusted V2 runtime registrations.</summary>
    [Fact]
    public void DpReplaceWorkflowRemainsClosedToV2RegisteredMembers()
    {
        string[] supportedIcIds =
        [
            .. IcSupportCatalog.All
                .Where(entry => entry.SupportsWorkflow(IcWorkflowIds.DpReplace))
                .Select(entry => entry.IcId)
                .Order(StringComparer.Ordinal),
        ];
        string[] registeredIcIds =
        [
            .. IcSupportCatalog.All
                .Where(entry => WorkbenchCompositionService.HasBuiltInV2DpReplace(entry.IcId))
                .Select(entry => entry.IcId)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(registeredIcIds, supportedIcIds);
        Assert.Equal(
            supportedIcIds.Intersect(
                WorkbenchCompositionService.GetSupportedIcIds(),
                StringComparer.Ordinal),
            WorkbenchCompositionService.GetReplaceProfileSummaries()
                .Select(summary => summary.IcId)
                .Order(StringComparer.Ordinal));
        foreach (string icId in supportedIcIds)
        {
            WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
                icId,
                "single",
                WorkbenchReplaceModes.Dp);
            WorkbenchMemoryMapRow row = Assert.Single(display.MemoryMapRows);
            Assert.StartsWith("Reference FlashCode length:", display.RangeLabel, StringComparison.Ordinal);
            Assert.Equal("Reference FlashCode", row.BeforeSource);
            Assert.Equal("Select", row.ActionLabel);
            Assert.Equal("DP replacement", row.AfterSource);
            Assert.NotEmpty(display.CoverageSegments);
        }

        foreach (string icId in IcSupportCatalog.All
                     .Select(entry => entry.IcId)
                     .Except(supportedIcIds, StringComparer.Ordinal))
        {
            WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
                icId,
                "single",
                WorkbenchReplaceModes.Dp);
            WorkbenchMemoryMapRow row = Assert.Single(display.MemoryMapRows);
            Assert.Equal("Not available", display.RangeLabel);
            Assert.Equal("Blocked", row.ActionLabel);
            Assert.Equal("No target", row.AfterSource);
            Assert.Empty(display.CoverageSegments);
        }
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
