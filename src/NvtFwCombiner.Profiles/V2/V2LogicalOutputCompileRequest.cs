using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>One concrete immutable input binding supplied for explicit mapping compilation.</summary>
internal sealed record V2ExplicitMappingInputBinding(
    string BindingId,
    string SlotId,
    long ExactLengthBytes);

internal abstract class V2ExplicitMappingCompileRequest
{
    protected V2ExplicitMappingCompileRequest(
        IEnumerable<V2ExplicitMappingInputBinding> bindings,
        IEnumerable<ExplicitMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(mappings);
        Bindings = Array.AsReadOnly([.. bindings]);
        Mappings = Array.AsReadOnly([.. mappings]);
    }

    internal IReadOnlyList<V2ExplicitMappingInputBinding> Bindings { get; }

    internal IReadOnlyList<ExplicitMapping> Mappings { get; }
}

/// <summary>Typed General Merge overlay containing one exact initializer and explicit mappings.</summary>
internal sealed class V2LogicalOutputCompileRequest : V2ExplicitMappingCompileRequest
{
    internal V2LogicalOutputCompileRequest(
        GeneralMergeOutputInitializer outputInitializer,
        IEnumerable<V2ExplicitMappingInputBinding> bindings,
        IEnumerable<ExplicitMapping> mappings)
        : base(bindings, mappings)
    {
        ArgumentNullException.ThrowIfNull(outputInitializer);
        OutputInitializer = outputInitializer;
    }

    /// <summary>Requested exact final logical output initialization.</summary>
    internal GeneralMergeOutputInitializer OutputInitializer { get; }

}
