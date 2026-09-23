using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Read-only detected reference shape, independent of execution support.</summary>
public enum CtrlRamBaseKind
{
    /// <summary>No unambiguous reference shape was established.</summary>
    Unknown,
    /// <summary>A Standard TP-only reference.</summary>
    StandardTp,
    /// <summary>A Standard complete flash reference.</summary>
    StandardFlash,
    /// <summary>The declared NT51929 AB pair, possibly with blocking integrity issues.</summary>
    AbFlash,
}

/// <summary>Complete immutable reference observations from one captured image and publication.</summary>
public sealed class CtrlRamBaseInspection
{
    internal CtrlRamBaseInspection(CtrlRamBaseKind kind, CtrlRamAuthoringDraftState? effectiveDraft,
        IEnumerable<CtrlRamBaseBankInspection> banks, IEnumerable<CompositionIssue> issues, ResolutionToken resolutionToken,
        FileStamp referenceStamp, byte? standardEventBufferFormatVersion = null)
    {
        Kind = kind;
        EffectiveDraft = effectiveDraft;
        Banks = Array.AsReadOnly(banks.ToArray());
        Issues = Array.AsReadOnly(issues.ToArray());
        ResolutionToken = resolutionToken;
        ReferenceStamp = referenceStamp;
        StandardEventBufferFormatVersion = standardEventBufferFormatVersion;
    }

    /// <summary>Detected shape; blocking issues never demote an identified AB image to Standard.</summary>
    public CtrlRamBaseKind Kind { get; }
    /// <summary>Authoring choice to adopt with this exact batch.</summary>
    public CtrlRamAuthoringDraftState? EffectiveDraft { get; }
    /// <summary>Both canonical banks regardless of the selected write banks.</summary>
    public IReadOnlyList<CtrlRamBaseBankInspection> Banks { get; }
    /// <summary>Detection and structural issues; these do not grant execution support.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; }
    /// <summary>Publication owning the declarations used by this inspection.</summary>
    public ResolutionToken ResolutionToken { get; }
    /// <summary>Identity of the exact complete Reference capture used by these observations.</summary>
    public FileStamp ReferenceStamp { get; }
    /// <summary>Canonical optional field from a unique exact Standard candidate or unanimous current TP-only candidates in this capture.</summary>
    public byte? StandardEventBufferFormatVersion { get; }
}

/// <summary>Facts read from one declared bank without producing a replacement plan.</summary>
public sealed class CtrlRamBaseBankInspection
{
    internal CtrlRamBaseBankInspection(string bankId, ByteRange range, FirmwareConfigMetadataSnapshot? firmwareConfig,
        CompiledInputVersionObservation? tpVersion, CompiledInputVersionObservation? dpVersion,
        byte? eventBufferFormatVersion, IEnumerable<CompositionIssue> issues)
    {
        BankId = bankId;
        Range = range;
        FirmwareConfig = firmwareConfig;
        TpVersion = tpVersion;
        DpVersion = dpVersion;
        EventBufferFormatVersion = eventBufferFormatVersion;
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    /// <summary>Exact a-bank or b-bank identity.</summary>
    public string BankId { get; }
    /// <summary>Absolute canonical bank range.</summary>
    public ByteRange Range { get; }
    /// <summary>Validated bank-local FWConfig facts; its location remains bank-local.</summary>
    public FirmwareConfigMetadataSnapshot? FirmwareConfig { get; }
    /// <summary>TP version from the validated bank-local Backup.</summary>
    public CompiledInputVersionObservation? TpVersion { get; }
    /// <summary>DP observation decoded from the canonical bank CMI region.</summary>
    public CompiledInputVersionObservation? DpVersion { get; }
    /// <summary>Bank-local canonical structure byte, or null when its optional read cannot resolve.</summary>
    public byte? EventBufferFormatVersion { get; }
    /// <summary>Bank-specific validation issues.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; }
}
