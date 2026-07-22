using Avalonia.Data.Converters;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Display-only labels for stable workbench mode tokens.</summary>
public static class WorkbenchModeDisplayConverters
{
    /// <summary>Maps contract tokens to the current product wording.</summary>
    public static FuncValueConverter<string, string> DisplayName { get; } = new(GetDisplayName);

    /// <summary>Returns a product label without changing the persisted/runtime token.</summary>
    public static string GetDisplayName(string? mode)
    {
        return mode switch
        {
            WorkbenchMergeModes.Standard => "Standard",
            WorkbenchMergeModes.General => "Customized",
            _ => mode ?? string.Empty,
        };
    }
}
