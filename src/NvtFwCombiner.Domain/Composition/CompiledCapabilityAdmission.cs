using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>One profile-required confirmed capability and its exact admitted fact binding.</summary>
public sealed class CompiledCapabilityAdmission
{
    internal CompiledCapabilityAdmission(
        string requiredCapabilityId,
        FirmwareMapFactBinding<FirmwareCapabilityFact> binding)
    {
        RequiredCapabilityId = RequiredValue.NotBlank(requiredCapabilityId);
        Binding = RequiredValue.NotNull(binding);
        DomainInvariant.Reject(
            !StringComparer.Ordinal.Equals(requiredCapabilityId, binding.Value.CapabilityId) ||
            binding.Value.State != FirmwareCapabilityState.ConfirmedPresent ||
            binding.EffectiveKey.FactKind != FirmwareFactKind.Capability ||
            binding.DirectSourceKey.FactKind != FirmwareFactKind.Capability,
            "Compiled capability admission requires one confirmed-present capability binding.",
            nameof(binding));

    }

    /// <summary>Profile-required technical capability identifier proven present at map admission.</summary>
    public string RequiredCapabilityId { get; }

    /// <summary>Exact immutable effective/direct binding, capability value, applicability, and alias/evidence provenance.</summary>
    public FirmwareMapFactBinding<FirmwareCapabilityFact> Binding { get; }
}
