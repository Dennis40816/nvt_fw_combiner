using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>One concrete immutable input binding supplied for a map-bound runtime reference-replace request.</summary>
internal sealed class V2RuntimeReferenceReplaceInputBinding
{
    internal V2RuntimeReferenceReplaceInputBinding(string bindingId, string slotId, long exactLengthBytes)
    {
        BindingId = bindingId;
        SlotId = slotId;
        ExactLengthBytes = exactLengthBytes;
    }

    /// <summary>Concrete immutable address-space identity for this compile request.</summary>
    internal string BindingId { get; }

    /// <summary>Profile slot materialized by this concrete binding.</summary>
    internal string SlotId { get; }

    /// <summary>Exact immutable source capacity expected by the resulting plan.</summary>
    internal long ExactLengthBytes { get; }
}

/// <summary>Typed map-bound General Replace overlay containing only input lengths and explicit half-open mappings.</summary>
internal sealed class V2RuntimeReferenceReplaceCompileRequest
{
    private readonly V2RuntimeReferenceReplaceInputBinding[] _bindings;
    private readonly ExplicitMapping[] _mappings;

    internal V2RuntimeReferenceReplaceCompileRequest(
        IEnumerable<V2RuntimeReferenceReplaceInputBinding> bindings,
        IEnumerable<ExplicitMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(mappings);
        _bindings = [.. bindings];
        _mappings = [.. mappings];
        Bindings = Array.AsReadOnly(_bindings);
        Mappings = Array.AsReadOnly(_mappings);
    }

    /// <summary>Concrete immutable inputs with no host paths, source bytes, or process authority.</summary>
    internal IReadOnlyList<V2RuntimeReferenceReplaceInputBinding> Bindings { get; }

    /// <summary>Explicit source-to-output mappings lowered through the shared composition plan algebra.</summary>
    internal IReadOnlyList<ExplicitMapping> Mappings { get; }
}
