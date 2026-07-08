namespace NvtFwCombiner.Domain.Composition;

/// <summary>Validated deterministic plan compiled from a profile and request.</summary>
public sealed partial class CompositionPlan
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
            if (addressSpace.Mutability != AddressSpaceMutability.Immutable &&
                (addressSpace.InputPaddingByte is not null ||
                    addressSpace.InputOversizePolicy != InputOversizePolicy.Reject ||
                    addressSpace.AllowedInputLengths.Count > 0))
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

}
