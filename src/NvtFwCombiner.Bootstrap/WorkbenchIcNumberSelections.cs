using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static class WorkbenchIcNumberSelections
{
    public static IcNumberSelection FromNumberToken(string number)
    {
        IcNumberInputMode mode = IcNumberSelectionTokens.IsSingle(number)
            ? IcNumberInputMode.SingleSelector
            : int.TryParse(number, out _)
                ? IcNumberInputMode.NumericSelector
                : IcNumberInputMode.CascadeSelector;
        return new IcNumberSelection(mode, [number]);
    }
}
