using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Composition;

namespace NvtFwCombiner.Infrastructure.Tests.Composition;

/// <summary>Tests the one selector that every flow uses to pick the postbuild profile and the TP flash map.</summary>
public sealed class BuiltInCommonFwSelectorTests
{
    /// <summary>NT51925 Common FW 1.x is refused with a message that names the missing 1.x map.</summary>
    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.4.1")]
    [InlineData("1.255.255")]
    public void Nt51925Common1xIsRefusedUntilItsMapExists(string commonFwVersion)
    {
        Assert.False(BuiltInCommonFwSelector.TrySelect("NT51925", true, commonFwVersion,
            out BuiltInCommonFwSelection? selection, out CompositionIssue? issue));

        Assert.Null(selection);
        Assert.Equal(CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnsupported, issue!.Code);
        Assert.Equal("NT51925 1.x map has not been provided", issue.Message);
    }

    /// <summary>NT51925 Common FW 2.0.0 and later is refused with a message that names the missing 2.0.0 map.</summary>
    [Theory]
    [InlineData("2.0.0")]
    [InlineData("2.4.99")]
    [InlineData("2.5.0")]
    [InlineData("3.0.0")]
    [InlineData("255.255.255")]
    public void Nt51925Common2xAndLaterIsRefusedUntilItsMapExists(string commonFwVersion)
    {
        Assert.False(BuiltInCommonFwSelector.TrySelect("NT51925", true, commonFwVersion,
            out BuiltInCommonFwSelection? selection, out CompositionIssue? issue));

        Assert.Null(selection);
        Assert.Equal(CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnsupported, issue!.Code);
        Assert.Equal("NT51925 2.0.0 map has not been provided", issue.Message);
    }

    /// <summary>An unreadable or missing version never selects a map for NT51925.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("2.0")]
    public void Nt51925UnreadableVersionIsRefused(string? commonFwVersion)
    {
        Assert.False(BuiltInCommonFwSelector.TrySelect("NT51925", false, commonFwVersion,
            out BuiltInCommonFwSelection? selection, out CompositionIssue? issue));

        Assert.Null(selection);
        Assert.Equal(CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnknown, issue!.Code);
    }

    /// <summary>A version below the minimum is refused for NT51925 before any map is chosen.</summary>
    [Fact]
    public void Nt51925VersionBelowMinimumIsRefused()
    {
        Assert.False(BuiltInCommonFwSelector.TrySelect("NT51925", true, "0.9.9", out _, out CompositionIssue? issue));

        Assert.Equal(CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnsupported, issue!.Code);
    }

    /// <summary>The same selection must reach the postbuild profile and the TP map of NT51926 for both Common FW lines.</summary>
    [Theory]
    [InlineData("1.4.1", 1)]
    [InlineData("1.255.255", 1)]
    [InlineData("2.0.0", 2)]
    [InlineData("255.255.255", 2)]
    public void Nt51926SelectsItsPostbuildProfileAndTheSoleMap(string commonFwVersion, int expectedMajor)
    {
        Assert.True(BuiltInCommonFwSelector.TrySelect("NT51926", true, commonFwVersion,
            out BuiltInCommonFwSelection? selection, out CompositionIssue? issue));

        Assert.Null(issue);
        Assert.Equal(expectedMajor, selection!.PostbuildProfile!.EffectiveCommonFwVersion.Major);
        Assert.True(BuiltInTpFlashMapCatalog.TryFind("NT51926", out TpFlashMapProfile? sole));
        Assert.Same(sole, selection.TpFlashMap);
    }

    /// <summary>Every shipped IC other than NT51925 keeps selecting its sole entry for any readable version.</summary>
    [Fact]
    public void EveryOtherShippedIcKeepsItsSoleMapForAnyVersion()
    {
        foreach (string icId in BuiltInTpFlashMapCatalog.IcIds.Where(static id => id != "NT51925"))
        {
            Assert.True(BuiltInTpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? sole));
            foreach (string version in new[] { "1.0.0", "2.0.0", "255.255.255" })
            {
                Assert.Same(sole, BuiltInCommonFwSelector.FindTpFlashMap(icId, version));
            }

            Assert.Same(sole, BuiltInCommonFwSelector.FindTpFlashMap(icId, null));
        }
    }

    /// <summary>The display lookup shows no map instead of an unverified one when the selection is refused.</summary>
    [Theory]
    [InlineData("1.4.1")]
    [InlineData("2.0.0")]
    [InlineData("255.255.255")]
    [InlineData(null)]
    [InlineData("invalid")]
    public void DisplayLookupShowsNoMapForARefusedNt51925Version(string? commonFwVersion)
    {
        Assert.Null(BuiltInCommonFwSelector.FindTpFlashMap("NT51925", commonFwVersion));
    }

    /// <summary>An IC without a TP flash-map entry still resolves its postbuild profile and gets no map.</summary>
    [Fact]
    public void UnknownIcWithoutAnyEntryIsRefusedByThePostbuildCatalog()
    {
        Assert.False(BuiltInCommonFwSelector.TrySelect("NT00000", true, "1.0.0", out _, out CompositionIssue? issue));

        Assert.Equal(CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildProfileMissing, issue!.Code);
        Assert.Null(BuiltInCommonFwSelector.FindTpFlashMap("NT00000", "1.0.0"));
    }
}
