using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.MemoryLayout;

public static partial class MemoryLayoutProjector
{
    private static ProjectionRegion[] SelectBankPrimaryRegions(
        RuntimeReferenceBankReplaceV2CompilationContext context, IReadOnlyList<CtrlRamRegion>? ctrlRamRegions)
    {
        FirmwareImageMap map = context.ResolvedMap.ImageMap;
        ProjectionRegion[] physical = SelectPrimaryRegions(map, null);
        if (ctrlRamRegions is null)
        {
            return physical;
        }

        // The checked context already binds every local compilation to Definition.Local.
        // Reuse that shared geometry for preserved banks, never their selected execution obligations.
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap local = context.Banks[0].LocalComposition.V2Details.Provenance.ResolvedMap;
        ProjectionRegion[] localRegions = SelectPrimaryRegions(local.ImageMap, ctrlRamRegions);
        MemoryLayoutBankLocator[] banks = ProjectBankLocators(context, map.AddressSpaceId, map.CapacityBytes);
        var placed = new List<(MemoryLayoutBankRegion Attribution, ProjectionRegion Local)>();
        foreach (MemoryLayoutBankLocator bank in banks)
        {
            foreach (ProjectionRegion region in localRegions.Where(static region => region.ContentRole == MemoryContentRole.CtrlRam))
            {
                placed.Add((new MemoryLayoutBankRegion(bank, local, region.CanonicalRegion!), region));
            }
        }
        if (placed.Any(left => placed.Any(right => !ReferenceEquals(left.Attribution, right.Attribution) &&
            left.Attribution.Range.Overlaps(right.Attribution.Range))))
        {
            throw new MemoryLayoutDisplayProjectionException("Placed CtrlRAM declarations must be unambiguous.", nameof(ctrlRamRegions));
        }
        long[] boundaries = [.. physical.SelectMany(static region => new[] { region.Range.Start, region.Range.EndExclusive })
            .Concat(placed.SelectMany(static item => new[] { item.Attribution.Range.Start, item.Attribution.Range.EndExclusive }))
            .Distinct().Order()];
        var result = new List<ProjectionRegion>();
        for (int i = 1; i < boundaries.Length; i++)
        {
            var range = ByteRange.FromStartEndExclusive(boundaries[i - 1], boundaries[i]);
            ProjectionRegion original = physical.Single(region => region.Range.Contains(range));
            (MemoryLayoutBankRegion? attribution, ProjectionRegion? detail) = placed
                .SingleOrDefault(item => item.Attribution.Range.Contains(range));
            result.Add(original with
            {
                Range = range,
                ContentRole = detail?.ContentRole ?? original.ContentRole,
                RegionGroup = detail?.RegionGroup ?? ReplaceRegionGroup.Base,
                CtrlRamRegionRole = detail?.CtrlRamRegionRole ?? original.CtrlRamRegionRole,
                BankRegion = attribution,
            });
        }
        return [.. result];
    }

    private static MemoryLayoutBankLocator[] ProjectBanks(
        CompiledComposition composition, string addressSpaceId, long capacity)
    {
        return composition.V2Details.Provenance.Context is RuntimeReferenceBankReplaceV2CompilationContext context
            ? ProjectBankLocators(context, addressSpaceId, capacity) : [];
    }

    private static MemoryLayoutBankLocator[] ProjectBankLocators(
        RuntimeReferenceBankReplaceV2CompilationContext context, string addressSpaceId, long capacity)
    {
        FirmwareImageMap map = context.ResolvedMap.ImageMap;
        var output = new ByteRange(0, capacity);
        // Selected obligations omit preserved banks; the accepted canonical map owns both placements.
        MemoryLayoutBankLocator[] banks = [.. map.Regions
            .Where(static region => region.RegionId is "a-bank" or "b-bank")
            .OrderBy(static region => region.Range.Start)
            .Select(region => new MemoryLayoutBankLocator(region.RegionId, map.AddressSpaceId, region.Range))];
        bool invalid = map.AddressSpaceId != addressSpaceId || banks.Length != 2 ||
            banks.Select(static bank => bank.BankId).Distinct(StringComparer.Ordinal).Count() != 2 ||
            banks.Any(bank => !output.Contains(bank.Range)) || banks[0].Range.Overlaps(banks[1].Range) ||
            context.Banks.Any(selected => !banks.Any(bank =>
                bank.BankId == selected.BankId && bank.Range.Contains(selected.OutputRange) &&
                selected.OutputRange == new ByteRange(
                    checked(bank.Range.Start + context.Definition.LocalBankRange.Start),
                    context.Definition.LocalBankRange.Length)));
        return invalid
            ? throw new InvalidOperationException("Bank display locators must agree with the complete canonical output and selected bank obligations.")
            : banks;
    }

    // Display coordinates only. The original operation remains the execution/report authority.
    private sealed record ProjectedOperation(CompositionOperation Operation, IReadOnlyList<ByteRange> Ranges);

    private static ProjectedOperation[] ProjectOperations(CompiledComposition composition)
    {
        CompositionPlan plan = composition.Plan;
        if (composition.V2Details.Provenance.Context is not RuntimeReferenceBankReplaceV2CompilationContext context)
        {
            return [.. plan.OrderedOperations.Where(operation => operation.TargetSpaceId == plan.OutputSpaceId)
                .Select(static operation => new ProjectedOperation(operation, operation.DeclaredWriteRanges))];
        }
        var projected = new List<ProjectedOperation>();
        foreach (CompositionOperation operation in plan.OrderedOperations)
        {
            if (operation.TargetSpaceId == plan.OutputSpaceId &&
                operation.Kind == CompositionOperationKind.RunExternalProcessor)
            {
                projected.Add(new ProjectedOperation(operation, operation.DeclaredWriteRanges));
                continue;
            }
            CompiledReferenceBank? bank = context.Banks.SingleOrDefault(bank => bank.WorkspaceId == operation.TargetSpaceId);
            if (bank is null)
            {
                continue;
            }
            var wholeBank = new ByteRange(0, bank.OutputRange.Length);
            if (operation.OperationId == $"{bank.BankId}/seed" &&
                operation.Kind == CompositionOperationKind.CopyRange && operation.SourceSpaceId == bank.Reference.ArtifactId &&
                operation.SourceRange == wholeBank && operation.TargetRange == wholeBank)
            {
                continue;
            }
            ByteRange[] ranges = [.. operation.DeclaredWriteRanges.Select(range =>
                new ByteRange(checked(bank.OutputRange.Start + range.Start), range.Length))];
            if (ranges.Any(range => !bank.OutputRange.Contains(range)))
            {
                throw new InvalidOperationException("Bank display effects must remain inside their checked output range.");
            }
            projected.Add(new(operation, ranges));
        }
        return [.. projected];
    }
}
