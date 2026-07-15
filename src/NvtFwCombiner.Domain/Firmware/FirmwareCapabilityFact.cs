namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Evidence state for one map-scoped technical capability fact.</summary>
public enum FirmwareCapabilityState
{
    /// <summary>Evidence confirms the capability is present.</summary>
    ConfirmedPresent,

    /// <summary>Evidence confirms the capability is absent.</summary>
    ConfirmedAbsent,

    /// <summary>Evidence does not establish the capability state.</summary>
    Unknown,
}

/// <summary>Immutable map-bound technical evidence that cannot grant execution support.</summary>
public sealed class FirmwareCapabilityFact : IFirmwareMapFact
{
    private readonly string[] _evidenceRefs;

    /// <summary>Creates one evidence-backed map-scoped capability fact.</summary>
    public FirmwareCapabilityFact(
        string capabilityFactId,
        string capabilityId,
        FirmwareCapabilityState state,
        string reason,
        IEnumerable<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown firmware capability state.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        _evidenceRefs = [.. evidenceRefs];
        if (_evidenceRefs.Length == 0 || _evidenceRefs.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Capability evidence references must be non-empty values.", nameof(evidenceRefs));
        }

        if (_evidenceRefs.Distinct(StringComparer.Ordinal).Count() != _evidenceRefs.Length)
        {
            throw new ArgumentException("Capability evidence references must be ordinally unique.", nameof(evidenceRefs));
        }

        Array.Sort(_evidenceRefs, StringComparer.Ordinal);
        CapabilityFactId = capabilityFactId;
        CapabilityId = capabilityId;
        State = state;
        Reason = reason;
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    /// <summary>Stable aliasable capability fact identity.</summary>
    public string CapabilityFactId { get; }

    /// <summary>Technical capability identifier.</summary>
    public string CapabilityId { get; }

    /// <summary>Evidence-backed state that remains separate from execution support.</summary>
    public FirmwareCapabilityState State { get; }

    /// <summary>Required evidence explanation.</summary>
    public string Reason { get; }

    /// <inheritdoc />
    public FirmwareFactKind FactKind => FirmwareFactKind.Capability;

    /// <inheritdoc />
    public string CanonicalFactId => CapabilityFactId;

    /// <inheritdoc />
    public IReadOnlyList<string> EvidenceRefs { get; }
}
