namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static void ValidateTpMaximumInputGeometry(
        AddressSpace addressSpace,
        IReadOnlyList<CompiledResolvedPhysicalView> resolvedViews)
    {
        long maximumEndExclusive = 0;
        bool hasSourceView = false;
        foreach (CompiledResolvedPhysicalView view in resolvedViews.Where(view =>
                     StringComparer.Ordinal.Equals(view.AddressSpaceId, addressSpace.AddressSpaceId)))
        {
            maximumEndExclusive = Math.Max(maximumEndExclusive, view.Range.EndExclusive);
            hasSourceView = true;
        }

        if (!hasSourceView ||
            maximumEndExclusive > CompiledTpMaximum256KInputLengthRequirement.MaximumBytes ||
            addressSpace.Length != maximumEndExclusive ||
            addressSpace.InputPaddingByte is not null ||
            addressSpace.InputOversizePolicy != InputOversizePolicy.ExtractDeclaredRange ||
            addressSpace.AllowedInputLengths.Count != 0)
        {
            throw new ArgumentException(
                "TP maximum input requirements must extract the maximum resolved source span while accepting inputs through the 256 KiB limit.",
                nameof(addressSpace));
        }
    }

    private static void ValidateDeclaredPrefixInputGeometry(
        AddressSpace addressSpace,
        CompiledDeclaredPrefixWithWarningInputLengthRequirement requirement)
    {
        if (addressSpace.Length != requirement.RequiredEndExclusive ||
            addressSpace.InputPaddingByte is not null ||
            addressSpace.InputOversizePolicy != InputOversizePolicy.ExtractDeclaredRange ||
            addressSpace.AllowedInputLengths.Count != 0 ||
            !addressSpace.ExpectedInputLengths.SequenceEqual(requirement.ExpectedOuterLengths) ||
            !StringComparer.Ordinal.Equals(
                addressSpace.UnexpectedInputLengthIssueCode,
                requirement.UnexpectedOuterLengthIssueCode))
        {
            throw new ArgumentException(
                "Declared-prefix input requirements must bind the exact execution prefix, outer-length expectations, extraction policy, and warning code.",
                nameof(addressSpace));
        }
    }

    private static void ValidateSourceViewInputGeometry(
        AddressSpace addressSpace,
        CompiledSourceViewCoverageInputLengthRequirement requirement)
    {
        if (addressSpace.InputPaddingByte is not null ||
            addressSpace.InputOversizePolicy != InputOversizePolicy.ExtractDeclaredRange ||
            addressSpace.AllowedInputLengths.Count != 0 ||
            !addressSpace.ExpectedInputLengths.SequenceEqual(requirement.ExpectedOuterLengths) ||
            requirement.ExpectedOuterLengths.Any(length => length < addressSpace.Length) ||
            !StringComparer.Ordinal.Equals(
                addressSpace.UnexpectedInputLengthIssueCode,
                requirement.UnexpectedOuterLengthIssueCode))
        {
            throw new ArgumentException(
                "Source-view coverage requirements must bind one compiler-derived source span, optional outer-length diagnostics, and declared-range extraction.",
                nameof(addressSpace));
        }
    }
}
