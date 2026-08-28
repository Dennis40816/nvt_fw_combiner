namespace NvtFwCombiner.Bootstrap;

/// <summary>Resolves ordered external host configuration for fixed Registry replicas.</summary>
public static class UpdateSourceRegistryLocator
{
    internal const string EnvironmentVariableName = "NFC_UPDATE_SOURCE_REGISTRY_PATH";
    internal static IReadOnlyList<string> ProductionDefaults { get; } = Array.AsReadOnly(
    [
        @"G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\update-source-registry.json",
        @"G:\AUTO\Tool\NVT_FW_Combiner\update-source-registry.json",
    ]);

    /// <summary>Returns explicit, external override, or the release-owned ordered replica pair.</summary>
    public static IReadOnlyList<string> ResolveAll(
        bool explicitLocatorSupplied,
        string? explicitLocator,
        Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        if (explicitLocatorSupplied)
        {
            return [explicitLocator ?? throw new ArgumentNullException(nameof(explicitLocator))];
        }

        string? configured = readEnvironment(EnvironmentVariableName);
        return string.IsNullOrWhiteSpace(configured) ? ProductionDefaults : [configured];
    }
}
