using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryCreateIcNumberSelection(
        CompiledComposition composition,
        ParsedOptions options,
        string icNumber,
        TextWriter error,
        [NotNullWhen(true)] out IcNumberSelection? selection)
    {
        selection = null;
        if (composition.IcNumberPolicy == CompiledIcNumberPolicy.NotApplicable)
        {
            error.WriteLine($"error: replace profile '{composition.ProfileId}' does not declare an IC num input mode");
            return false;
        }

        if (composition.IcNumberPolicy == CompiledIcNumberPolicy.SingleSelector)
        {
            if (options.Values.ContainsKey("--ic-family"))
            {
                error.WriteLine("error: --ic-family is used only by cascade IC num profiles");
                return false;
            }

            selection = WorkbenchIcNumberSelections.Single(icNumber);
            return true;
        }

        if (composition.IcNumberPolicy == CompiledIcNumberPolicy.NumericSelector)
        {
            if (options.Values.ContainsKey("--ic-family"))
            {
                error.WriteLine("error: --ic-family is used only by cascade IC num profiles");
                return false;
            }

            if (!int.TryParse(
                    icNumber,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedIcNumber) ||
                parsedIcNumber <= 0)
            {
                error.WriteLine("error: numeric --ic-num must be a positive integer");
                return false;
            }

            selection = WorkbenchIcNumberSelections.Numeric(
                parsedIcNumber.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        if (composition.IcNumberPolicy != CompiledIcNumberPolicy.CascadeSelector)
        {
            throw new ArgumentOutOfRangeException(
                nameof(composition),
                composition.IcNumberPolicy,
                "Unknown compiled IC-number policy.");
        }

        if (!RequireOption(options, "--ic-family", error, out string? icFamily))
        {
            return false;
        }

        selection = WorkbenchIcNumberSelections.Cascade(icFamily, icNumber);
        return true;
    }
}
