namespace NvtFwCombiner.Application.Authoring;

/// <summary>Closed immutable CtrlRAM compiler inputs, owned by one authoring revision.</summary>
public abstract record CtrlRamAuthoringDraftState : AuthoringDraftState
{
    internal CtrlRamAuthoringDraftState(AuthoringDraftKind kind) : base(kind) { }
}

/// <summary>Selected banks of an explicitly declared AB Reference.</summary>
public enum AbCtrlRamBankSelection
{
    /// <summary>Replace A and preserve every B byte.</summary>
    A,
    /// <summary>Replace B and preserve every A byte.</summary>
    B,
    /// <summary>Replace both banks using the shared sources.</summary>
    Both,
}

/// <summary>AB selection and optional independent version edits; null preserves that bank's version.</summary>
public sealed record AbCtrlRamDraftState : CtrlRamAuthoringDraftState
{
    /// <summary>Creates an explicit AB draft; both banks are selected by default.</summary>
    public AbCtrlRamDraftState(AbCtrlRamBankSelection banks = AbCtrlRamBankSelection.Both,
        CtrlRamFirmwareVersionDraftState? aVersion = null, CtrlRamFirmwareVersionDraftState? bVersion = null)
        : base(AuthoringDraftKind.AbCtrlRam)
    {
        if (!Enum.IsDefined(banks))
        {
            throw new ArgumentOutOfRangeException(nameof(banks));
        }
        Banks = banks;
        AVersion = aVersion;
        BVersion = bVersion;
    }

    /// <summary>Exact selected banks.</summary>
    public AbCtrlRamBankSelection Banks { get; }
    /// <summary>Optional A version edit.</summary>
    public CtrlRamFirmwareVersionDraftState? AVersion { get; }
    /// <summary>Optional B version edit.</summary>
    public CtrlRamFirmwareVersionDraftState? BVersion { get; }

    internal override AuthoringDraftState CreateImmutableSnapshot() { return this; }
    internal override bool HasSameValue(AuthoringDraftState other)
    {
        return other is AbCtrlRamDraftState draft && Banks == draft.Banks &&
            Equals(AVersion, draft.AVersion) && Equals(BVersion, draft.BVersion);
    }
}
