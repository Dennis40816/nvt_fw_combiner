// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>Projects write declarations through ordered copy placements without executing or compiling firmware.</summary>
internal static class MemoryLayoutOutputWriteProjection
{
    internal static MemoryLayoutFactSet<MemoryLayoutOutputWrite> Create(
        CompiledComposition? composition, string addressSpaceId,
        IReadOnlyDictionary<string, string> slotsBySpace, IReadOnlyList<MemoryLayoutBankLocator> banks)
    {
        if (composition is null)
        {
            return MemoryLayoutFactSet<MemoryLayoutOutputWrite>.Unavailable(
                MemoryLayoutFactUnavailableReason.CompiledPlanNotResolved);
        }

        CompositionPlan plan = composition.Plan;
        ByteRange output = new(0, plan.OutputInitialization.Capacity);
        List<MemoryLayoutOutputWrite> writes = [];
        for (int index = 0; index < plan.OrderedOperations.Count; index++)
        {
            CompositionOperation operation = plan.OrderedOperations[index];
            InputSlice[]? sources = CopySources(plan, operation, index, slotsBySpace);
            if (sources is null)
            {
                return MemoryLayoutFactSet<MemoryLayoutOutputWrite>.Unavailable(
                    MemoryLayoutFactUnavailableReason.OutputLocationNotResolved);
            }
            if (IsTransport(composition, operation, sources))
            {
                continue;
            }
            ByteRange[] contentRanges = sources.Length == 0
                ? [.. operation.DeclaredWriteRanges]
                : [.. sources.Select(static source => source.TargetRange)];
            foreach (ByteRange range in contentRanges)
            {
                Resolution resolution = FindPlacements(plan, operation.TargetSpaceId, range, index + 1);
                Placement[] placements = resolution.Placements;
                if (range.Subtract([.. placements.Select(static placement => placement.OriginalRange), .. resolution.Consumed]).Count != 0 ||
                    placements.Any(placement => !output.Contains(placement.OutputRange)))
                {
                    return MemoryLayoutFactSet<MemoryLayoutOutputWrite>.Unavailable(
                        MemoryLayoutFactUnavailableReason.OutputLocationNotResolved);
                }
                foreach (Placement placement in placements)
                {
                    AddWrites(writes, operation, sources.FirstOrDefault(source => source.TargetRange.Contains(range)),
                        placement, addressSpaceId, banks);
                }
            }
        }
        return MemoryLayoutFactSet<MemoryLayoutOutputWrite>.Known(writes.Distinct());
    }

    private static bool IsCopy(CompositionOperation operation)
    {
        return operation.Kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange;
    }

    private static bool IsTransport(CompiledComposition composition, CompositionOperation operation, InputSlice[] sources)
    {
        if (!IsCopy(operation))
        {
            return false;
        }
        if (composition.V2Details.Provenance.Context is RuntimeReferenceBankReplaceV2CompilationContext context &&
            context.Banks.Any(bank => MemoryLayoutProjector.IsBankSeed(operation, bank)))
        {
            return true;
        }
        // A copy of an input-cloned image carries selected content (for example TPB after relocation).
        // Other mutable-source copies only carry declarations made by earlier content operations.
        // Invariant: an input-clone copy has no source slice only when earlier writes cover its whole
        // source range. Those earlier writes are followed through this copy by FindPlacements, so
        // treating the copy as transport loses no byte.
        return sources.Length == 0 && composition.Plan.AddressSpaces.Any(space =>
            space.AddressSpaceId == operation.SourceSpaceId && space.Mutability == AddressSpaceMutability.Mutable);
    }

    /// <summary>Returns the input slices a copy carries, or null when its clone reference has no bound input slot.</summary>
    private static InputSlice[]? CopySources(CompositionPlan plan, CompositionOperation operation, int before,
        IReadOnlyDictionary<string, string> slotsBySpace)
    {
        if (!IsCopy(operation) || operation.SourceSpaceId is not { } sourceSpace ||
            operation.SourceRange is not { } sourceRange || sourceSpace == plan.OutputSpaceId)
        {
            return [];
        }
        if (slotsBySpace.TryGetValue(sourceSpace, out string? slot))
        {
            return [new InputSlice(slot, sourceRange, operation.TargetRange)];
        }
        ImageInitialization? initialization = plan.Initializations.FirstOrDefault(candidate =>
            candidate.TargetSpaceId == sourceSpace);
        if (initialization?.ReferenceSpaceId is not { } reference)
        {
            return [];
        }
        if (!slotsBySpace.TryGetValue(reference, out slot))
        {
            // A clone of a space that is not a bound input (for example a clone of a clone) can carry
            // input bytes this projection cannot attribute. Report the set as unresolved.
            return null;
        }
        // Clone initialization preserves offsets. Every earlier write into the clone (copy, fill, patch,
        // transform or processor) is followed from its own step, so its bytes are not attributed to the
        // input here. The rest still carries this input clone.
        ByteRange[] replaced = [.. plan.OrderedOperations.Take(before)
            .Where(candidate => candidate.TargetSpaceId == sourceSpace)
            .SelectMany(static candidate => candidate.DeclaredWriteRanges)];
        return [.. sourceRange.Subtract(replaced).Select(range => new InputSlice(slot, range,
            new ByteRange(checked(operation.TargetRange.Start + range.Start - sourceRange.Start), range.Length)))];
    }

