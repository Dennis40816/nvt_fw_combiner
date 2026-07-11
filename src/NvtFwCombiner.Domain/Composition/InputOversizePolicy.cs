namespace NvtFwCombiner.Domain.Composition;

/// <summary>Declares how an address space handles supplied input bytes longer than its declared length.</summary>
public enum InputOversizePolicy
{
    /// <summary>Reject input bytes that exceed the declared address-space length.</summary>
    Reject = 0,

    /// <summary>Keep the leading declared bytes and emit a run diagnostic for the discarded tail bytes.</summary>
    TruncateWithWarning = 1,

    /// <summary>Use only the declared source range; unexpected artifact lengths are reported separately as warnings.</summary>
    ExtractDeclaredRange = 2,
}
