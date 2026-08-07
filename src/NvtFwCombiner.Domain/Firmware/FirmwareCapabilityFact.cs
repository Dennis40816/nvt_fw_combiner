namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Evidence state for one map-scoped technical capability fact.</summary>
internal enum FirmwareCapabilityState
{
    /// <summary>Evidence confirms the capability is present.</summary>
    ConfirmedPresent,

    /// <summary>Evidence confirms the capability is absent.</summary>
    ConfirmedAbsent,

    /// <summary>Evidence does not establish the capability state.</summary>
    Unknown,
}

/// <summary>Immutable map-bound technical evidence that cannot grant execution support.</summary>
internal sealed class FirmwareCapabilityFact(
    string capabilityFactId,
    string capabilityId,
    FirmwareCapabilityState state,
    string reason,
    IEnumerable<string> evidenceRefs) : IFirmwareMapFact
{
    public string CapabilityFactId { get; } = RequiredValue.NotBlank(capabilityFactId);

    public string CapabilityId { get; } = RequiredValue.NotBlank(capabilityId);

    public FirmwareCapabilityState State { get; } = ClosedEnum.Require(
        state,
        "Unknown firmware capability state.");

    public string Reason { get; } = RequiredValue.NotBlank(reason);

    public FirmwareFactKind FactKind => FirmwareFactKind.Capability;

    public string CanonicalFactId => CapabilityFactId;

    public IReadOnlyList<string> EvidenceRefs { get; } = Array.AsReadOnly(
        ImmutableStringSnapshot.Create(
            evidenceRefs,
            nameof(evidenceRefs),
            "Capability evidence references must be non-empty values.",
            "Capability evidence references must be non-empty values.",
            "Capability evidence references must be ordinally unique."));
}
