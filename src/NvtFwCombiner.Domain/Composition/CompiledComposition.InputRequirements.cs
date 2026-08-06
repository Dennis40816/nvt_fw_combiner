namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static void ValidateExactBytesInputGeometry(
        CompositionKind compositionKind,
        CompiledInputSlotRequirement requirement,
        CompiledExactBytesInputLengthRequirement exact,
        AddressSpace addressSpace)
    {
        if (requirement.ArtifactClass == CompiledInputArtifactClass.TpFirmware &&
            requirement.Normalization is CompiledNoInputNormalization &&
            exact.Bytes <= CompiledTpMaximum256KInputLengthRequirement.MaximumBytes &&
            addressSpace.Length == exact.Bytes &&
            addressSpace.InputPaddingByte is null &&
            addressSpace.InputOversizePolicy == InputOversizePolicy.Reject &&
            addressSpace.AllowedInputLengths.Count == 0 &&
            addressSpace.ExpectedInputLengths.Count == 0)
        {
            return;
        }

        if (requirement.ArtifactClass == CompiledInputArtifactClass.CtrlRamReplacement &&
            requirement.Normalization is CompiledTruncateCtrlRamInputNormalization &&
            compositionKind == CompositionKind.Replace &&
            addressSpace.Length == exact.Bytes &&
            addressSpace.InputPaddingByte is null &&
            addressSpace.InputOversizePolicy == InputOversizePolicy.TruncateWithWarning &&
            addressSpace.AllowedInputLengths.Count == 0 &&
            addressSpace.ExpectedInputLengths.Count == 0)
        {
            return;
        }

        throw new ArgumentException(
            "Exact input requirements must be an unnormalized TP input within 256 KiB or an evidenced typed CtrlRAM truncation input.",
            nameof(requirement));
    }
}
