using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildCatalogTests
{
    /// <summary>Locks NT51926 Common FW 1.4.1 to its owner-provided header-copy target.</summary>
    [Fact]
    public void Nt51926CommonFw141SelectsLegacyHeaderCopyTarget()
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51926",
            "1.4.1",
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue), issue);

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            profile!,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Equal("nfc.nt51926.ctrlram-postbuild-fw1.4.1", profile!.ProcessorId);
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

    /// <summary>Locks NT51930 Common FW 1.x to the 1.4.0-era command shape.</summary>
    [Fact]
    public void Nt51930CommonFw1xSelectsSingleLegacyHeaderCommand()
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51930",
            "1.3.0",
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue), issue);

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            profile!,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Equal("nfc.nt51930.ctrlram-postbuild-fw1.x", profile!.ProcessorId);
        Assert.Contains("PostbuildSetup_51930_1.4.0.bat", profile.Evidence, StringComparison.Ordinal);
        LegacyCombinerPostbuildCommand command = Assert.Single(plan.Commands);
        Assert.Contains(
            command.Blocks,
            block => block.SourceFileName == "MP_Ctrlram.bin" &&
                     block.FirmwareRange == new ByteRange(0x24250, 13312));
        Assert.Contains(
            command.Blocks,
            block => block.BlockId == "header-copy" &&
                     block.FirmwareRange == new ByteRange(0x28FB0, 256));
    }

    /// <summary>Locks NT51930 Common FW 1.x large cascade counts to the archived extend branch.</summary>
    [Fact]
    public void Nt51930CommonFw1xSelectsExtendedCascadeDiffLength()
    {
        LegacyCombinerPostbuildCommandPlan normalCascade = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["13"]));
        LegacyCombinerPostbuildCommandPlan extendedCascade = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["14"]));

        LegacyCombinerBlockArgument normalDiff = normalCascade.Commands
            .SelectMany(command => command.Blocks)
            .Single(block => block.SourceFileName == "DiffDLM.bin");
        LegacyCombinerBlockArgument extendedDiff = extendedCascade.Commands
            .SelectMany(command => command.Blocks)
            .Single(block => block.SourceFileName == "DiffDLM.bin");

        Assert.Equal(LegacyCombinerPostbuildBranch.Cascade, normalCascade.Branch);
        Assert.Equal(new ByteRange(0x2F200, 65024), normalDiff.FirmwareRange);
        Assert.Equal(LegacyCombinerPostbuildBranch.CascadeExtended, extendedCascade.Branch);
        Assert.Equal(new ByteRange(0x2F200, 143360), extendedDiff.FirmwareRange);
    }

    /// <summary>Locks ambiguous versioned ICs to fail closed for unsupported Common FW versions.</summary>
    [Fact]
    public void VersionedPostbuildSelectionRejectsUnknownCommonFw()
    {
        Assert.False(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51926",
            "1.4.0",
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue));

        Assert.Null(profile);
        Assert.Contains("no approved postbuild category", issue, StringComparison.Ordinal);
        Assert.Contains("Common FW 1.4.1 => PostbuildSetup_51926_1.4.1", issue, StringComparison.Ordinal);
        Assert.Contains("Common FW 2.0.0 => PostbuildSetup_51926_2.0.0", issue, StringComparison.Ordinal);

        Assert.False(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51930",
            "10.0.0",
            out profile,
            out issue));

        Assert.Null(profile);
        Assert.Contains("no approved postbuild category", issue, StringComparison.Ordinal);
        Assert.Contains("Common FW 1.x.x => PostbuildSetup_51930_1.4.0", issue, StringComparison.Ordinal);
        Assert.Contains("Common FW 2.0.0 => PostbuildSetup_51930_2.0.0", issue, StringComparison.Ordinal);
    }

    /// <summary>Locks duplicate IC postbuild rows to explicit Common FW category selection.</summary>
    [Fact]
    public void DuplicatePostbuildIcIdsMustHaveVersionSelectionPolicy()
    {
        (string IcId, string CommonFwVersion, LegacyCombinerPostbuildProfile Profile)[] versionedCases =
        [
            ("NT51926", "1.4.1", LegacyCombinerPostbuildCatalog.Nt51926CommonFw141),
            ("NT51926", "2.0.0", LegacyCombinerPostbuildCatalog.Nt51926),
            ("NT51930", "1.0.0", LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x),
            ("NT51930", "1.3.0", LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x),
            ("NT51930", "2.0.0", LegacyCombinerPostbuildCatalog.Nt51930),
        ];
        string[] duplicateIcIds = [
            .. LegacyCombinerPostbuildCatalog.All
                .GroupBy(profile => profile.IcId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(["NT51926", "NT51930"], duplicateIcIds);
        foreach (string icId in duplicateIcIds)
        {
            Assert.All(
                LegacyCombinerPostbuildCatalog.GetProfiles(icId),
                profile => Assert.NotNull(profile.CommonFwVersionRule));

            Assert.False(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
                icId,
                commonFwVersion: null,
                out LegacyCombinerPostbuildProfile? profile,
                out string? issue));
            Assert.Null(profile);
            Assert.Contains("multiple postbuild categories", issue, StringComparison.Ordinal);

            string[] duplicateProfileIds = [
                .. LegacyCombinerPostbuildCatalog.GetProfiles(icId)
                    .Select(candidate => candidate.ProcessorId)
                    .Order(StringComparer.Ordinal),
            ];
            string[] reachableProfileIds = [
                .. versionedCases
                    .Where(testCase => testCase.IcId == icId)
                    .Select(testCase => testCase.Profile.ProcessorId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ];
            Assert.Equal(duplicateProfileIds, reachableProfileIds);
        }

        foreach ((string icId, string commonFwVersion, LegacyCombinerPostbuildProfile expectedProfile) in versionedCases)
        {
            AssertSelectsProfile(icId, commonFwVersion, expectedProfile);
        }
    }

    private static void AssertSelectsProfile(
        string icId,
        string commonFwVersion,
        LegacyCombinerPostbuildProfile expectedProfile)
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            icId,
            commonFwVersion,
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue), issue);

        Assert.Same(expectedProfile, profile);
        Assert.Null(issue);
    }
}
