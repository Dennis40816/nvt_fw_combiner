namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks external production Registry-locator precedence without validating paths twice.</summary>
public sealed class UpdateSourceRegistryLocatorTests
{
    /// <summary>An explicit host option wins even when the inherited environment has another value.</summary>
    [Theory]
    [InlineData(@"G:\fixed\registry.json")]
    [InlineData(@"relative\must-fail-in-the-registry-adapter.json")]
    [InlineData("")]
    public void ExplicitLocatorAlwaysWins(string explicitLocator)
    {
        int reads = 0;

        string? resolved = UpdateSourceRegistryLocator.Resolve(
            explicitLocatorSupplied: true,
            explicitLocator,
            _ =>
            {
                reads++;
                return @"G:\environment\registry.json";
            });

        Assert.Equal(explicitLocator, resolved);
        Assert.Equal(0, reads);
    }

    /// <summary>The external environment supplies the locator only when no explicit option exists.</summary>
    [Theory]
    [InlineData(@"G:\external\registry.json", @"G:\external\registry.json")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void EnvironmentIsOneOptionalFallback(string? configured, string? expected)
    {
        string? observedName = null;

        string? resolved = UpdateSourceRegistryLocator.Resolve(
            explicitLocatorSupplied: false,
            explicitLocator: null,
            name =>
            {
                observedName = name;
                return configured;
            });

        Assert.Equal(UpdateSourceRegistryLocator.EnvironmentVariableName, observedName);
        Assert.Equal(expected, resolved);
    }

    /// <summary>An impossible explicit-null host state fails before another authority is consulted.</summary>
    [Fact]
    public void ExplicitNullStateFailsClosed()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            UpdateSourceRegistryLocator.Resolve(
                explicitLocatorSupplied: true,
                explicitLocator: null,
                _ => @"G:\environment\registry.json"));
    }
}
