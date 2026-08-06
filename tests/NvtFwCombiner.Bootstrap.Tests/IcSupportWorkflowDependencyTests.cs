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
        foreach (string icId in CanonicalCapabilityProjection.GetIcIds())
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
        HashSet<string> supportedIcIds = [.. CanonicalCapabilityProjection.GetIcIds()];

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

    /// <summary>DP Replace exposure stays closed to members with trusted V2 runtime registrations.</summary>
    [Fact]
    public void DpReplaceWorkflowRemainsClosedToV2RegisteredMembers()
    {
        string[] supportedIcIds =
        [
            .. GetAuthorableIcIds(ExperienceIds.DpReplace)
                .Order(StringComparer.Ordinal),
        ];
        string[] registeredIcIds =
        [
            .. CanonicalCapabilityProjection.GetDpReplaceProfileSummaries()
                .Select(summary => summary.IcId)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(registeredIcIds, supportedIcIds);
        foreach (string icId in supportedIcIds)
        {
            WorkbenchMemoryDisplay display = CompositionMemoryProjection.GetReplaceMemoryDisplay(
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

        foreach (string icId in CanonicalCapabilityProjection.GetIcIds()
                     .Except(supportedIcIds, StringComparer.Ordinal))
        {
            WorkbenchMemoryDisplay display = CompositionMemoryProjection.GetReplaceMemoryDisplay(
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
        foreach (string icId in GetAuthorableIcIds(ExperienceIds.CtrlRamReplace))
        {
            foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.GetProfiles(icId))
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

    private static string[] GetAuthorableIcIds(string workflowId)
    {
        CanonicalCapabilityCatalogSnapshot snapshot = WorkbenchHostServices
            .CanonicalCapabilityQuery
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
