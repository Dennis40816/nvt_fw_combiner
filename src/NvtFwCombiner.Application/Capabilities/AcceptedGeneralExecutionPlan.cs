using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Application-owned immutable General execution evidence retained by one exact accepted
/// compilation. Execution consumes this plan and never reconstructs admission from authoring input.
/// </summary>
public sealed record AcceptedGeneralExecutionPlan
{
    private readonly InputArtifactBinding[] _inputBindings;
    private readonly ReadOnlyDictionary<string, ReadOnlyMemory<byte>> _virtualArtifacts;

    /// <summary>Creates one exact admitted General execution plan.</summary>
    public AcceptedGeneralExecutionPlan(
        GeneralAuthoringAdmissionResult admission,
        IEnumerable<InputArtifactBinding> inputBindings,
        IReadOnlyDictionary<string, byte[]>? virtualArtifacts = null,
        IcNumberSelection? icNumberSelection = null)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(inputBindings);
        if (!admission.IsAdmitted)
        {
            throw new ArgumentException(
                "A General execution plan requires one admitted authoring result.",
                nameof(admission));
        }

        _inputBindings = [.. inputBindings];
        if (_inputBindings.Any(static binding => binding is null) ||
            _inputBindings.Select(static binding => binding.AddressSpaceId)
                .Distinct(StringComparer.Ordinal).Count() != _inputBindings.Length)
        {
            throw new ArgumentException(
                "General execution bindings must be non-null and use unique address spaces.",
                nameof(inputBindings));
        }

        Admission = admission;
        InputBindings = Array.AsReadOnly(_inputBindings);
        _virtualArtifacts = new ReadOnlyDictionary<string, ReadOnlyMemory<byte>>(
            (virtualArtifacts ?? new Dictionary<string, byte[]>(StringComparer.Ordinal))
            .ToDictionary(
                static pair => pair.Key,
                static pair => (ReadOnlyMemory<byte>)pair.Value.ToArray(),
                StringComparer.Ordinal));
        IcNumberSelection = icNumberSelection;
    }

    /// <summary>Exact admitted draft, limits, resources, and occupancy evidence.</summary>
    public GeneralAuthoringAdmissionResult Admission { get; }

    /// <summary>Exact compiler address-space bindings in canonical order.</summary>
    public IReadOnlyList<InputArtifactBinding> InputBindings { get; }

    /// <summary>Exact immutable host-created patch artifacts keyed by compiled artifact id.</summary>
    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> VirtualArtifacts => _virtualArtifacts;

    /// <summary>Exact accepted IC-number selection for General Replace, or null for General Merge.</summary>
    public IcNumberSelection? IcNumberSelection { get; }
}
