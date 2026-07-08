using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
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
                IcNumberSelection selection = ToSelection(numberChoice);
                Assert.Contains(
                    profiles,
                    profile => CanCreatePostbuildPlan(profile, selection));
            }
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

    /// <summary>Every postbuild branch staged BIN is explainable by the profile-adjusted TP Overview CtrlRAM rows.</summary>
    [Fact]
    public void PostbuildBranchesMapToProfileAdjustedCtrlRamRows()
    {
        foreach (IcSupportEntry entry in IcSupportCatalog.All.Where(entry =>
                     entry.SupportsWorkflow(IcWorkflowIds.CtrlRamReplace)))
        {
            foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.GetProfiles(entry.IcId))
            {
                foreach (IcNumberSelection selection in GetBranchSelections(profile))
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

    private static IEnumerable<IcNumberSelection> GetBranchSelections(LegacyCombinerPostbuildProfile profile)
    {
        return profile.BranchRules
            .Select(rule => ToSelection(rule.Key, rule.Value))
            .DistinctBy(selection => $"{selection.Mode}:{string.Join("|", selection.Parts)}");
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

    private static IcNumberSelection ToSelection(string token)
    {
        return token switch
        {
            string value when IcNumberSelectionTokens.IsSingle(value) =>
                new IcNumberSelection(IcNumberInputMode.SingleSelector, [value]),
            string value when string.Equals(value, IcNumberSelectionTokens.Cascade, StringComparison.Ordinal) =>
                new IcNumberSelection(IcNumberInputMode.CascadeSelector, [value]),
            string value when int.TryParse(value, out _) =>
                new IcNumberSelection(IcNumberInputMode.NumericSelector, [value]),
            _ => throw new ArgumentException($"Unsupported IC number token '{token}'.", nameof(token)),
        };
    }

    private static IcNumberSelection ToSelection(string token, LegacyCombinerPostbuildBranch branch)
    {
        return branch switch
        {
            LegacyCombinerPostbuildBranch.SingleChip =>
                new IcNumberSelection(IcNumberInputMode.SingleSelector, [IcNumberSelectionTokens.SingleChip]),
            LegacyCombinerPostbuildBranch.Cascade when int.TryParse(token, out int count) && count > 1 =>
                new IcNumberSelection(IcNumberInputMode.NumericSelector, [token]),
            LegacyCombinerPostbuildBranch.Cascade =>
                new IcNumberSelection(IcNumberInputMode.CascadeSelector, [IcNumberSelectionTokens.Cascade]),
            LegacyCombinerPostbuildBranch.CascadeExtended or
                LegacyCombinerPostbuildBranch.TwoChip or
                LegacyCombinerPostbuildBranch.ThreeChip =>
                new IcNumberSelection(IcNumberInputMode.NumericSelector, [token]),
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "Unsupported postbuild branch."),
        };
    }
}
