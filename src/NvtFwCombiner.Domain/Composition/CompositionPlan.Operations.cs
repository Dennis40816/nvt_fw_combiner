namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompositionPlan
{
    private void ValidateOperations()
    {
        HashSet<string> operationIds = new(StringComparer.Ordinal);
        List<CompositionOperation> priorWrites = [];
        foreach (CompositionOperation operation in OrderedOperations)
        {
            if (!operationIds.Add(operation.OperationId))
            {
                throw new ArgumentException(
                    $"Operation '{operation.OperationId}' is declared more than once.",
                    nameof(OrderedOperations));
            }

            ValidateOperationReferences(operation);
            ValidateOperationOverlap(operation, priorWrites);
            priorWrites.Add(operation);
        }
    }

    private void ValidateOperationReferences(CompositionOperation operation)
    {
        if (!_addressSpacesById.TryGetValue(operation.TargetSpaceId, out AddressSpace? targetSpace))
        {
            throw new ArgumentException(
                $"Operation '{operation.OperationId}' targets undeclared address space '{operation.TargetSpaceId}'.",
                nameof(operation));
        }

        if (targetSpace.Mutability != AddressSpaceMutability.Mutable)
        {
            throw new ArgumentException(
                $"Operation '{operation.OperationId}' targets immutable address space '{operation.TargetSpaceId}'.",
                nameof(operation));
        }

        if (!targetSpace.Contains(operation.TargetRange))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                $"Operation '{operation.OperationId}' target range is outside address space '{operation.TargetSpaceId}'.");
        }

        if (operation.Kind == CompositionOperationKind.RunExternalProcessor)
        {
            ValidateExternalProcessorRanges(operation, targetSpace);
        }

        if (operation.SourceSpaceId is not null && operation.SourceRange is not null)
        {
            ValidateSourceRange(operation);
        }
    }

    private void ValidateExternalProcessorRanges(
        CompositionOperation operation,
        AddressSpace targetSpace)
    {
        ExternalProcessorInvocation invocation = operation.ExternalProcessorInvocation
            ?? throw new ArgumentException(
                $"Operation '{operation.OperationId}' is missing an external processor invocation.",
                nameof(operation));

        if (operation.TargetRange.Start != 0 || operation.TargetRange.Length != targetSpace.Length)
        {
            throw new ArgumentException(
                $"Operation '{operation.OperationId}' external processor target range must cover the full target address space.",
                nameof(operation));
        }

        foreach (ByteRange range in invocation.AllowedReadRanges)
        {
            if (!operation.TargetRange.Contains(range) || !targetSpace.Contains(range))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    $"Operation '{operation.OperationId}' allowed read range is outside the staged target range.");
            }
        }

        foreach (ByteRange range in invocation.AllowedWriteRanges)
        {
            if (!operation.TargetRange.Contains(range) || !targetSpace.Contains(range))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    $"Operation '{operation.OperationId}' allowed write range is outside the staged target range.");
            }
        }

        foreach (ExternalProcessorStagedSourceBinding binding in invocation.StagedSourceBindings)
        {
            if (!_addressSpacesById.TryGetValue(binding.SourceSpaceId, out AddressSpace? sourceSpace))
            {
                throw new ArgumentException(
                    $"Operation '{operation.OperationId}' staged source reads undeclared address space '{binding.SourceSpaceId}'.",
                    nameof(operation));
            }

            if (!sourceSpace.Contains(binding.SourceRange))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    $"Operation '{operation.OperationId}' staged source range is outside address space '{binding.SourceSpaceId}'.");
            }

            if (sourceSpace.Mutability != AddressSpaceMutability.Immutable)
            {
                throw new ArgumentException(
                    $"Operation '{operation.OperationId}' staged source address space '{binding.SourceSpaceId}' must be immutable.",
                    nameof(operation));
            }

            if (!operation.TargetRange.Contains(binding.FirmwareRange) || !targetSpace.Contains(binding.FirmwareRange))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    $"Operation '{operation.OperationId}' staged source firmware range is outside the staged target range.");
            }
        }
    }

    private void ValidateSourceRange(CompositionOperation operation)
    {
        if (!_addressSpacesById.TryGetValue(operation.SourceSpaceId!, out AddressSpace? sourceSpace))
        {
            throw new ArgumentException(
                $"Operation '{operation.OperationId}' reads undeclared address space '{operation.SourceSpaceId}'.",
                nameof(operation));
        }

        if (!sourceSpace.Contains(operation.SourceRange!.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                $"Operation '{operation.OperationId}' source range is outside address space '{operation.SourceSpaceId}'.");
        }
    }

    private void ValidateOperationOverlap(
        CompositionOperation operation,
        IReadOnlyList<CompositionOperation> priorWrites)
    {
        foreach (CompositionOperation prior in priorWrites)
        {
            if (CreatesSameSequenceMutableDependency(prior, operation))
            {
                throw new ArgumentException(
                    $"Operations '{prior.OperationId}' and '{operation.OperationId}' use a mutable read/write dependency with the same sequence.",
                    nameof(priorWrites));
            }

            if (!string.Equals(prior.TargetSpaceId, operation.TargetSpaceId, StringComparison.Ordinal) ||
                !DeclaredWriteRangesOverlap(prior, operation))
            {
                continue;
            }

            if (prior.Sequence == operation.Sequence)
            {
                throw new ArgumentException(
                    $"Operations '{prior.OperationId}' and '{operation.OperationId}' overlap with the same sequence.",
                    nameof(priorWrites));
            }

            if (operation.OverlapPolicy == OverlapPolicy.AllowDeclared)
            {
                throw new ArgumentException(
                    $"Operation '{operation.OperationId}' uses allow-declared overlap without validation evidence.",
                    nameof(priorWrites));
            }

            if (operation.OverlapPolicy == OverlapPolicy.Reject)
            {
                throw new ArgumentException(
                    $"Operation '{operation.OperationId}' overlaps earlier operation '{prior.OperationId}' without declared overlap policy.",
                    nameof(priorWrites));
            }
        }
    }

    private static bool DeclaredWriteRangesOverlap(CompositionOperation first, CompositionOperation second)
    {
        foreach (ByteRange firstRange in DeclaredWriteRanges(first))
        {
            foreach (ByteRange secondRange in DeclaredWriteRanges(second))
            {
                if (firstRange.Overlaps(secondRange))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IReadOnlyList<ByteRange> DeclaredWriteRanges(CompositionOperation operation)
    {
        return operation.Kind == CompositionOperationKind.RunExternalProcessor
            ? operation.ExternalProcessorInvocation!.AllowedWriteRanges
            : [operation.TargetRange];
    }

    private bool CreatesSameSequenceMutableDependency(
        CompositionOperation first,
        CompositionOperation second)
    {
        return first.Sequence == second.Sequence &&
            (ReadsMutableWrite(first, second) || ReadsMutableWrite(second, first));
    }

    private bool ReadsMutableWrite(CompositionOperation reader, CompositionOperation writer)
    {
        return (reader.Kind == CompositionOperationKind.RunExternalProcessor &&
                string.Equals(reader.TargetSpaceId, writer.TargetSpaceId, StringComparison.Ordinal) &&
                reader.ExternalProcessorInvocation!.AllowedReadRanges.Any(range => range.Overlaps(writer.TargetRange))) ||
            (reader.SourceSpaceId is not null &&
             reader.SourceRange is not null &&
             string.Equals(reader.SourceSpaceId, writer.TargetSpaceId, StringComparison.Ordinal) &&
             _addressSpacesById[reader.SourceSpaceId].Mutability == AddressSpaceMutability.Mutable &&
             reader.SourceRange.Value.Overlaps(writer.TargetRange));
    }

    private bool RequiresSeededMutableAddressSpace(string addressSpaceId)
    {
        return OrderedOperations.Any(operation =>
            string.Equals(operation.TargetSpaceId, addressSpaceId, StringComparison.Ordinal) ||
            string.Equals(operation.SourceSpaceId, addressSpaceId, StringComparison.Ordinal));
    }
}
