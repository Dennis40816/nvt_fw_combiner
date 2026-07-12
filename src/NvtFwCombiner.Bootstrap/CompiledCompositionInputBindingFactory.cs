using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Projects one compiled input contract into an immutable runtime artifact binding.</summary>
internal static class CompiledCompositionInputBindingFactory
{
    internal static InputArtifactBinding Create(
        CompiledComposition compiledComposition,
        string addressSpaceId,
        string artifactId)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        if (compiledComposition.V2Details is not { } details)
        {
            return new InputArtifactBinding(addressSpaceId, addressSpaceId, artifactId);
        }

        CompiledInputSpaceBinding spaceBinding = details.InputContract.SpaceBindings.SingleOrDefault(binding =>
            StringComparer.Ordinal.Equals(binding.AddressSpaceId, addressSpaceId)) ?? throw new InvalidOperationException(
            $"V2 compiled input contract does not declare address space '{addressSpaceId}'.");
        CompiledInputSlotRequirement slot = details.InputContract.Slots.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.SlotId, spaceBinding.SlotId)) ?? throw new InvalidOperationException(
            $"V2 compiled input contract does not declare slot '{spaceBinding.SlotId}'.");
        string originalFileName = Path.GetFileName(artifactId);
        return string.IsNullOrWhiteSpace(originalFileName)
            ? throw new ArgumentException("V2 input artifacts require a plain original filename.", nameof(artifactId))
            : new InputArtifactBinding(
            addressSpaceId,
            addressSpaceId,
            artifactId,
            originalFileName,
            slot.ArtifactClass);
    }
}
