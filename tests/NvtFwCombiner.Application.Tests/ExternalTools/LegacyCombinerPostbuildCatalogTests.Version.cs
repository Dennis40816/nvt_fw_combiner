using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildCatalogTests
{
    /// <summary>Locks every supported 1.x version to NT51926's first runtime profile interval.</summary>
    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.4.0")]
    [InlineData("1.4.1")]
    [InlineData("1.255.255")]
    public void Nt51926CommonFw1xSelectsFirstRuntimeProfile(string commonFwVersion)
    {
        LegacyCombinerPostbuildProfile profile = Select("NT51926", commonFwVersion);
        LegacyCombinerPostbuildCommandPlan plan = profile.ResolvePlan(new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Equal("nfc.nt51926.ctrlram-postbuild-fw1.4.1", profile.ProcessorId);
        Assert.Equal(new LegacyCombinerCommonFwVersion(1, 0, 0), profile.EffectiveCommonFwVersion);
        Assert.Contains("PostbuildSetup_51926_1.4.1.bat", profile.Evidence, StringComparison.Ordinal);
        Assert.Contains(
            plan.Commands.SelectMany(command => command.Blocks),
            block => block.BlockId == "header-copy" &&
                     block.FirmwareRange == new ByteRange(0x32F50, 256));
        Assert.Contains(
            plan.Commands.SelectMany(command => command.Blocks),
            block => block.SourceFileName == "VN_Ctrlram.bin" &&
                     block.FirmwareRange == new ByteRange(0x315D0, 5728));
    }

    /// <summary>Locks NT51926 2.0.0 and later to the second runtime profile interval.</summary>
    [Theory]
    [InlineData("2.0.0")]
    [InlineData("2.0.1")]
    [InlineData("3.0.0")]
    [InlineData("255.255.255")]
    public void Nt51926CommonFw2AndLaterSelectsSecondRuntimeProfile(string commonFwVersion)
    {
        LegacyCombinerPostbuildProfile profile = Select("NT51926", commonFwVersion);

        Assert.Equal("nfc.nt51926.ctrlram-postbuild-v1", profile.ProcessorId);
        Assert.Equal(new LegacyCombinerCommonFwVersion(2, 0, 0), profile.EffectiveCommonFwVersion);
        Assert.Contains("PostbuildSetup_51926_2.0.0.bat", profile.Evidence, StringComparison.Ordinal);
    }

    /// <summary>Every IC with one runtime profile routes without informational Common FW metadata.</summary>
    [Fact]
    public void SingleRuntimeProfilesDoNotRequireCommonFw()
    {
        IGrouping<string, LegacyCombinerPostbuildProfile>[] singleProfileIcs =
        [
            .. LegacyCombinerPostbuildCatalog.All
                .GroupBy(static profile => profile.IcId, StringComparer.Ordinal)
                .Where(static group => group.Count() == 1),
        ];

        Assert.Equal(9, singleProfileIcs.Length);
        foreach (IGrouping<string, LegacyCombinerPostbuildProfile> group in singleProfileIcs)
        {
            LegacyCombinerPostbuildProfile selected = Select(group.Key, commonFwVersion: null);
            Assert.Same(Assert.Single(group), selected);
            Assert.Equal(LegacyCombinerCommonFwVersion.MinimumSupported, selected.EffectiveCommonFwVersion);
        }
    }

    /// <summary>Readable versions below the owner-defined 1.0.0 minimum fail for every profile count.</summary>
    [Theory]
    [InlineData("NT51926")]
    public void CommonFwBelowMinimumIsRejected(string icId)
    {
        Assert.False(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            icId,
            "0.255.255",
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue));

        Assert.Null(profile);
        Assert.Contains("below the minimum supported version 1.0.0", issue, StringComparison.Ordinal);
    }

    /// <summary>Multiple runtime intervals require valid metadata because the command profile differs.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("not-a-version")]
    public void MultipleRuntimeProfilesRejectMissingOrMalformedCommonFw(string? commonFwVersion)
    {
        Assert.False(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51926",
            commonFwVersion,
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue));

        Assert.Null(profile);
        Assert.Contains("multiple runtime postbuild profiles", issue, StringComparison.Ordinal);
        Assert.Contains("[1.0.0, 2.0.0)", issue, StringComparison.Ordinal);
        Assert.Contains("[2.0.0, infinity)", issue, StringComparison.Ordinal);
    }

    /// <summary>Duplicate runtime IC rows must form unique ordered intervals beginning at 1.0.0.</summary>
    [Fact]
    public void DuplicateRuntimeProfilesExposeCompleteEffectiveIntervals()
    {
        IGrouping<string, LegacyCombinerPostbuildProfile> duplicate = Assert.Single(
            LegacyCombinerPostbuildCatalog.All
                .GroupBy(static profile => profile.IcId, StringComparer.Ordinal),
            static group => group.Count() > 1);
        LegacyCombinerPostbuildProfile[] profiles =
        [
            .. duplicate.OrderBy(static profile => profile.EffectiveCommonFwVersion),
        ];

        Assert.Equal("NT51926", duplicate.Key);
        Assert.Equal(
            [new LegacyCombinerCommonFwVersion(1, 0, 0), new LegacyCombinerCommonFwVersion(2, 0, 0)],
            profiles.Select(static profile => profile.EffectiveCommonFwVersion));
        Assert.Same(profiles[0], Select("NT51926", "1.255.255"));
        Assert.Same(profiles[1], Select("NT51926", "2.0.0"));
    }

    private static LegacyCombinerPostbuildProfile Select(string icId, string? commonFwVersion)
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            icId,
            commonFwVersion,
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue), issue);
        Assert.Null(issue);
        return profile!;
    }
}
