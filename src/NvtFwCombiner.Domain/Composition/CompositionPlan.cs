namespace NvtFwCombiner.Domain.Composition;

/// <summary>Validated deterministic plan compiled from a profile and request.</summary>
public sealed class CompositionPlan
{
    private readonly Dictionary<string, AddressSpace> _addressSpacesById;

    /// <summary>Creates a plan and validates address spaces, operation references, bounds, and overlap policy.</summary>
    public CompositionPlan(
        ImageInitialization initialization,
        IEnumerable<AddressSpace> addressSpaces,
        IEnumerable<CompositionOperation> operations,
        CompositionPlanProvenance? provenance = null)
    {
        ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(addressSpaces);
        ArgumentNullException.ThrowIfNull(operations);

        Initialization = initialization;
        AddressSpaces = [.. addressSpaces];
        OrderedOperations = [.. operations.OrderBy(operation => operation.Sequence).ThenBy(operation => operation.OperationId)];
        Provenance = provenance;
        _addressSpacesById = BuildAddressSpaceIndex(AddressSpaces);

        ValidateInitialization();
        ValidateOperations();
    }

    /// <summary>Image initialization applied before operations execute.</summary>
    public ImageInitialization Initialization { get; }

    /// <summary>Declared address spaces in the plan.</summary>
    public IReadOnlyList<AddressSpace> AddressSpaces { get; }

    /// <summary>Operations sorted by sequence and then operation id.</summary>
    public IReadOnlyList<CompositionOperation> OrderedOperations { get; }

    /// <summary>Profile identity carried by compiler-created plans.</summary>
    public CompositionPlanProvenance? Provenance { get; }

    /// <summary>Immutable address spaces that must be provided by the application before execution.</summary>
    public IReadOnlyList<string> RequiredInputAddressSpaceIds =>
        [.. AddressSpaces
            .Where(addressSpace => addressSpace.Mutability == AddressSpaceMutability.Immutable)
            .Select(addressSpace => addressSpace.AddressSpaceId)
            .Order(StringComparer.Ordinal)];

    /// <summary>Mutable non-output address spaces that must be seeded before execution.</summary>
    public IReadOnlyList<string> RequiredSeededMutableAddressSpaceIds =>
        [.. AddressSpaces
            .Where(addressSpace =>
                addressSpace.Mutability == AddressSpaceMutability.Mutable &&
                !string.Equals(addressSpace.AddressSpaceId, Initialization.TargetSpaceId, StringComparison.Ordinal) &&
                RequiresSeededMutableAddressSpace(addressSpace.AddressSpaceId))
            .Select(addressSpace => addressSpace.AddressSpaceId)
            .Order(StringComparer.Ordinal)];

    internal AddressSpace GetAddressSpace(string addressSpaceId)
    {
        return _addressSpacesById[addressSpaceId];
    }

    private static Dictionary<string, AddressSpace> BuildAddressSpaceIndex(IEnumerable<AddressSpace> addressSpaces)
    {
        Dictionary<string, AddressSpace> byId = new(StringComparer.Ordinal);
        foreach (AddressSpace addressSpace in addressSpaces)
        {
            if (!byId.TryAdd(addressSpace.AddressSpaceId, addressSpace))
            {
                throw new ArgumentException(
                    $"Address space '{addressSpace.AddressSpaceId}' is declared more than once.",
                    nameof(addressSpaces));
            }
        }

        return byId;
    }

    private void ValidateInitialization()
    {
        if (!_addressSpacesById.TryGetValue(Initialization.TargetSpaceId, out AddressSpace? targetSpace))
        {
            throw new ArgumentException(
                $"Initialization target address space '{Initialization.TargetSpaceId}' is not declared.",
                nameof(Initialization));
        }

        if (targetSpace.Mutability != AddressSpaceMutability.Mutable)
        {
            throw new ArgumentException("Initialization target address space must be mutable.", nameof(Initialization));
        }

        if (targetSpace.Length != Initialization.Capacity)
        {
            throw new ArgumentException("Initialization capacity must match target address-space length.", nameof(Initialization));
        }

        if (Initialization.Kind == ImageInitializationKind.Reference)
        {
            ValidateReferenceInitialization();
        }
    }

    private void ValidateReferenceInitialization()
    {
        if (Initialization.ReferenceSpaceId is null)
        {
            throw new ArgumentException("Reference initialization requires a reference address space id.", nameof(Initialization));
        }

        if (!_addressSpacesById.TryGetValue(Initialization.ReferenceSpaceId, out AddressSpace? referenceSpace))
        {
            throw new ArgumentException(
                $"Reference address space '{Initialization.ReferenceSpaceId}' is not declared.",
                nameof(Initialization));
        }

        if (referenceSpace.Mutability != AddressSpaceMutability.Immutable)
        {
            throw new ArgumentException("Reference address space must be immutable.", nameof(Initialization));
        }

        if (referenceSpace.Length != Initialization.Capacity)
        {
            throw new ArgumentException("Reference address-space length must match initialization capacity.", nameof(Initialization));
        }
    }

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

    private static void ValidateExternalProcessorRanges(
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
        return reader.SourceSpaceId is not null &&
            reader.SourceRange is not null &&
            string.Equals(reader.SourceSpaceId, writer.TargetSpaceId, StringComparison.Ordinal) &&
            _addressSpacesById[reader.SourceSpaceId].Mutability == AddressSpaceMutability.Mutable &&
            reader.SourceRange.Value.Overlaps(writer.TargetRange);
    }

    private bool RequiresSeededMutableAddressSpace(string addressSpaceId)
    {
        return OrderedOperations.Any(operation =>
            string.Equals(operation.TargetSpaceId, addressSpaceId, StringComparison.Ordinal) ||
            string.Equals(operation.SourceSpaceId, addressSpaceId, StringComparison.Ordinal));
    }
}
