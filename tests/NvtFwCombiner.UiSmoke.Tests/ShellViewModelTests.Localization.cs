using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class GeneralWorkflowTests
{
    /// <summary>Verifies immutable language bundles are reused instead of rebuilt by each shell projection.</summary>
    [Theory]
    [InlineData("English")]
    [InlineData("ChineseTraditional")]
    public void LocalizedShellBundlesAreCached(string languageName)
    {
        ArgumentNullException.ThrowIfNull(languageName);
        ShellLanguage language = Enum.Parse<ShellLanguage>(languageName);
        Assert.Same(ShellTextResources.For(language), ShellTextResources.For(language));
    }

    /// <summary>Verifies hexadecimal viewport labels follow the selected shell language.</summary>
    [Fact]
    public void HexEditorLabelsAreLocalized()
    {
        var english = ShellTextResources.For(ShellLanguage.English);
        var traditionalChinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);

        Assert.Equal("Address", english.HexEditorAddressColumnLabel);
        Assert.Equal("位址", traditionalChinese.HexEditorAddressColumnLabel);
        Assert.Equal("ASCII", english.HexEditorAsciiColumnLabel);
        Assert.Equal("ASCII", traditionalChinese.HexEditorAsciiColumnLabel);
    }

    /// <summary>Verifies every bindable string is populated in both supported language bundles.</summary>
    [Theory]
    [InlineData("English")]
    [InlineData("ChineseTraditional")]
    public void LocalizedShellBundlesPopulateEveryString(string languageName)
    {
        ArgumentNullException.ThrowIfNull(languageName);
        ShellLanguage language = Enum.Parse<ShellLanguage>(languageName);
        var resources = ShellTextResources.For(language);
        IEnumerable<string> emptyProperties = typeof(ShellTextResources)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .Where(property => string.IsNullOrEmpty((string?)property.GetValue(resources)))
            .Select(property => property.Name);

        Assert.Empty(emptyProperties);
    }
}
