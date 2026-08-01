using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>One concrete immutable logical-output input binding supplied at compile time.</summary>
internal sealed class V2LogicalOutputInputBinding
{
    internal V2LogicalOutputInputBinding(string bindingId, string slotId, int exactLengthBytes)
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
    internal int ExactLengthBytes { get; }
}

/// <summary>Typed General Merge overlay containing one exact initializer and explicit mappings.</summary>
internal sealed class V2LogicalOutputCompileRequest
{
    private readonly V2LogicalOutputInputBinding[] _bindings;
    private readonly ExplicitMapping[] _mappings;

    internal V2LogicalOutputCompileRequest(
        GeneralMergeOutputInitializer outputInitializer,
        IEnumerable<V2LogicalOutputInputBinding> bindings,
        IEnumerable<ExplicitMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(outputInitializer);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(mappings);
        _bindings = [.. bindings];
        _mappings = [.. mappings];
        OutputInitializer = outputInitializer;
        Bindings = Array.AsReadOnly(_bindings);
        Mappings = Array.AsReadOnly(_mappings);
    }

    /// <summary>Requested exact final logical output initialization.</summary>
    internal GeneralMergeOutputInitializer OutputInitializer { get; }

    /// <summary>Concrete immutable source bindings with no host paths or source bytes.</summary>
    internal IReadOnlyList<V2LogicalOutputInputBinding> Bindings { get; }

    /// <summary>Explicit source-to-output mappings lowered through the shared plan algebra.</summary>
    internal IReadOnlyList<ExplicitMapping> Mappings { get; }
}
