// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>All-IC policy behavior using synthetic declarations, without firmware or process I/O.</summary>
public sealed class GeneralReplacePolicyTests
{
    private const long Capacity = 0x1000;

    /// <summary>The image and address-space bounds are separate, even when their lengths differ.</summary>
    [Theory]
    [InlineData(0x800, 0x7FF, 2, "general-replace.segment.image-out-of-bounds")]
    [InlineData(0x2000, 0xFFF, 2, "general-replace.segment.address-space-out-of-bounds")]
    public void WritesPastEitherExtentBlockAndGrantNoAuthority(long imageLength, long start, long length, string code)
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("row", start, length)], imageLength: imageLength);

        Assert.False(result.CanOutput);
        Assert.Equal([code], result.Blocks.Select(static issue => issue.Code));
        Assert.Empty(result.DeclaredWriteRanges);
    }

    /// <summary>A matching numeric offset in another space does not authorize that space.</summary>
    [Fact]
    public void WrongOutputAddressSpaceBlocks()
    {
        GeneralReplaceSegment segment = new("row", "input", new ByteRange(0, 4), "other", 0x400, 4);

        GeneralReplacePolicyResult result = Evaluate([segment]);

        Assert.Equal(CompositionPlanningIssueCodes.GeneralReplaceAddressSpaceOutOfBounds, Assert.Single(result.Blocks).Code);
        Assert.Empty(result.DeclaredWriteRanges);
    }

    /// <summary>A positive segment is outside an empty base and returns a user-data issue.</summary>
    [Fact]
    public void EmptyBaseBlocksSegmentWithoutThrowing()
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0, 1)], IntegrityRecomputeFacts.NoRecomputeRule(null), imageLength: 0);

        Assert.Equal(CompositionPlanningIssueCodes.GeneralReplaceImageOutOfBounds, Assert.Single(result.Blocks).Code);
        Assert.Empty(result.DeclaredWriteRanges);
    }

    /// <summary>Malformed coordinates are issues rather than overflow or range exceptions.</summary>
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(long.MaxValue, 1)]
    public void InvalidOutputCoordinatesBlock(long start, long length)
    {
        GeneralReplaceSegment segment = new("row", "input", new ByteRange(0, 1), "flash", start, length);

        GeneralReplacePolicyResult result = Evaluate([segment]);

        Assert.Contains(result.Blocks, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceSegmentRangeInvalid);
        Assert.Empty(result.DeclaredWriteRanges);
    }

    /// <summary>The selected input range must exactly supply the requested output length.</summary>
    [Fact]
    public void UnequalInputAndOutputLengthsBlock()
    {
        GeneralReplaceSegment segment = new("row", "input", new ByteRange(0, 3), "flash", 0x400, 4);

        GeneralReplacePolicyResult result = Evaluate([segment]);

        Assert.Equal(CompositionPlanningIssueCodes.GeneralReplaceSourceLengthMismatch, Assert.Single(result.Blocks).Code);
    }

    /// <summary>The complete source read must fit the supplied slot, including its nonzero start.</summary>
    [Fact]
    public void ShortSourceBlocks()
    {
        GeneralReplaceSegment segment = new("row", "input", new ByteRange(Capacity - 3, 4), "flash", 0x400, 4);

        GeneralReplacePolicyResult result = Evaluate([segment]);

        Assert.Equal(CompositionPlanningIssueCodes.GeneralReplaceSourceLengthMismatch, Assert.Single(result.Blocks).Code);
    }

    /// <summary>An absent source is not an empty or padded input.</summary>
    [Fact]
    public void MissingSourceBlocks()
    {
        GeneralReplaceSegment segment = new("row", "absent", new ByteRange(0, 4), "flash", 0x400, 4);

        GeneralReplacePolicyResult result = Evaluate([segment]);

        Assert.Equal(CompositionPlanningIssueCodes.GeneralReplaceSourceLengthMismatch, Assert.Single(result.Blocks).Code);
    }

    /// <summary>The default value of ByteRange cannot authorize a zero-length read.</summary>
    [Fact]
    public void EmptyInputRangeBlocks()
    {
        GeneralReplaceSegment segment = new("row", "input", default, "flash", 0x400, 4);

        GeneralReplacePolicyResult result = Evaluate([segment]);

        Assert.Equal(CompositionPlanningIssueCodes.GeneralReplaceSourceLengthMismatch, Assert.Single(result.Blocks).Code);
    }

    /// <summary>One shared byte blocks regardless of which row starts first.</summary>
    [Theory]
    [InlineData(0x402, 0x400)]
    [InlineData(0x400, 0x402)]
    public void OverlappingUserSegmentsBlock(long first, long second)
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("first", first, 4), Segment("second", second, 4)]);

        Assert.Equal(CompositionPlanningIssueCodes.GeneralReplaceSegmentOverlap, Assert.Single(result.Blocks).Code);
        Assert.Empty(result.DeclaredWriteRanges);
    }

    /// <summary>Half-open adjacency and a range ending exactly at capacity are allowed.</summary>
    [Fact]
    public void AdjacentSegmentsAndExactCapacityEndAreAllowed()
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("first", Capacity - 8, 4), Segment("second", Capacity - 4, 4)]);

        Assert.True(result.CanOutput);
        Assert.Empty(result.Blocks);
        Assert.Equal(new FirmwareAddressedRange("flash", new ByteRange(Capacity - 4, 4)), result.DeclaredWriteRanges[1]);
    }

    /// <summary>A complete flash segment is admitted without a DP/TP route or region-size restriction.</summary>
    [Fact]
    public void FullFlashSegmentIsAllowed()
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0, Capacity)], IntegrityRecomputeFacts.NoRecomputeRule(null));

        Assert.True(result.CanOutput);
        Assert.Equal([new FirmwareAddressedRange("flash", new ByteRange(0, Capacity))], result.DeclaredWriteRanges);
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceProtectedRegionHit);
    }

    /// <summary>Several segments can read different nonzero ranges from one input slot.</summary>
    [Fact]
    public void NonzeroInputRangesCanShareOneSlot()
    {
        GeneralReplaceSegment[] segments =
        [
            new("first", "input", new ByteRange(12, 4), "flash", 0x400, 4),
            new("second", "input", new ByteRange(20, 4), "flash", 0x500, 4),
        ];

        GeneralReplacePolicyResult result = Evaluate(segments, IntegrityRecomputeFacts.NoRecomputeRule(null));

        Assert.True(result.CanOutput);
        Assert.Equal([new FirmwareAddressedRange("flash", new ByteRange(0x400, 4)),
            new FirmwareAddressedRange("flash", new ByteRange(0x500, 4))], result.DeclaredWriteRanges);
    }

    /// <summary>A named and physically protected header remains writable with both warnings.</summary>
    [Fact]
    public void NamedAndProtectedRegionsWarnWithTheirNameAndHalfOpenRange()
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0x10, 4)]);

        Assert.True(result.CanOutput);
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceNamedRegionHit &&
            issue.Message.Contains("header", StringComparison.Ordinal) && issue.Message.Contains("[16, 20)", StringComparison.Ordinal));
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceProtectedRegionHit &&
            issue.Message.Contains("header", StringComparison.Ordinal) && issue.Message.Contains("flash", StringComparison.Ordinal));
        Assert.All(result.AdvisoryIssues, static issue => Assert.Equal(CompositionIssueSeverity.Warning, issue.Severity));
    }

    /// <summary>Crossing the header boundary reports both named regions and the protected overlap.</summary>
    [Fact]
    public void CrossRegionSegmentWarnsWithoutBlocking()
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0xFE, 4)]);

        Assert.True(result.CanOutput);
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceNamedRegionHit &&
            issue.Message.Contains("header", StringComparison.Ordinal));
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceNamedRegionHit &&
            issue.Message.Contains("body", StringComparison.Ordinal));
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceProtectedRegionHit);
    }

    /// <summary>A recompute overwrite warning follows every write hit, including non-storage writes.</summary>
    [Theory]
    [InlineData(0x18, true)]
    [InlineData(0x200, false)]
    public void RecomputeWarnsForStorageAndOtherRewrittenBytes(long address, bool storageHit)
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("row", address, 1)]);

        Assert.True(result.CanOutput);
        Assert.Equal(storageHit, result.StorageHits.Status == MemoryLayoutFactStatus.KnownWithValues);
        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, result.WriteRangeHits.Status);
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceRecomputedRangeHit &&
            issue.Message.Contains("flash", StringComparison.Ordinal));
    }

    /// <summary>Recompute grants every declared rewrite and the authored segments, without broadening either.</summary>
    [Fact]
    public void RecomputeIncludesExactlyUserAndDeclaredWrites()
    {
        IntegrityRecomputeFacts facts = RecomputeFacts();
        FirmwareAddressedRange[] expected =
            [new("flash", new ByteRange(0x400, 4)), .. facts.WriteRanges.Values.Select(static range => new FirmwareAddressedRange("flash", range.Range))];

        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0x400, 4)], facts);

        Assert.Equal(IntegrityRecomputeChoice.Recompute, result.Choice);
        Assert.Equal(expected, result.DeclaredWriteRanges);
        Assert.DoesNotContain(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceWriteAsIs);
    }

    /// <summary>WriteAsIs excludes recompute authority and lists every skipped rewritten range.</summary>
    [Fact]
    public void WriteAsIsWarnsForEverySkippedRewriteAndGrantsOnlyUserRanges()
    {
        IntegrityRecomputeFacts facts = RecomputeFacts();

        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0x18, 1)], facts, IntegrityRecomputeChoice.WriteAsIs);

        Assert.True(result.CanOutput);
        Assert.Equal([new FirmwareAddressedRange("flash", new ByteRange(0x18, 1))], result.DeclaredWriteRanges);
        Assert.DoesNotContain(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceRecomputedRangeHit);
        Assert.Equal(facts.WriteRanges.Values.Select(static range => $"flash:{range.SectionId}:{range.Range}"),
            result.AdvisoryIssues.Where(static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceWriteAsIs)
                .Select(static issue => issue.OperationId));
    }

    /// <summary>A missing rule never pretends to run recompute, even with the new-rule default choice.</summary>
    [Theory]
    [InlineData(IntegrityRecomputeChoice.Recompute)]
    [InlineData(IntegrityRecomputeChoice.WriteAsIs)]
    public void NoRuleWarnsAndResolvesToWriteAsIs(IntegrityRecomputeChoice choice)
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0x400, 4)], IntegrityRecomputeFacts.NoRecomputeRule(null), choice);

        Assert.True(result.CanOutput);
        Assert.Equal(IntegrityRecomputeChoice.WriteAsIs, result.Choice);
        Assert.Equal([new FirmwareAddressedRange("flash", new ByteRange(0x400, 4))], result.DeclaredWriteRanges);
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceNoRecomputeRule);
        Assert.Equal(MemoryLayoutFactStatus.Unavailable, result.WriteRangeHits.Status);
    }

    /// <summary>The confirmation renders only typed facts and keeps undeclared coverage unavailable.</summary>
    [Fact]
    public void DeclaredConfirmationCarriesFactsVersionsModesAndThreeButtons()
    {
        IntegrityRecomputeFacts facts = RecomputeFacts();
        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0x18, 1)], facts);

        GeneralReplaceConfirmation box = result.CreateConfirmation("1.3.0-g.2");

        Assert.Equal(IntegrityRecomputeAvailability.Declared, box.Availability);
        Assert.Equal(GeneralReplaceConfirmationReason.ReplacedContentRequiresRecompute, box.Reason);
        Assert.Equal("1.3.0-g.2", box.NfcVersion);
        Assert.Equal("synthetic-tool-v9", box.ToolBindingId);
        Assert.Equal([new IntegrityRecomputeCommand("refresh", LegacyCombinerCommandFamily.NormalMode, "CRC_Enable", null)], box.Commands);
        Assert.Same(facts.StorageLocations, box.StorageLocations);
        Assert.Same(facts.WriteRanges, box.WriteRanges);
        Assert.Same(result.StorageHits, box.StorageHits);
        Assert.Same(result.WriteRangeHits, box.WriteRangeHits);
        Assert.Equal(MemoryLayoutFactStatus.Unavailable, box.Coverage.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.NotDeclared, box.Coverage.UnavailableReason);
        Assert.Equal([GeneralReplaceConfirmationButton.RecomputeAndOutput, GeneralReplaceConfirmationButton.WriteAsIsAndOutput,
            GeneralReplaceConfirmationButton.Cancel], box.Buttons);
        Assert.Equal(GeneralReplaceConfirmationButton.RecomputeAndOutput, box.DefaultButton);
    }

    /// <summary>The restored choice selects WriteAsIs without hiding the recompute action.</summary>
    [Fact]
    public void DeclaredConfirmationPreservesWriteAsIsSelection()
    {
        GeneralReplaceConfirmation box = Evaluate([Segment("row", 0x400, 4)], choice: IntegrityRecomputeChoice.WriteAsIs)
            .CreateConfirmation("1.3.0");

        Assert.Equal(GeneralReplaceConfirmationButton.WriteAsIsAndOutput, box.DefaultButton);
        Assert.Equal(3, box.Buttons.Count);
    }

    /// <summary>A missing rule offers output-as-is and cancel, with no invented command or tool version.</summary>
    [Fact]
    public void MissingRuleConfirmationHasTwoButtons()
    {
        GeneralReplaceConfirmation box = Evaluate([Segment("row", 0x400, 4)], IntegrityRecomputeFacts.NoRecomputeRule(null))
            .CreateConfirmation("1.3.0");

        Assert.Equal(GeneralReplaceConfirmationReason.NoRecomputeRule, box.Reason);
        Assert.Equal([GeneralReplaceConfirmationButton.WriteAsIsAndOutput, GeneralReplaceConfirmationButton.Cancel], box.Buttons);
        Assert.Equal(GeneralReplaceConfirmationButton.WriteAsIsAndOutput, box.DefaultButton);
        Assert.Null(box.ToolBindingId);
        Assert.Empty(box.Commands);
        Assert.Equal(MemoryLayoutFactUnavailableReason.NotDeclared, box.Coverage.UnavailableReason);
    }

    /// <summary>Unresolved write ranges cannot prove that a segment avoids integrity writes.</summary>
    [Theory]
    [InlineData(IntegrityRecomputeChoice.Recompute)]
    [InlineData(IntegrityRecomputeChoice.WriteAsIs)]
    public void UnknownFactsBlockInsteadOfBecomingNoRule(IntegrityRecomputeChoice choice)
    {
        CompositionIssue cause = new("selection.unresolved", "Synthetic unresolved route");
        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.Unknown(cause);

        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0x18, 1)], facts, choice);

        Assert.False(result.CanOutput);
        Assert.Equal(CompositionPlanningIssueCodes.GeneralReplaceRecomputeUnknown, Assert.Single(result.Blocks).Code);
        Assert.Empty(result.DeclaredWriteRanges);
        Assert.Same(cause, result.RecomputeFacts.Issue);
        Assert.Equal(MemoryLayoutFactStatus.Unavailable, result.WriteRangeHits.Status);
        Assert.DoesNotContain(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceNoRecomputeRule);
    }

    /// <summary>An unresolved route exposes its absence and no build button.</summary>
    [Fact]
    public void UnknownConfirmationCannotOfferAnUnresolvedBuild()
    {
        GeneralReplaceConfirmation box = Evaluate([Segment("row", 0x18, 1)], IntegrityRecomputeFacts.Unknown(null))
            .CreateConfirmation("1.3.0");

        Assert.Equal(IntegrityRecomputeAvailability.Unknown, box.Availability);
        Assert.Equal(GeneralReplaceConfirmationReason.RecomputeUnavailable, box.Reason);
        Assert.Equal([GeneralReplaceConfirmationButton.Cancel], box.Buttons);
        Assert.Equal(GeneralReplaceConfirmationButton.Cancel, box.DefaultButton);
        Assert.Equal(MemoryLayoutFactStatus.Unavailable, box.StorageHits.Status);
        Assert.Equal(MemoryLayoutFactStatus.Unavailable, box.WriteRangeHits.Status);
    }

    /// <summary>Legacy policy ceilings do not restrict the number of segments in the new all-IC policy.</summary>
    [Fact]
    public void MoreThanLegacyMappingLimitIsAllowedWhenRangesFit()
    {
        FirmwareImageMap map = Map(0x4000);
        GeneralReplaceSegment[] segments = [.. Enumerable.Range(0, 4097).Select(static index => Segment($"row-{index}", 0x400 + index, 1))];

        GeneralReplacePolicyResult result = GeneralReplacePolicy.Evaluate(map, Access(map), map.CapacityBytes,
            IntegrityRecomputeFacts.NoRecomputeRule(null), segments,
            new Dictionary<string, long>(StringComparer.Ordinal) { ["input"] = 1 });

        Assert.True(result.CanOutput);
        Assert.Empty(result.Blocks);
        Assert.Equal(4097, result.DeclaredWriteRanges.Count);
    }

    /// <summary>Profile hidden/read-only restrictions warn through the canonical access projection.</summary>
    [Theory]
    [InlineData(RegionAccessKind.Hidden)]
    [InlineData(RegionAccessKind.ReadOnly)]
    public void ProfileProtectedRegionWarnsWithoutBlocking(RegionAccessKind accessKind)
    {
        FirmwareImageMap map = Map();
        FirmwareRegion root = map.Regions.First(static region => region.RegionId == "image");
        FirmwareRegion body = map.Regions.First(static region => region.RegionId == "body");
        CompiledRegionAccessContract access = new(
            [new CompiledRegionAccessRequirement("image", RegionAccessKind.ExplicitRange, "Synthetic root", [], [root]),
                new CompiledRegionAccessRequirement("body", accessKind, "Synthetic protected body", [], [root, body])], []);

        GeneralReplacePolicyResult result = GeneralReplacePolicy.Evaluate(map, access, Capacity, RecomputeFacts(),
            [Segment("row", 0x400, 4)], new Dictionary<string, long>(StringComparer.Ordinal) { ["input"] = 4 });

        Assert.True(result.CanOutput);
        Assert.Contains(result.AdvisoryIssues, static issue => issue.Code == CompositionPlanningIssueCodes.GeneralReplaceProtectedRegionHit &&
            issue.Message.Contains("body", StringComparison.Ordinal));
    }

    /// <summary>Recompute facts for a larger image cannot broaden the smaller base's host authority.</summary>
    [Fact]
    public void RecomputeWritesBeyondTheImageBlock()
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0, 1)], imageLength: 0x80);

        Assert.False(result.CanOutput);
        Assert.Contains(result.Blocks, static issue => issue.Code == CompositionPlanningIssueCodes.ReplaceGeneralPostbuildWriteRangeMissing);
        Assert.Empty(result.DeclaredWriteRanges);
    }

    /// <summary>Published collections cannot be mutated through an IList cast.</summary>
    [Fact]
    public void PublishedDecisionAndButtonsAreReadOnly()
    {
        GeneralReplacePolicyResult result = Evaluate([Segment("row", 0x400, 4)]);
        GeneralReplaceConfirmation box = result.CreateConfirmation("1.3.0");

        Assert.True(Assert.IsType<IList<CompositionIssue>>(result.AdvisoryIssues, exactMatch: false).IsReadOnly);
        Assert.True(Assert.IsType<IList<FirmwareAddressedRange>>(result.DeclaredWriteRanges, exactMatch: false).IsReadOnly);
        Assert.True(Assert.IsType<IList<GeneralReplaceConfirmationButton>>(box.Buttons, exactMatch: false).IsReadOnly);
    }

    private static GeneralReplacePolicyResult Evaluate(GeneralReplaceSegment[] segments, IntegrityRecomputeFacts? facts = null,
        IntegrityRecomputeChoice choice = IntegrityRecomputeChoice.Recompute, long imageLength = Capacity)
    {
        FirmwareImageMap map = Map();
        return GeneralReplacePolicy.Evaluate(map, Access(map), imageLength, facts ?? RecomputeFacts(), segments,
            new Dictionary<string, long>(StringComparer.Ordinal) { ["input"] = Capacity }, choice);
    }

    private static CompiledRegionAccessContract Access(FirmwareImageMap map)
    {
        return new([new CompiledRegionAccessRequirement("image", RegionAccessKind.ExplicitRange, "Synthetic access", [],
            [map.Regions.First(static region => region.RegionId == "image")])], []);
    }

    private static GeneralReplaceSegment Segment(string id, long outputStart, long length)
    {
        return new(id, "input", new ByteRange(0, length), "flash", outputStart, length);
    }

    private static FirmwareImageMap Map(long capacity = Capacity)
    {
        FirmwareRegion[] regions =
        [
            new("image", null, FirmwareRegionOwner.System, FirmwareRegionKind.Image, new ByteRange(0, capacity), FirmwareWriteConstraint.ExplicitRange),
            new("header", "image", FirmwareRegionOwner.System, FirmwareRegionKind.Header, new ByteRange(0, 0x100), FirmwareWriteConstraint.Forbidden),
            new("body", "image", FirmwareRegionOwner.Tp, FirmwareRegionKind.Code, new ByteRange(0x100, capacity - 0x100), FirmwareWriteConstraint.ExplicitRange),
        ];
        return FirmwareImageMapTestFactory.CreateDirect("synthetic-map", "flash",
            new FirmwareMapApplicability(["SYNTHETIC"], [ExperienceIds.GeneralReplace], TopologyRequirement.NoTopologyConstraint(), capacity),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet("synthetic-regions", "flash", regions, ["synthetic-evidence"])], [], ["synthetic-evidence"]);
    }

    private static IntegrityRecomputeFacts RecomputeFacts()
    {
        LegacyCombinerPostbuildCommand command = new("refresh", LegacyCombinerCommandFamily.NormalMode, "CRC_Enable", null,
            [new LegacyCombinerBlockArgument("copy", LegacyCombinerBlockSourceKind.FirmwareImage, "base.bin", 0x300, new ByteRange(0x200, 0x10))]);
        LegacyCombinerPostbuildProfile profile = new("synthetic-refresh", "SYNTHETIC", "synthetic-tool-v9", "base.bin",
            [command], [command], "Synthetic recompute declarations");
        return IntegrityRecomputeFacts.FromPostbuild(profile.ResolvePlan(IcNumberSelection.FromToken("single")), Capacity);
    }
}