    private static Resolution FindPlacements(CompositionPlan plan, string spaceId, ByteRange range, int after)
    {
        if (spaceId == plan.OutputSpaceId)
        {
            return new Resolution([new Placement(range, range)], []);
        }

        List<Placement> placements = [];
        List<ByteRange> consumed = [];
        List<ByteRange> remaining = [range];
        for (int index = after; index < plan.OrderedOperations.Count; index++)
        {
            CompositionOperation copy = plan.OrderedOperations[index];
            if (IsCopy(copy) && copy.SourceSpaceId == spaceId && copy.SourceRange is { } read)
            {
                foreach (ByteRange live in remaining)
                {
                    if (live.Intersect(read) is not { } intersection)
                    {
                        continue;
                    }
                    ByteRange target = new(checked(copy.TargetRange.Start + intersection.Start - read.Start), intersection.Length);
                    Resolution next = FindPlacements(plan, copy.TargetSpaceId, target, index + 1);
                    foreach (Placement placement in next.Placements)
                    {
                        ByteRange original = new(checked(intersection.Start + placement.OriginalRange.Start - target.Start), placement.OriginalRange.Length);
                        placements.Add(new Placement(original, placement.OutputRange));
                    }
                    consumed.AddRange(next.Consumed.Select(dead =>
                        new ByteRange(checked(intersection.Start + dead.Start - target.Start), dead.Length)));
                }
            }
            if (copy.TargetSpaceId == spaceId && (IsCopy(copy) || copy.Kind == CompositionOperationKind.FillRange))
            {
                // A later content copy/fill can reuse this workspace for another placement. Do not carry
                // an earlier declaration across that reset. Scalar/processor overlays retain its geometry.
                // Overwritten bytes never reach the output, so they are consumed, not unresolved.
                consumed.AddRange(remaining.SelectMany(live => copy.DeclaredWriteRanges
                    .Select(write => live.Intersect(write)).OfType<ByteRange>()));
                remaining = [.. remaining.SelectMany(live => live.Subtract(copy.DeclaredWriteRanges))];
            }
        }
        return new Resolution([.. placements.Distinct().OrderBy(static placement => placement.OutputRange.Start)
            .ThenBy(static placement => placement.OriginalRange.Start)], [.. consumed]);
    }

    private static void AddWrites(List<MemoryLayoutOutputWrite> writes, CompositionOperation operation,
        InputSlice? source, Placement placement, string addressSpaceId, IReadOnlyList<MemoryLayoutBankLocator> banks)
    {
        long[] boundaries = [.. banks.Where(bank => bank.AddressSpaceId == addressSpaceId).Select(static bank => bank.Range.Start)
            .Concat(banks.Where(bank => bank.AddressSpaceId == addressSpaceId).Select(static bank => bank.Range.EndExclusive))
            .Where(placement.OutputRange.Contains)
            .Append(placement.OutputRange.Start).Append(placement.OutputRange.EndExclusive).Distinct().Order()];
        for (int index = 1; index < boundaries.Length; index++)
        {
            ByteRange output = ByteRange.FromStartEndExclusive(boundaries[index - 1], boundaries[index]);
            string? bankId = banks.FirstOrDefault(bank => bank.AddressSpaceId == addressSpaceId && bank.Range.Contains(output))?.BankId;
            ByteRange? input = source is null ? null : new ByteRange(
                checked(source.Range.Start + placement.OriginalRange.Start - source.TargetRange.Start + output.Start - placement.OutputRange.Start),
                output.Length);
            writes.Add(new MemoryLayoutOutputWrite(operation.OperationId,
                new FirmwareAddressedRange(addressSpaceId, output), source?.SlotId, input, bankId));
        }
    }

    private sealed record InputSlice(string SlotId, ByteRange Range, ByteRange TargetRange);
    private sealed record Placement(ByteRange OriginalRange, ByteRange OutputRange);
    private sealed record Resolution(Placement[] Placements, ByteRange[] Consumed);
}
