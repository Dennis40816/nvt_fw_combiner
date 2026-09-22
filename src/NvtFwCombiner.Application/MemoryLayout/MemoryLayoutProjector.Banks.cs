using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

public static partial class MemoryLayoutProjector
{
    private static MemoryLayoutBankLocator[] ProjectBanks(
        CompiledComposition composition, string addressSpaceId, long capacity)
    {
        if (composition.V2Details.Provenance.Context is not RuntimeReferenceBankReplaceV2CompilationContext context)
        {
            return [];
        }

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
                bank.BankId == selected.BankId && bank.Range == selected.OutputRange));
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
