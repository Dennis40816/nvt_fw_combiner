using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.TestSupport;

internal static class CompiledInputSlotTestFactory
{
    internal static CompiledInputSlotRequirement Create(
        string slotId,
        string role,
        CompiledInputArtifactClass artifactClass,
        bool required,
        CompiledInputSlotCardinality cardinality,
        IEnumerable<string> acceptedExtensions,
        CompiledInputLengthRequirement lengthRequirement,
        CompiledInputNormalization normalization)
    {
        var definition = new CompositionInputSlotDefinition(
            slotId,
            role,
            artifactClass,
            required,
            cardinality,
            acceptedExtensions,
            lengthRequirement,
            normalization);
        return new CompiledInputSlotRequirement(definition, lengthRequirement);
    }
}
