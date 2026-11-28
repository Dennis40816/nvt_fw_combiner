// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>The recompute facts query reuses Common FW selection and the General Replace postbuild plan checks.</summary>
public sealed class IntegrityRecomputeFactsQueryTests
{
    private const int BlankLength = 0x40000;

    private static readonly BuiltInIntegrityRecomputeFactsQuery Query = new(BootstrapTestHost.Canonical.Projection);

    /// <summary>The host exposes the query.</summary>
    [Fact]
    public void HostExposesTheQuery()
    {
        Assert.NotNull(BootstrapTestHost.Services.IntegrityRecomputeFactsQuery);
    }

    /// <summary>No base image means the route cannot be resolved, which is unknown and not a missing rule.</summary>
    [Fact]
    public void EmptyBaseImageIsUnknown()
    {
        IntegrityRecomputeFacts facts = Query.Resolve("NT51926", "cascade", ReadOnlyMemory<byte>.Empty);

        Assert.Equal(IntegrityRecomputeAvailability.Unknown, facts.Availability);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, facts.Issue?.Code);
    }

    /// <summary>An IC with no registered postbuild profile has no recompute rule.</summary>
    [Fact]
    public void IcWithoutPostbuildProfileHasNoRecomputeRule()
    {
        IntegrityRecomputeFacts facts = Query.Resolve("NT59999", "single", new byte[BlankLength]);

        Assert.Equal(IntegrityRecomputeAvailability.NoRecomputeRule, facts.Availability);
        Assert.Equal(CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildProfileMissing, facts.Issue?.Code);
    }

    /// <summary>An IC with several Common FW profiles needs the base version; without it the route is unknown.</summary>
    [Fact]
    public void UnreadableVersionWithSeveralProfilesIsUnknown()
    {
        Assert.True(LegacyCombinerPostbuildCatalog.GetProfiles("NT51926").Count > 1);

        IntegrityRecomputeFacts facts = Query.Resolve("NT51926", "cascade", new byte[BlankLength]);

        Assert.Equal(IntegrityRecomputeAvailability.Unknown, facts.Availability);
        Assert.NotNull(facts.Issue);
    }

    /// <summary>
    /// The query reads the base version and gives the facts of the profile the shared Common FW selector picks.
    /// Today both NT51926 profiles give the same cascade facts, so this proves the hand-off to the selector, not
    /// which profile wins. The selector's own tests own the interval rule.
    /// </summary>
    [Theory]
    [InlineData(1, 4, 1)]
    [InlineData(2, 0, 0)]
    public void Nt51926VersionGivesTheFactsOfTheSelectedProfile(byte major, byte minor, byte additional)
    {
        Assert.True(BuiltInCommonFwSelector.TrySelect(
            "NT51926",
            hasCommonFwVersion: true,
            $"{major}.{minor}.{additional}",
            out BuiltInCommonFwSelection? selection,
            out _));
        IntegrityRecomputeFacts expected = IntegrityRecomputeFacts.FromPostbuild(
            selection!.PostbuildProfile!.ResolvePlan(IcNumberSelection.FromToken("cascade")),
            BlankLength);

        IntegrityRecomputeFacts facts = Query.Resolve("NT51926", "cascade", Nt51926Base(major, minor, additional));

        Assert.Equal(IntegrityRecomputeAvailability.Declared, facts.Availability);
        Assert.Equal(expected.ToolBindingId, facts.ToolBindingId);
        Assert.Equal(Commands(expected), Commands(facts));
        Assert.Equal(expected.StorageLocations.Values, facts.StorageLocations.Values);
        Assert.Equal(expected.WriteRanges.Values, facts.WriteRanges.Values);
    }

    /// <summary>A base shorter than the plan needs is unknown with the planner's length issue, not a missing rule.</summary>
    [Fact]
    public void ShortBaseIsUnknownWithThePlannerLengthIssue()
    {
        (string icId, string token, long required) = LegacyCombinerPostbuildCatalog.All
            .GroupBy(static profile => profile.IcId, StringComparer.Ordinal)
            .Where(static ic => ic.Count() == 1)
            .Select(static ic => ic.Single())
            .SelectMany(static profile => profile.PlanSelectors.Select(selector => (
                profile.IcId,
                selector.Token,
                LegacyCombinerPostbuildPlanCompiler.CalculateRequiredCapacity(
                    profile.ResolvePlan(IcNumberSelection.FromToken(selector.Token)), []))))
            .First(static route => Query.Resolve(route.IcId, route.Token, new byte[route.Item3]).Availability ==
                IntegrityRecomputeAvailability.Declared);

        IntegrityRecomputeFacts facts = Query.Resolve(icId, token, new byte[required - 1]);

        Assert.Equal(IntegrityRecomputeAvailability.Unknown, facts.Availability);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, facts.Issue?.Code);
        Assert.Equal(MemoryLayoutFactStatus.Unavailable, facts.StorageLocations.Status);
    }

    /// <summary>An IC number token the plan rejects is unknown with the planner's IC number issue.</summary>
    [Fact]
    public void UnsupportedIcNumberIsUnknownWithThePlannerIssue()
    {
        // "0" is not a positive count and is not a selector token, so no NT51926 plan selector matches it.
        IntegrityRecomputeFacts facts = Query.Resolve("NT51926", "0", Nt51926Base(2, 0, 0));

        Assert.Equal(IntegrityRecomputeAvailability.Unknown, facts.Availability);
        Assert.Equal(CompositionPlanningIssueCodes.ReplaceGeneralIcNumberUnsupported, facts.Issue?.Code);
    }

    private static string[] Commands(IntegrityRecomputeFacts facts)
    {
        return [.. facts.Commands.Select(static command => $"{command.Family}|{command.ModeArgument}|{command.CrcArgument}")];
    }

    private static byte[] Nt51926Base(byte major, byte minor, byte additional)
    {
        byte[] image = new byte[BlankLength];
        const int backupStart = 0x3F000;
        const int markerStart = 0x3FFFC;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = 0x20;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = 0xDF;
        image[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = major;
        image[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = minor;
        image[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = additional;
        image[markerStart + 1] = (byte)'N';
        image[markerStart + 2] = (byte)'V';
        image[markerStart + 3] = (byte)'T';
        return image;
    }
}
