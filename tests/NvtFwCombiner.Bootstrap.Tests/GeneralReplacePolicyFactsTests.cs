// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Policy integration with canonical IC maps and real postbuild declarations; no processors run.</summary>
public sealed class GeneralReplacePolicyFactsTests
{
    /// <summary>Both choices retain the real tool binding, command mode and exact write authority.</summary>
    [Theory]
    [InlineData("NT51926", "cascade", IntegrityRecomputeChoice.Recompute)]
    [InlineData("NT51926", "cascade", IntegrityRecomputeChoice.WriteAsIs)]
    [InlineData("NT51927", "2", IntegrityRecomputeChoice.Recompute)]
    [InlineData("NT51927", "2", IntegrityRecomputeChoice.WriteAsIs)]
    public void RealFactsDrivePolicyAndConfirmation(string icId, string token, IntegrityRecomputeChoice choice)
    {
        (CompiledComposition composition, IntegrityRecomputeFacts facts) = Resolve(icId, token);
        FirmwareImageMap map = composition.V2Details.Provenance.ResolvedMap.ImageMap;
        ByteRange userRange = facts.StorageLocations.Values[0].StorageRange;
        GeneralReplaceSegment segment = new("row", "input", new ByteRange(0, userRange.Length),
            map.AddressSpaceId, userRange.Start, userRange.Length);

        GeneralReplacePolicyResult result = GeneralReplacePolicy.Evaluate(map, composition.V2Details.RegionAccessContract,
            map.CapacityBytes, facts, [segment], new Dictionary<string, long>(StringComparer.Ordinal) { ["input"] = userRange.Length }, choice);
        GeneralReplaceConfirmation box = result.CreateConfirmation("1.3.0-g.2");

        Assert.True(result.CanOutput);
        Assert.Empty(result.Blocks);
        Assert.Equal(choice, result.Choice);
        FirmwareAddressedRange[] recomputeWrites = choice == IntegrityRecomputeChoice.Recompute
            ? [.. facts.WriteRanges.Values.Select(range => new FirmwareAddressedRange(map.AddressSpaceId, range.Range))]
            : [];
        FirmwareAddressedRange[] expected = [.. new[] { new FirmwareAddressedRange(map.AddressSpaceId, userRange) }.Concat(recomputeWrites).Distinct()];
        Assert.Equal(expected, result.DeclaredWriteRanges);
        Assert.Equal(IntegrityRecomputeAvailability.Declared, box.Availability);
        Assert.Equal(facts.ToolBindingId, box.ToolBindingId);
        Assert.Equal(facts.Commands, box.Commands);
        Assert.Equal(facts.StorageLocations.Values, box.StorageLocations.Values);
        Assert.Equal(facts.WriteRanges.Values, box.WriteRanges.Values);
        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, box.StorageHits.Status);
        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, box.WriteRangeHits.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.NotDeclared, box.Coverage.UnavailableReason);
        Assert.Equal([GeneralReplaceConfirmationButton.RecomputeAndOutput, GeneralReplaceConfirmationButton.WriteAsIsAndOutput,
            GeneralReplaceConfirmationButton.Cancel], box.Buttons);
        Assert.Equal(choice == IntegrityRecomputeChoice.Recompute
            ? GeneralReplaceConfirmationButton.RecomputeAndOutput
            : GeneralReplaceConfirmationButton.WriteAsIsAndOutput, box.DefaultButton);
        Assert.Contains(result.AdvisoryIssues, issue => issue.Code == (choice == IntegrityRecomputeChoice.Recompute
            ? CompositionPlanningIssueCodes.GeneralReplaceRecomputedRangeHit
            : CompositionPlanningIssueCodes.GeneralReplaceWriteAsIs));
    }

    /// <summary>CRC-only includes relocated header/data writes beyond its CRC words.</summary>
    [Fact]
    public void Nt51927CrcOnlyWritesOutsideCrcStorageStillWarnAndReachHostAuthority()
    {
        (CompiledComposition composition, IntegrityRecomputeFacts facts) = Resolve("NT51927", "2");
        FirmwareImageMap map = composition.V2Details.Provenance.ResolvedMap.ImageMap;
        ByteRange[] storage = [.. facts.StorageLocations.Values.Select(static location => location.StorageRange)];
        ByteRange writeOnly = facts.WriteRanges.Values.SelectMany(range => range.Range.Subtract(storage)).First();
        GeneralReplaceSegment segment = new("row", "input", new ByteRange(0, 1), map.AddressSpaceId, writeOnly.Start, 1);

        GeneralReplacePolicyResult result = GeneralReplacePolicy.Evaluate(map, composition.V2Details.RegionAccessContract,
            map.CapacityBytes, facts, [segment], new Dictionary<string, long>(StringComparer.Ordinal) { ["input"] = 1 });

        Assert.Contains(facts.Commands, static command => command.Family == LegacyCombinerCommandFamily.CrcOnlyMode);
        Assert.True(result.CanOutput);
        Assert.Equal(MemoryLayoutFactStatus.KnownEmpty, result.StorageHits.Status);
        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, result.WriteRangeHits.Status);
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceRecomputedRangeHit);
        Assert.All(facts.WriteRanges.Values, range => Assert.Contains(
            new FirmwareAddressedRange(map.AddressSpaceId, range.Range), result.DeclaredWriteRanges));
        Assert.True(facts.WriteRanges.Values.Sum(static range => range.Range.Length) >
            facts.StorageLocations.Values.Sum(static location => location.StorageRange.Length));
    }

    private static (CompiledComposition Composition, IntegrityRecomputeFacts Facts) Resolve(string icId, string token)
    {
        Assert.True(BootstrapTestHost.Canonical.Compiler.TryCompileStandardMerge(icId, null,
            out CompiledComposition? compiled, out IReadOnlyList<CompositionIssue> compileIssues),
            string.Join(Environment.NewLine, compileIssues.Select(static issue => issue.Message)));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compiled);
        Assert.True(BuiltInCommonFwSelector.TrySelect(icId, hasCommonFwVersion: true, "2.0.0",
            out BuiltInCommonFwSelection? selection, out CompositionIssue? selectionIssue), selectionIssue?.Message);
        LegacyCombinerPostbuildCommandPlan? plan = BuiltInGeneralAuthoringPlanner.TryResolveGeneralReplacePostbuildPlan(
            icId, IcNumberSelection.FromToken(token), composition.V2Details.Provenance.ResolvedMap.ImageMap.CapacityBytes,
            profileResolved: true, selection!.PostbuildProfile, profileIssue: null, out CompositionIssue? issue);
        Assert.Null(issue);
        return (composition, IntegrityRecomputeFacts.FromPostbuild(Assert.IsType<LegacyCombinerPostbuildCommandPlan>(plan),
            composition.V2Details.Provenance.ResolvedMap.ImageMap.CapacityBytes));
    }
}
