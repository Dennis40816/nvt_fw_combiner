namespace NvtFwCombiner.Bootstrap;

/// <summary>Resolves external host configuration for the one fixed Registry adapter.</summary>
public static class UpdateSourceRegistryLocator
{
    internal const string EnvironmentVariableName = "NFC_UPDATE_SOURCE_REGISTRY_PATH";

    /// <summary>Returns the explicit locator when supplied, otherwise the external environment value.</summary>
    public static string? Resolve(
        bool explicitLocatorSupplied,
        string? explicitLocator,
        Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        if (explicitLocatorSupplied)
        {
            return explicitLocator ?? throw new ArgumentNullException(nameof(explicitLocator));
        }

        string? configured = readEnvironment(EnvironmentVariableName);
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }
}
