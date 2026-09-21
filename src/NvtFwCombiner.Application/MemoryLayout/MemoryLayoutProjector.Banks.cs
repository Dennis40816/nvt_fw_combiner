using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.MemoryLayout;

public static partial class MemoryLayoutProjector
{
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
