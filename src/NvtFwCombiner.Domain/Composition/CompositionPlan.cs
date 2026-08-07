namespace NvtFwCombiner.Domain.Composition;

/// <summary>Validated deterministic plan compiled from a profile and request.</summary>
public sealed partial class CompositionPlan
{
    private readonly Dictionary<string, AddressSpace> _addressSpacesById;
    private readonly Dictionary<string, ImageInitialization> _initializationsByTargetSpaceId;
    private readonly ImageInitialization[] _initializations;

    /// <summary>Creates a singleton-initializer plan through the canonical multi-buffer model.</summary>
    public CompositionPlan(
        ImageInitialization initialization,
        IEnumerable<AddressSpace> addressSpaces,
        IEnumerable<CompositionOperation> operations)
        : this(
            [RequireInitialization(initialization)],
            RequireInitialization(initialization).TargetSpaceId,
            addressSpaces,
            operations)
    {
    }

    /// <summary>Creates a plan with one engine-owned initializer for every mutable address space.</summary>
    public CompositionPlan(
        IEnumerable<ImageInitialization> initializations,
        string outputSpaceId,
        IEnumerable<AddressSpace> addressSpaces,
        IEnumerable<CompositionOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(initializations);
        OutputSpaceId = RequiredValue.NotBlank(outputSpaceId);
        ArgumentNullException.ThrowIfNull(addressSpaces);
        ArgumentNullException.ThrowIfNull(operations);

        AddressSpaces = [.. addressSpaces];
        OrderedOperations = [.. operations.OrderBy(operation => operation.Sequence).ThenBy(operation => operation.OperationId)];
        _addressSpacesById = BuildAddressSpaceIndex(AddressSpaces);
        (_initializations, _initializationsByTargetSpaceId) = BuildInitializationIndex(initializations);
        Initializations = Array.AsReadOnly(_initializations);

        ValidateInitializations();
        ValidateOperations();
    }

    /// <summary>Engine-owned initializers in ordinal target-space order.</summary>
    public IReadOnlyList<ImageInitialization> Initializations { get; }

    /// <summary>Mutable address space selected as the final composition output.</summary>
    public string OutputSpaceId { get; }

    /// <summary>Initializer selected by <see cref="OutputSpaceId"/>.</summary>
    public ImageInitialization OutputInitialization => _initializationsByTargetSpaceId[OutputSpaceId];

    /// <summary>Declared address spaces in the plan.</summary>
    public IReadOnlyList<AddressSpace> AddressSpaces { get; }

    /// <summary>Operations sorted by sequence and then operation id.</summary>
    public IReadOnlyList<CompositionOperation> OrderedOperations { get; }

    /// <summary>Immutable address spaces that must be provided by the application before execution.</summary>
    public IReadOnlyList<string> RequiredInputAddressSpaceIds =>
        [.. AddressSpaces
            .Where(addressSpace => addressSpace.Mutability == AddressSpaceMutability.Immutable)
            .Select(addressSpace => addressSpace.AddressSpaceId)
            .Order(StringComparer.Ordinal)];

    internal bool TryGetAddressSpace(string addressSpaceId, out AddressSpace? addressSpace)
    {
        return _addressSpacesById.TryGetValue(addressSpaceId, out addressSpace);
    }

    private static Dictionary<string, AddressSpace> BuildAddressSpaceIndex(IEnumerable<AddressSpace> addressSpaces)
    {
        Dictionary<string, AddressSpace> byId = new(StringComparer.Ordinal);
        foreach (AddressSpace addressSpace in addressSpaces)
        {
            if (addressSpace.Mutability != AddressSpaceMutability.Immutable &&
                (addressSpace.InputPaddingByte is not null ||
                    addressSpace.InputOversizePolicy != InputOversizePolicy.Reject ||
                    addressSpace.AllowedInputLengths.Count > 0 ||
                    addressSpace.ExpectedInputLengths.Count > 0))
            {
                throw new ArgumentException("Mutable address spaces cannot declare input size relaxation.", nameof(addressSpaces));
            }

            if (!byId.TryAdd(addressSpace.AddressSpaceId, addressSpace))
            {
                throw new ArgumentException(
                    $"Address space '{addressSpace.AddressSpaceId}' is declared more than once.",
                    nameof(addressSpaces));
            }
        }

        return byId;
    }

    private static (
        ImageInitialization[] Ordered,
        Dictionary<string, ImageInitialization> ByTargetSpaceId) BuildInitializationIndex(
        IEnumerable<ImageInitialization> initializations)
    {
        ImageInitialization[] ordered = ImmutableReferenceSnapshot.Create(
            initializations,
            "Composition plans require non-null mutable-space initializers.",
            requireValue: true);

        Array.Sort(ordered, static (left, right) =>
            StringComparer.Ordinal.Compare(left.TargetSpaceId, right.TargetSpaceId));
        Dictionary<string, ImageInitialization> byTargetSpaceId = new(StringComparer.Ordinal);
        foreach (ImageInitialization initialization in ordered)
        {
            if (!byTargetSpaceId.TryAdd(initialization.TargetSpaceId, initialization))
            {
                throw new ArgumentException(
                    $"Address space '{initialization.TargetSpaceId}' has more than one initializer.",
                    nameof(initializations));
            }
        }

        return (ordered, byTargetSpaceId);
    }

    private static ImageInitialization RequireInitialization(ImageInitialization? initialization)
    {
        return initialization ?? throw new ArgumentNullException(nameof(initialization));
    }

}
