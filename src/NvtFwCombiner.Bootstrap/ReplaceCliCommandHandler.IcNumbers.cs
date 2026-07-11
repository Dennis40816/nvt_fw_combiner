using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryCreateIcNumberSelection(
        CompositionProfileDefinition profile,
        ParsedOptions options,
        TextWriter error,
        [NotNullWhen(true)] out IcNumberSelection? selection)
    {
        selection = null;
        if (profile.IcNumberInputMode is null)
        {
            error.WriteLine($"error: replace profile '{profile.ProfileId}' does not declare an IC num input mode");
            return false;
        }

        if (!RequireOption(options, "--ic-num", error, out string? icNumber))
        {
            return false;
        }

        if (profile.IcNumberInputMode == IcNumberInputMode.SingleSelector)
        {
            if (options.Values.ContainsKey("--ic-family"))
            {
                error.WriteLine("error: --ic-family is used only by cascade IC num profiles");
                return false;
            }

            selection = WorkbenchIcNumberSelections.Single(icNumber);
            return true;
        }

        if (profile.IcNumberInputMode == IcNumberInputMode.NumericSelector)
        {
            if (options.Values.ContainsKey("--ic-family"))
            {
                error.WriteLine("error: --ic-family is used only by cascade IC num profiles");
                return false;
            }

            if (!int.TryParse(icNumber, out int parsedIcNumber) || parsedIcNumber <= 0)
            {
                error.WriteLine("error: numeric --ic-num must be a positive integer");
                return false;
            }

            selection = WorkbenchIcNumberSelections.Numeric(icNumber);
            return true;
        }

        if (!RequireOption(options, "--ic-family", error, out string? icFamily))
        {
            return false;
        }

        selection = WorkbenchIcNumberSelections.Cascade(icFamily, icNumber);
        return true;
    }
}
