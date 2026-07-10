namespace NvtFwCombiner.Bootstrap;

/// <summary>Equal-length General Replace patch action requested by UI or CLI authoring.</summary>
public sealed record WorkbenchGeneralReplacePatchInput(
    string PatchId,
    string TargetStart,
    string TargetEndInclusive,
    WorkbenchGeneralReplacePatchKind Kind,
    string Value);

/// <summary>Supported equal-length General Replace patch operations.</summary>
public enum WorkbenchGeneralReplacePatchKind
{
    /// <summary>Replace the selected range with the supplied hexadecimal bytes.</summary>
    Overwrite,

    /// <summary>Replace every selected byte with one hexadecimal byte value.</summary>
    Fill,
}
