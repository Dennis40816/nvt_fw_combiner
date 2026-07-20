// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Supported shell text languages.</summary>
public enum ShellLanguage
{
    /// <summary>English UI text.</summary>
    English,

    /// <summary>Traditional Chinese UI text.</summary>
    ChineseTraditional,
}

/// <summary>Localized text bundle for the production-backed UI shell.</summary>
public sealed partial class ShellTextResources
{
    private static readonly PlanningCardText EmptyPlanningCard = new(string.Empty, string.Empty, [], string.Empty);
    private static readonly Lazy<ShellTextResources> English = new(
        static () => CreateLocalized(ShellLanguage.English));
    private static readonly Lazy<ShellTextResources> ChineseTraditional = new(
        static () => CreateLocalized(ShellLanguage.ChineseTraditional));

    private ShellTextResources()
    {
    }

    /// <summary>Gets the resource bundle for a language.</summary>
    public static ShellTextResources For(ShellLanguage language)
    {
        return language switch
        {
            ShellLanguage.English => English.Value,
            ShellLanguage.ChineseTraditional => ChineseTraditional.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };
    }

    /// <summary>Converts the persisted language preference into a resource language.</summary>
    public static ShellLanguage LanguageFromPreference(string? preference)
    {
        return string.Equals(preference, "Traditional Chinese", StringComparison.Ordinal)
            ? ShellLanguage.ChineseTraditional
            : ShellLanguage.English;
    }

}

#pragma warning restore CS1591
