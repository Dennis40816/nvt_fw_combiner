// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

/// <summary>Recompute confirmation facts reuse the postbuild owner and never treat unknown as none.</summary>
public sealed class IntegrityRecomputeFactsTests
{
    private const long Capacity = 0x40000;

    /// <summary>Storage locations and write ranges are exactly the postbuild owner's sections, for every built-in plan.</summary>
    [Fact]
    public void FactsReuseThePostbuildOwnerForEveryPlan()
    {
        foreach (LegacyCombinerPostbuildCommandPlan plan in AllPlans())
        {
            long capacity = CapacityFor(plan);
            IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, capacity);

            Assert.Equal(IntegrityRecomputeAvailability.Declared, facts.Availability);
            Assert.Null(facts.Issue);
            ByteRange[] expectedStorage = Sorted(
                LegacyCombinerPostbuildPlanCompiler.GetKnownIntegrityWriteRangeSections(plan, capacity)
                    .Select(static section => section.Range));
            ByteRange[] actualStorage = Sorted(facts.StorageLocations.Values.Select(static location => location.StorageRange));
            Assert.Equal(expectedStorage, actualStorage);
            // A declared route never reports storage as known empty: no listed storage means not declared.
            Assert.Equal(
                expectedStorage.Length == 0 ? MemoryLayoutFactStatus.Unavailable : MemoryLayoutFactStatus.KnownWithValues,
                facts.StorageLocations.Status);
            ByteRange[] expectedWrites = Sorted(
                LegacyCombinerPostbuildPlanCompiler.GetAllowedWriteRangeSectionsForInPlaceRefresh(plan, capacity)
                    .Select(static section => section.Range));
            ByteRange[] actualWrites = Sorted(facts.WriteRanges.Values.Select(static range => range.Range));
            Assert.Equal(expectedWrites, actualWrites);
            Assert.Equal(plan.Profile.ToolBindingId, facts.ToolBindingId);
            (LegacyCombinerCommandFamily, string, string?)[] expectedCommands =
                [.. plan.Commands.Select(static command => (command.Family, command.ModeArgument, command.CrcArgument))];
            (LegacyCombinerCommandFamily, string, string?)[] actualCommands =
                [.. facts.Commands.Select(static command => (command.Family, command.ModeArgument, command.CrcArgument))];
            Assert.Equal(expectedCommands, actualCommands);
        }
    }

    /// <summary>The write ranges cover every storage location, so the box never shows less than the CRC words.</summary>
    [Fact]
    public void WriteRangesContainEveryStorageLocation()
    {
        foreach (LegacyCombinerPostbuildCommandPlan plan in AllPlans())
        {
            IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, CapacityFor(plan));

            Assert.All(facts.StorageLocations.Values, location => Assert.Contains(
                facts.WriteRanges.Values,
                range => range.Range.Start <= location.StorageRange.Start &&
                    location.StorageRange.EndExclusive <= range.Range.EndExclusive));
        }
    }

    /// <summary>A CRC-only route also rewrites ranges other than the CRC words, and the facts say so.</summary>
    [Fact]
    public void CrcOnlyRouteDeclaresWritesBeyondTheStorageLocations()
    {
        LegacyCombinerPostbuildCommandPlan plan = AllPlans().First(
            static plan => plan.Profile.IcId == "NT51927" && plan.Commands.Any(
                static command => command.Family == LegacyCombinerCommandFamily.CrcOnlyMode));

        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, CapacityFor(plan));

        long storageBytes = facts.StorageLocations.Values.Sum(static location => location.StorageRange.Length);
        long writeBytes = facts.WriteRanges.Values.Sum(static range => range.Range.Length);
        Assert.True(writeBytes > storageBytes, $"write {writeBytes} bytes, storage {storageBytes} bytes");
    }

    /// <summary>NT51926 cascade stores its header CRC words in the flash header section.</summary>
    [Fact]
    public void Nt51926CascadeStoresHeaderCrcInTheFlashHeader()
    {
        LegacyCombinerPostbuildCommandPlan plan = Nt51926Cascade();

        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, CapacityFor(plan));

        Assert.Equal(IntegrityRecomputeAvailability.Declared, facts.Availability);
        Assert.NotEmpty(facts.StorageLocations.Values);
        Assert.All(facts.StorageLocations.Values,
            static location => Assert.Equal(PostbuildWriteSectionIds.FlashHeaderCrc, location.SectionId));
        Assert.Contains(facts.StorageLocations.Values, static location => location.StorageRange.Contains(0x18));
    }

    /// <summary>A capacity below the plan's required capacity is rejected instead of projecting partial facts.</summary>
    [Fact]
    public void CapacityBelowTheRequiredCapacityIsRejected()
    {
        LegacyCombinerPostbuildCommandPlan plan = Nt51926Cascade();
        long required = LegacyCombinerPostbuildPlanCompiler.CalculateRequiredCapacity(plan, []);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => IntegrityRecomputeFacts.FromPostbuild(plan, required - 1));
    }

    /// <summary>Missing and unresolved routes keep distinct states and their issue; neither claims known-empty storage.</summary>
    [Fact]
    public void MissingAndUnresolvedRoutesStayDistinct()
    {
        CompositionIssue missingIssue = new("missing", "missing route", "postbuild");
        CompositionIssue unknownIssue = new("unknown", "unresolved route", "number");
        IntegrityRecomputeFacts missing = IntegrityRecomputeFacts.NoRecomputeRule(missingIssue);
        IntegrityRecomputeFacts unknown = IntegrityRecomputeFacts.Unknown(unknownIssue);

        Assert.Equal(IntegrityRecomputeAvailability.NoRecomputeRule, missing.Availability);
        Assert.Equal(IntegrityRecomputeAvailability.Unknown, unknown.Availability);
        Assert.Same(missingIssue, missing.Issue);
        Assert.Same(unknownIssue, unknown.Issue);
        IntegrityRecomputeFacts[] both = [missing, unknown];
        foreach (IntegrityRecomputeFacts facts in both)
        {
            Assert.Equal(MemoryLayoutFactStatus.Unavailable, facts.StorageLocations.Status);
            Assert.Equal(MemoryLayoutFactStatus.Unavailable, facts.WriteRanges.Status);
            Assert.Null(facts.ToolBindingId);
            Assert.Empty(facts.Commands);
        }
        Assert.Equal(MemoryLayoutFactUnavailableReason.NotDeclared, missing.StorageLocations.UnavailableReason);
        Assert.Equal(MemoryLayoutFactUnavailableReason.OutputLocationNotResolved, unknown.StorageLocations.UnavailableReason);
    }

    /// <summary>Coverage and algorithm names are not declared by today's profiles.</summary>
    [Fact]
    public void CoverageAndAlgorithmsAreUnavailableUntilDeclared()
    {
        LegacyCombinerPostbuildCommandPlan plan = Nt51926Cascade();
        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, CapacityFor(plan));

        Assert.Equal(MemoryLayoutFactUnavailableReason.NotDeclared, facts.Coverage.UnavailableReason);
        Assert.Equal(MemoryLayoutFactUnavailableReason.NotDeclared, facts.Algorithms.UnavailableReason);
    }

    /// <summary>Each user segment reports the declared locations it overlaps and the exact half-open overlap.</summary>
    [Fact]
    public void StorageHitsListEveryOverlapWithItsRange()
    {
        LegacyCombinerPostbuildCommandPlan plan = Nt51926Cascade();
        long capacity = CapacityFor(plan);
        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, capacity);
        IntegrityStorageLocation first = facts.StorageLocations.Values[0];
        ByteRange straddling = ByteRange.FromStartEndExclusive(first.StorageRange.Start - 2, first.StorageRange.Start + 1);
        ByteRange before = ByteRange.FromStartEndExclusive(0, first.StorageRange.Start);
        ByteRange far = new(capacity - 0x10, 0x10);

        MemoryLayoutFactSet<IntegrityStorageHit> hits = facts.FindStorageHits([straddling, before, far]);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, hits.Status);
        IntegrityStorageHit hit = Assert.Single(hits.Values);
        Assert.Equal(first, hit.Location);
        Assert.Equal(straddling, hit.UserSegment);
        Assert.Equal(new ByteRange(first.StorageRange.Start, 1), hit.Overlap);
    }

    /// <summary>A known route that no segment touches reports known-empty hits.</summary>
    [Fact]
    public void UntouchedStorageIsKnownEmpty()
    {
        LegacyCombinerPostbuildCommandPlan plan = Nt51926Cascade();
        long capacity = CapacityFor(plan);
        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, capacity);

        MemoryLayoutFactSet<IntegrityStorageHit> hits = facts.FindStorageHits([new ByteRange(capacity - 0x10, 0x10)]);

        Assert.Equal(MemoryLayoutFactStatus.KnownEmpty, hits.Status);
    }

    /// <summary>Unavailable storage gives unavailable hits instead of claiming that no CRC is touched.</summary>
    [Fact]
    public void UnknownFactsGiveUnavailableHits()
    {
        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.Unknown(null);

        MemoryLayoutFactSet<IntegrityStorageHit> hits = facts.FindStorageHits([new ByteRange(0, Capacity)]);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, hits.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.OutputLocationNotResolved, hits.UnavailableReason);
    }

    /// <summary>A segment inside a rewritten range but outside every storage location is a write hit only.</summary>
    [Fact]
    public void WriteRangeHitsIncludeRewrittenBytesOutsideTheStorageLocations()
    {
        LegacyCombinerPostbuildCommandPlan plan = AllPlans().First(candidate =>
            WriteOnlyByte(IntegrityRecomputeFacts.FromPostbuild(candidate, CapacityFor(candidate))) is not null);
        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, CapacityFor(plan));
        long address = WriteOnlyByte(facts)!.Value;
        ByteRange segment = new(address, 1);

        MemoryLayoutFactSet<IntegrityWriteRangeHit> writeHits = facts.FindWriteRangeHits([segment]);
        MemoryLayoutFactSet<IntegrityStorageHit> storageHits = facts.FindStorageHits([segment]);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, writeHits.Status);
        Assert.All(writeHits.Values, hit =>
        {
            Assert.Equal(segment, hit.UserSegment);
            Assert.Equal(segment, hit.Overlap);
            Assert.True(hit.Range.Range.Contains(address));
        });
        Assert.NotEqual(MemoryLayoutFactStatus.KnownWithValues, storageHits.Status);
    }

    /// <summary>A segment that overlaps a storage location is also a write hit, because writes contain storage.</summary>
    [Fact]
    public void StorageHitIsAlsoAWriteRangeHit()
    {
        LegacyCombinerPostbuildCommandPlan plan = Nt51926Cascade();
        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, CapacityFor(plan));
        ByteRange segment = facts.StorageLocations.Values[0].StorageRange;

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, facts.FindStorageHits([segment]).Status);
        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, facts.FindWriteRangeHits([segment]).Status);
    }

    /// <summary>Unavailable write ranges give unavailable write hits, and untouched ranges give known-empty hits.</summary>
    [Fact]
    public void WriteRangeHitsKeepTheUnavailableAndEmptyStates()
    {
        MemoryLayoutFactSet<IntegrityWriteRangeHit> unknown =
            IntegrityRecomputeFacts.Unknown(null).FindWriteRangeHits([new ByteRange(0, Capacity)]);
        MemoryLayoutFactSet<IntegrityWriteRangeHit> noRule =
            IntegrityRecomputeFacts.NoRecomputeRule(null).FindWriteRangeHits([new ByteRange(0, Capacity)]);
        LegacyCombinerPostbuildCommandPlan plan = Nt51926Cascade();
        long capacity = CapacityFor(plan);
        IntegrityRecomputeFacts facts = IntegrityRecomputeFacts.FromPostbuild(plan, capacity);
        long free = Enumerable.Range(0, (int)capacity)
            .Select(static offset => (long)offset)
            .First(offset => !facts.WriteRanges.Values.Any(range => range.Range.Contains(offset)));

        Assert.Equal(MemoryLayoutFactUnavailableReason.OutputLocationNotResolved, unknown.UnavailableReason);
        Assert.Equal(MemoryLayoutFactUnavailableReason.NotDeclared, noRule.UnavailableReason);
        Assert.Equal(MemoryLayoutFactStatus.KnownEmpty, facts.FindWriteRangeHits([new ByteRange(free, 1)]).Status);
    }

    private static long? WriteOnlyByte(IntegrityRecomputeFacts facts)
    {
        foreach (IntegrityRecomputeWriteRange range in facts.WriteRanges.Values)
        {
            for (long address = range.Range.Start; address < range.Range.EndExclusive; address++)
            {
                if (!facts.StorageLocations.Values.Any(location => location.StorageRange.Contains(address)))
                {
                    return address;
                }
            }
        }
        return null;
    }

    private static LegacyCombinerPostbuildCommandPlan Nt51926Cascade()
    {
        return LegacyCombinerPostbuildCatalog.Nt51926.ResolvePlan(
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));
    }

    private static long CapacityFor(LegacyCombinerPostbuildCommandPlan plan)
    {
        return Math.Max(Capacity, LegacyCombinerPostbuildPlanCompiler.CalculateRequiredCapacity(plan, []));
    }

    private static ByteRange[] Sorted(IEnumerable<ByteRange> ranges)
    {
        return [.. ranges.OrderBy(static range => range.Start).ThenBy(static range => range.Length)];
    }

    private static IEnumerable<LegacyCombinerPostbuildCommandPlan> AllPlans()
    {
        foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.All)
        {
            foreach (LegacyCombinerPostbuildPlanSelector selector in profile.PlanSelectors)
            {
                IcNumberInputMode mode = selector.Kind switch
                {
                    LegacyCombinerPostbuildPlanSelectorKind.SingleChip => IcNumberInputMode.SingleSelector,
                    LegacyCombinerPostbuildPlanSelectorKind.ExactCount => IcNumberInputMode.NumericSelector,
                    LegacyCombinerPostbuildPlanSelectorKind.GenericCascade or
                        LegacyCombinerPostbuildPlanSelectorKind.CountRange => IcNumberInputMode.CascadeSelector,
                    _ => throw new ArgumentOutOfRangeException(nameof(selector), selector.Kind, null),
                };
                yield return profile.ResolvePlan(new IcNumberSelection(mode, [selector.Token]));
            }
        }
    }
}
